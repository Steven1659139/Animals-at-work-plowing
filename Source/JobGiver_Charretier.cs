using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Cerveau de la bête charretière, dans l'arbre de pensée. Ne produit un job
    // que si elle est en service (menée dehors par un colon) et déjà attelée à
    // sa charrette. Elle alterne tournées de chargement (plusieurs piles d'un
    // coup, jusqu'à la capacité de la charrette) et livraisons aux stocks. En
    // dessous de deux piles à ramasser, elle reste tranquille.
    public class JobGiver_Charretier : ThinkNode_JobGiver
    {
        // Garde-fou sur la longueur d'une tournée, pas la vraie limite : c'est la
        // masse qui doit décider quand la charrette est pleine. Un plafond bas
        // faisait rentrer les charrettes à moitié vides dès que la cargaison ne
        // s'empilait pas : huit gravats de 20 kg ne font que 160 kg.
        private const int MaxStacks = 25;
        private const int MinStacks = 2;
        // Rayon d'enchaînement : la première pile prise, les suivantes se
        // cherchent autour d'ELLE, pas autour de la bête. La tournée reste un
        // paquet compact au lieu d'une étoile aux quatre coins de la carte.
        // Limite volontairement souple : si la tournée n'atteint pas MinStacks
        // dans ce rayon, on la refait sans limite plutôt que de ne rien faire.
        private const float ChainingRadius = 25f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            // La bête ne charrie qu'une fois menée dehors par un colon (en
            // service).
            if (!ServiceTrait.OnDutyAtColony(pawn, out MapComponent_Labour component))
            {
                return null;
            }

            // Cargaison à bord : on livre avant tout (même si la charrette
            // vient de rendre l'âme en chemin, les piles doivent descendre).
            if (EquipementUtility.FirstCargo(pawn) != null)
            {
                return JobMaker.MakeJob(AAW_DefOf.AAW_ViderCharrette);
            }

            // Elle vient de déverser faute de rangement : on ne relance pas une
            // tournée tout de suite. Les piles déversées sont à ses pieds, donc
            // les premières trouvées : elle les reprendrait à l'instant pour
            // les redéverser, en usant sa charrette à chaque passage.
            if (component.InDumpGrace(pawn))
            {
                return null;
            }

            // Charretage coupé (interrupteur), recherche non faite, ou bête pas
            // (encore) équipée : pas de nouvelle tournée. La cargaison à bord est
            // déjà partie (bloc ci-dessus).
            if (!ServiceTrait.Equipped(pawn, TacheTrait.Charrette, component))
            {
                return null;
            }

            return LoadingRound(pawn, pawn.Map);
        }

        // Le meilleur rangement de cette pile pour cette bête, tel que vanilla
        // le cherche pour un porteur : IsGoodStoreCell écarte les cases
        // interdites à la bête (zone autorisée), celles qu'elle ne peut pas
        // réserver, et celles qu'elle ne peut pas atteindre. Ce dernier test
        // part de la PILE, pas de la bête, avec les règles de son espèce :
        // clôtures pour le bétail, portes qu'un animal n'ouvre pas. Il se pose
        // donc aussi bien depuis l'enclos que depuis le champ, et c'est ce qui
        // tranche le cas d'un stock derrière une porte fermée.
        private static bool BestCell(Pawn beast, Map map, Thing stack,
            StoragePriority currentPriority, out IntVec3 cell)
        {
            return StoreUtility.TryFindBestBetterStoreCellFor(stack, beast, map,
                currentPriority, beast.Faction, out cell, needAccurateResult: false);
        }

        // Cette pile vaut-elle une tournée pour cette bête ? Sert à l'envoi
        // (ServiceTrait.CountStacks), décidé depuis l'enclos, où la bête
        // n'atteint rien elle-même : on ne lui demande donc rien de plus que
        // le rangement. Une pile que rien n'accepte, ou dont le seul meilleur
        // stock est hors de sa portée, n'est pas du travail en attente : la
        // sortir pour ça, c'est la ramener aussitôt.
        //
        // Coûteux : à n'appeler qu'après les filtres bon marché.
        public static bool HasDestination(Pawn beast, Map map, Thing stack)
        {
            return BestCell(beast, map, stack,
                StoreUtility.CurrentStoragePriorityOf(stack), out _);
        }

        // La case où CETTE bête ira réellement déposer cette pile, si elle
        // existe. C'est la question du charretier, et elle doit se poser à
        // l'identique au chargement (CanBeCarted) et à la livraison
        // (JobDriver_ViderCharrette.NextDelivery) : c'est leur désaccord
        // qui faisait tourner la bête en boucle. Elle chargeait au nom d'un
        // stock que la livraison ne lui laissait pas joindre, reposait la pile
        // là où elle venait de la prendre, et recommençait.
        //
        // Le CanReach par-dessus, depuis la bête cette fois, n'ajoute que le
        // danger : vanilla teste en Deadly, on refuse une case en feu ou
        // mortellement froide. Même mode d'approche (ClosestTouch) que vanilla
        // et que le JobDriver. Ne vaut qu'une fois la bête au champ.
        public static bool Destination(Pawn beast, Map map, Thing stack,
            StoragePriority currentPriority, out IntVec3 cell)
        {
            return BestCell(beast, map, stack, currentPriority, out cell)
                && beast.CanReach(cell, PathEndMode.ClosestTouch, Danger.Some);
        }

        // Une pile que cette bête peut emporter de là où elle est : la question
        // du meneur, plus l'accessibilité et la capacité de ramassage. Ne vaut
        // qu'une fois la bête au champ : depuis l'enclos, elle répond toujours non.
        public static bool CanBeCarted(Pawn beast, Map map, Thing stack)
        {
            return beast.CanReserve(stack)
                && HaulAIUtility.PawnCanAutomaticallyHaulFast(beast, stack, false)
                && Destination(beast, map, stack,
                    StoreUtility.CurrentStoragePriorityOf(stack), out _);
        }

        private static Job LoadingRound(Pawn pawn, Map map)
        {
            // D'abord la tournée resserrée, qui est celle qu'on veut. Si elle ne
            // réunit pas assez de piles (carte clairsemée, fin de ramassage), on
            // reprend sans rayon : mieux vaut une tournée étalée que rien.
            Job job = Round(pawn, map, ChainingRadius * ChainingRadius);
            return job ?? Round(pawn, map, float.MaxValue);
        }

        // Itinéraire glouton : la pile la plus proche de la bête, puis à chaque
        // fois la plus proche de la PRÉCÉDENTE. L'ordre de la file est donc
        // l'ordre de passage, ce qui évite les allers-retours d'un bout à l'autre
        // de la carte que donnait un simple tri par distance au point de départ.
        private static Job Round(Pawn pawn, Map map, float maxRadiusSquared)
        {
            // Filtres bon marché d'abord ; les tests coûteux (atteignabilité,
            // recherche de rangement) ne tournent que pour une pile plus proche
            // que la meilleure du tour, pas pour les centaines de piles qu'un
            // raid laisse au sol. Une pile recalée est écartée définitivement.
            List<Thing> candidates = new List<Thing>();
            foreach (Thing t in map.listerHaulables.ThingsPotentiallyNeedingHauling())
            {
                // L'équipement de trait n'est jamais de la cargaison : à bord,
                // il passerait pour l'équipement porté, le déchargeur
                // l'ignorerait (jamais livré) et il serait confisqué aux
                // autres bêtes. Les colons s'en chargent, râtelier compris.
                if (EquipementUtility.IsEquipment(t.def))
                {
                    continue;
                }
                if (!t.IsForbidden(pawn))
                {
                    candidates.Add(t);
                }
            }
            if (candidates.Count < MinStacks)
            {
                return null;
            }

            float freeMass = EquipementUtility.FreeMass(pawn);
            List<LocalTargetInfo> targets = new List<LocalTargetInfo>();
            List<int> counts = new List<int>();
            IntVec3 from = pawn.Position;
            // La première pile se cherche sans rayon : c'est la plus proche de la
            // bête, le rayon ne borne que l'enchaînement à partir d'elle.
            float radiusSquared = float.MaxValue;

            while (targets.Count < MaxStacks && candidates.Count > 0)
            {
                // Une passe = la pile la plus proche du point courant, testée à
                // fond ; recalée, elle sort de la liste et on passe à la
                // suivante. Seule la tête d'un tri servirait : un balayage du
                // minimum suffit, sans allocation, et une pile recalée n'est
                // jamais retestée.
                Thing kept = null;
                int count = 0;
                float keptMass = 0f;
                while (candidates.Count > 0)
                {
                    int near = Closest(candidates, from);
                    Thing t = candidates[near];
                    // La plus proche hors rayon : la passe est finie.
                    if (t.Position.DistanceToSquared(from) > radiusSquared)
                    {
                        break;
                    }
                    Remove(candidates, near);
                    if (!CanBeCarted(pawn, map, t))
                    {
                        continue;
                    }
                    float perUnit = t.GetStatValue(StatDefOf.Mass);
                    int n = perUnit <= 0f
                        ? t.stackCount
                        : Mathf.Min(t.stackCount, Mathf.FloorToInt(freeMass / perUnit));
                    if (n <= 0)
                    {
                        // La place libre ne fera que diminuer : cette pile ne
                        // rentrera plus dans cette tournée-ci.
                        continue;
                    }
                    kept = t;
                    count = n;
                    keptMass = n * perUnit;
                    break;
                }
                if (kept == null)
                {
                    break;
                }
                targets.Add(kept);
                counts.Add(count);
                freeMass -= keptMass;
                from = kept.Position;
                radiusSquared = maxRadiusSquared;
            }
            if (targets.Count < MinStacks)
            {
                return null;
            }
            Job job = JobMaker.MakeJob(AAW_DefOf.AAW_ChargerCharrette);
            job.targetQueueA = targets;
            job.countQueue = counts;
            return job;
        }

        private static int Closest(List<Thing> stacks, IntVec3 from)
        {
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < stacks.Count; i++)
            {
                float dist = stacks[i].Position.DistanceToSquared(from);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }

        // L'ordre de la liste n'a pas d'importance : le dernier prend la place
        // du retiré, ce qui évite de décaler tout ce qui suit.
        private static void Remove(List<Thing> stacks, int index)
        {
            int last = stacks.Count - 1;
            stacks[index] = stacks[last];
            stacks.RemoveAt(last);
        }
    }
}
