using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Cerveau de la bête charretière. Aucun dressage : elle enfile un harnais,
    // s'attelle à une charrette en stock, puis alterne tournées de chargement
    // (plusieurs piles d'un coup, jusqu'à la capacité de la charrette) et
    // livraisons aux stocks. En dessous de deux piles à ramasser, elle reste
    // tranquille.
    public class JobGiver_Charretier : ThinkNode_JobGiver
    {
        private const int PilesMax = 8;
        private const int PilesMin = 2;

        protected override Job TryGiveJob(Pawn pawn)
        {
            Map map = pawn.Map;
            if (map == null || pawn.Faction != Faction.OfPlayer)
            {
                return null;
            }
            if (pawn.def.GetModExtension<ModExtension_BeteDeTrait>() == null)
            {
                return null;
            }

            MapComponent_Labour composante = map.GetComponent<MapComponent_Labour>();
            // La bête ne charrie qu'une fois menée dehors par un colon (en
            // service) : elle ne s'attelle ni ne sort de l'enclos seule.
            if (!composante.EstEnService(pawn))
            {
                return null;
            }

            // Cargaison à bord : on livre avant tout (même si la charrette
            // vient de rendre l'âme en chemin, les piles doivent descendre).
            if (EquipementUtility.PremierCargo(pawn) != null)
            {
                return JobMaker.MakeJob(AAW_DefOf.AAW_ViderCharrette);
            }

            // Charretage coupé (interrupteur), recherche non faite, ou bête pas
            // (encore) équipée : pas de nouvelle tournée. La cargaison à bord est
            // déjà partie (bloc ci-dessus).
            if (!composante.TacheAutorisee(pawn, TacheTrait.Charrette)
                || !AAW_DefOf.AAW_Charretterie.IsFinished
                || EquipementUtility.Porte(pawn, AAW_DefOf.AAW_HarnaisDeTrait) == null
                || EquipementUtility.Porte(pawn, AAW_DefOf.AAW_Charrette) == null)
            {
                return null;
            }

            return TourneeDeChargement(pawn, map);
        }

        private static Job TourneeDeChargement(Pawn pawn, Map map)
        {
            // Filtres bon marché d'abord, tri par distance, puis les tests
            // coûteux (atteignabilité, recherche de rangement) seulement
            // jusqu'à remplir la tournée, pas pour les centaines de piles
            // qu'un raid laisse au sol.
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
            candidats.Sort((a, b) => a.Position.DistanceToSquared(pawn.Position)
                .CompareTo(b.Position.DistanceToSquared(pawn.Position)));

            float masseLibre = EquipementUtility.CapaciteCharrette - EquipementUtility.MasseCargaison(pawn);
            List<LocalTargetInfo> cibles = new List<LocalTargetInfo>();
            List<int> quantites = new List<int>();
            for (int i = 0; i < candidats.Count && cibles.Count < PilesMax; i++)
            {
                Thing t = candidats[i];
                if (!pawn.CanReserve(t)
                    || !HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, t, false))
                {
                    continue;
                }
                if (!StoreUtility.TryFindBestBetterStoreCellFor(t, pawn, map,
                        StoreUtility.CurrentStoragePriorityOf(t), pawn.Faction, out _))
                {
                    continue;
                }
                float unitaire = t.GetStatValue(StatDefOf.Mass);
                int n = unitaire <= 0f
                    ? t.stackCount
                    : Mathf.Min(t.stackCount, Mathf.FloorToInt(masseLibre / unitaire));
                if (n <= 0)
                {
                    continue;
                }
                cibles.Add(t);
                quantites.Add(n);
                masseLibre -= n * unitaire;
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
    }
}
