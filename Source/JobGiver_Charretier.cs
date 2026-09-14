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
        private const int PilesMax = 25;
        private const int PilesMin = 2;
        // Rayon d'enchaînement : la première pile prise, les suivantes se
        // cherchent autour d'ELLE, pas autour de la bête. La tournée reste un
        // paquet compact au lieu d'une étoile aux quatre coins de la carte.
        // Limite volontairement souple : si la tournée n'atteint pas PilesMin
        // dans ce rayon, on la refait sans limite plutôt que de ne rien faire.
        private const float RayonEnchainement = 25f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            // La bête ne charrie qu'une fois menée dehors par un colon (en
            // service).
            if (!ServiceTrait.EnServiceALaColonie(pawn, out MapComponent_Labour composante))
            {
                return null;
            }

            // Cargaison à bord : on livre avant tout (même si la charrette
            // vient de rendre l'âme en chemin, les piles doivent descendre).
            if (EquipementUtility.PremierCargo(pawn) != null)
            {
                return JobMaker.MakeJob(AAW_DefOf.AAW_ViderCharrette);
            }

            // Elle vient de déverser faute de rangement : on ne relance pas une
            // tournée tout de suite. Les piles déversées sont à ses pieds, donc
            // les premières trouvées : elle les reprendrait à l'instant pour
            // les redéverser, en usant sa charrette à chaque passage.
            if (composante.EnRepitDeDeversement(pawn))
            {
                return null;
            }

            // Charretage coupé (interrupteur), recherche non faite, ou bête pas
            // (encore) équipée : pas de nouvelle tournée. La cargaison à bord est
            // déjà partie (bloc ci-dessus).
            if (!ServiceTrait.Equipee(pawn, TacheTrait.Charrette, composante))
            {
                return null;
            }

            return TourneeDeChargement(pawn, pawn.Map);
        }

        // Cette pile a-t-elle un stock où aller ? C'est le test qui manquait au
        // meneur : une pile posée là où rien ne l'accepte n'est pas du travail
        // en attente, aucune tournée ne la prendra jamais.
        //
        // Sert au seul envoi (ServiceTrait.ComptePiles), décidé depuis
        // l'enclos. Sans porteur, délibérément : dans l'enclos la bête est
        // bloquée par les clôtures, rien du dehors ne lui est accessible, et
        // lui poser la question reviendrait à ne jamais la sortir. Le
        // chargement, lui, se décide une fois la bête au champ et passe par
        // Destination, qui tient compte d'elle.
        //
        // Coûteux : à n'appeler qu'après les filtres bon marché.
        public static bool ADestination(Map carte, Thing pile)
        {
            return StoreUtility.TryFindBestBetterStoreCellFor(pile, null, carte,
                StoreUtility.CurrentStoragePriorityOf(pile), Faction.OfPlayer,
                out _, needAccurateResult: false);
        }

        // La case où CETTE bête ira réellement déposer cette pile, si elle
        // existe. C'est la question du charretier, et elle doit se poser à
        // l'identique au chargement (PeutEtreCharriee) et à la livraison
        // (JobDriver_ViderCharrette.ProchaineLivraison) : c'est leur désaccord
        // qui faisait tourner la bête en boucle. Elle chargeait au nom d'un
        // stock que la livraison ne lui laissait pas joindre, reposait la pile
        // là où elle venait de la prendre, et recommençait.
        //
        // La bête en porteur, à la différence d'ADestination : la zone
        // autorisée de l'animal et les réservations comptent ici, puisqu'on
        // parle du trajet qu'elle fera.
        //
        // Et le CanReach par-dessus, parce que TryFindBestBetterStoreCellFor
        // n'en fait aucun, avec porteur comme sans : le porteur ne change que
        // l'interdiction, la réservation et l'origine des distances. OnCell
        // comme le JobDriver, qui fait déposer la bête depuis la case même.
        public static bool Destination(Pawn bete, Map carte, Thing pile,
            StoragePriority prioriteActuelle, out IntVec3 cellule)
        {
            return StoreUtility.TryFindBestBetterStoreCellFor(pile, bete, carte,
                    prioriteActuelle, bete.Faction, out cellule,
                    needAccurateResult: false)
                && bete.CanReach(cellule, PathEndMode.OnCell, Danger.Some);
        }

        // Une pile que cette bête peut emporter de là où elle est : la question
        // du meneur, plus l'accessibilité et la capacité de ramassage. Ne vaut
        // qu'une fois la bête au champ : depuis l'enclos, elle répond toujours non.
        public static bool PeutEtreCharriee(Pawn bete, Map carte, Thing pile)
        {
            return bete.CanReserve(pile)
                && HaulAIUtility.PawnCanAutomaticallyHaulFast(bete, pile, false)
                && Destination(bete, carte, pile,
                    StoreUtility.CurrentStoragePriorityOf(pile), out _);
        }

        private static Job TourneeDeChargement(Pawn pawn, Map map)
        {
            // D'abord la tournée resserrée, qui est celle qu'on veut. Si elle ne
            // réunit pas assez de piles (carte clairsemée, fin de ramassage), on
            // reprend sans rayon : mieux vaut une tournée étalée que rien.
            Job job = Tournee(pawn, map, RayonEnchainement * RayonEnchainement);
            return job ?? Tournee(pawn, map, float.MaxValue);
        }

        // Itinéraire glouton : la pile la plus proche de la bête, puis à chaque
        // fois la plus proche de la PRÉCÉDENTE. L'ordre de la file est donc
        // l'ordre de passage, ce qui évite les allers-retours d'un bout à l'autre
        // de la carte que donnait un simple tri par distance au point de départ.
        private static Job Tournee(Pawn pawn, Map map, float rayonCarreMax)
        {
            // Filtres bon marché d'abord ; les tests coûteux (atteignabilité,
            // recherche de rangement) ne tournent que pour une pile plus proche
            // que la meilleure du tour, pas pour les centaines de piles qu'un
            // raid laisse au sol. Une pile recalée est écartée définitivement.
            List<Thing> candidats = new List<Thing>();
            foreach (Thing t in map.listerHaulables.ThingsPotentiallyNeedingHauling())
            {
                // L'équipement de trait n'est jamais de la cargaison : à bord,
                // il passerait pour l'équipement porté, le déchargeur
                // l'ignorerait (jamais livré) et il serait confisqué aux
                // autres bêtes. Les colons s'en chargent, râtelier compris.
                if (EquipementUtility.EstEquipement(t.def))
                {
                    continue;
                }
                if (!t.IsForbidden(pawn))
                {
                    candidats.Add(t);
                }
            }
            if (candidats.Count < PilesMin)
            {
                return null;
            }

            float masseLibre = EquipementUtility.MasseLibre(pawn);
            List<LocalTargetInfo> cibles = new List<LocalTargetInfo>();
            List<int> quantites = new List<int>();
            IntVec3 depuis = pawn.Position;
            // La première pile se cherche sans rayon : c'est la plus proche de la
            // bête, le rayon ne borne que l'enchaînement à partir d'elle.
            float rayonCarre = float.MaxValue;

            while (cibles.Count < PilesMax && candidats.Count > 0)
            {
                // Une passe = la pile la plus proche du point courant, testée à
                // fond ; recalée, elle sort de la liste et on passe à la
                // suivante. Seule la tête d'un tri servirait : un balayage du
                // minimum suffit, sans allocation, et une pile recalée n'est
                // jamais retestée.
                Thing retenue = null;
                int quantite = 0;
                float masseRetenue = 0f;
                while (candidats.Count > 0)
                {
                    int proche = PlusProche(candidats, depuis);
                    Thing t = candidats[proche];
                    // La plus proche hors rayon : la passe est finie.
                    if (t.Position.DistanceToSquared(depuis) > rayonCarre)
                    {
                        break;
                    }
                    Retirer(candidats, proche);
                    if (!PeutEtreCharriee(pawn, map, t))
                    {
                        continue;
                    }
                    float unitaire = t.GetStatValue(StatDefOf.Mass);
                    int n = unitaire <= 0f
                        ? t.stackCount
                        : Mathf.Min(t.stackCount, Mathf.FloorToInt(masseLibre / unitaire));
                    if (n <= 0)
                    {
                        // La place libre ne fera que diminuer : cette pile ne
                        // rentrera plus dans cette tournée-ci.
                        continue;
                    }
                    retenue = t;
                    quantite = n;
                    masseRetenue = n * unitaire;
                    break;
                }
                if (retenue == null)
                {
                    break;
                }
                cibles.Add(retenue);
                quantites.Add(quantite);
                masseLibre -= masseRetenue;
                depuis = retenue.Position;
                rayonCarre = rayonCarreMax;
            }
            if (cibles.Count < PilesMin)
            {
                return null;
            }
            Job job = JobMaker.MakeJob(AAW_DefOf.AAW_ChargerCharrette);
            job.targetQueueA = cibles;
            job.countQueue = quantites;
            return job;
        }

        private static int PlusProche(List<Thing> piles, IntVec3 depuis)
        {
            int meilleur = 0;
            float meilleureDist = float.MaxValue;
            for (int i = 0; i < piles.Count; i++)
            {
                float dist = piles[i].Position.DistanceToSquared(depuis);
                if (dist < meilleureDist)
                {
                    meilleureDist = dist;
                    meilleur = i;
                }
            }
            return meilleur;
        }

        // L'ordre de la liste n'a pas d'importance : le dernier prend la place
        // du retiré, ce qui évite de décaler tout ce qui suit.
        private static void Retirer(List<Thing> piles, int index)
        {
            int dernier = piles.Count - 1;
            piles[index] = piles[dernier];
            piles.RemoveAt(dernier);
        }
    }
}
