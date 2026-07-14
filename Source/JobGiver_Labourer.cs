using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Appelé par l'arbre de pensée quand une bête de trait apprivoisée cherche
    // quoi faire. Aucun dressage : le travail est affaire d'équipement. La bête
    // enfile d'abord un harnais, s'attelle à une charrue, puis laboure.
    // Retourne null si rien de tout ça n'est possible (elle passe à autre chose).
    public class JobGiver_Labourer : ThinkNode_JobGiver
    {
        private const float FertiliteMin = 0.9f;
        // En deçà, les plantes vanilla ne poussent plus (Plant.GrowthRateFactor_Temperature).
        private const float TemperatureMin = 0f;

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
            if (!AAW_DefOf.AAW_Harnachement.IsFinished)
            {
                return null;
            }

            MapComponent_Labour composante = map.GetComponent<MapComponent_Labour>();
            bool travailEnAttente = composante.TravailLabourEnAttente();
            // Terre gelée ou zones toutes retournées : la bête pose la charrue
            // et l'attelage d'hiver (le grattoir à neige) redevient possible.
            if (EquipementUtility.Porte(pawn, AAW_DefOf.AAW_Charrue) != null && !travailEnAttente)
            {
                EquipementUtility.DeposerAttelage(pawn, AAW_DefOf.AAW_Charrue);
                return null;
            }
            if (!travailEnAttente)
            {
                return null;
            }
            // Sans harnais sur le dos, la bête va d'abord en enfiler un.
            if (EquipementUtility.Porte(pawn, AAW_DefOf.AAW_HarnaisDeTrait) == null)
            {
                return EquipementUtility.AllerChercher(pawn, AAW_DefOf.AAW_HarnaisDeTrait, AAW_DefOf.AAW_Harnacher);
            }
            // Puis il lui faut une charrue, jamais en plus d'un autre
            // attelage : cette bête-là tire déjà autre chose.
            if (EquipementUtility.Porte(pawn, AAW_DefOf.AAW_Charrue) == null)
            {
                if (EquipementUtility.AttelagePorte(pawn) != null)
                {
                    return null;
                }
                return EquipementUtility.AllerChercher(pawn, AAW_DefOf.AAW_Charrue, AAW_DefOf.AAW_Atteler);
            }

            // Départ aléatoire plutôt qu'InRandomOrder : même étalement des
            // bêtes entre zones et cases, sans copier-mélanger des listes
            // entières à chaque décision.
            List<Zone> zones = map.zoneManager.AllZones;
            int departZone = Rand.Range(0, zones.Count);
            for (int i = 0; i < zones.Count; i++)
            {
                if (!(zones[(departZone + i) % zones.Count] is Zone_Growing zoneCulture)
                    || !composante.LabourAutorise(zoneCulture))
                {
                    continue;
                }
                List<IntVec3> cellules = zoneCulture.Cells;
                int departCellule = Rand.Range(0, cellules.Count);
                for (int j = 0; j < cellules.Count; j++)
                {
                    IntVec3 cellule = cellules[(departCellule + j) % cellules.Count];
                    if (CelluleLabourable(cellule, map)
                        && pawn.CanReserveAndReach(cellule, PathEndMode.OnCell, Danger.Some))
                    {
                        return JobMaker.MakeJob(AAW_DefOf.AAW_Labourer, cellule);
                    }
                }
            }
            return null;
        }

        // Y a-t-il une case à labourer quelque part (réservations mises à
        // part) ? Sert aussi à décider de poser ou de prendre la charrue.
        // Toujours via le cache de MapComponent_Labour, jamais en direct.
        public static bool TravailExiste(Map map, MapComponent_Labour composante)
        {
            foreach (Zone zone in map.zoneManager.AllZones)
            {
                if (!(zone is Zone_Growing zoneCulture) || !composante.LabourAutorise(zoneCulture))
                {
                    continue;
                }
                foreach (IntVec3 cellule in zoneCulture.Cells)
                {
                    if (CelluleLabourable(cellule, map))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool CelluleLabourable(IntVec3 cellule, Map map)
        {
            TerrainDef terrain = cellule.GetTerrain(map);
            if (terrain == AAW_DefOf.AAW_SolLaboure)
            {
                return false;
            }
            // Sol cultivable ordinaire uniquement : ni les planchers (fertilité 0),
            // ni le gravier (0.7), ni le sol riche (1.4) qu'on dégraderait.
            if (terrain.fertility < FertiliteMin || terrain.fertility >= AAW_DefOf.AAW_SolLaboure.fertility)
            {
                return false;
            }
            if (!terrain.affordances.Contains(AAW_DefOf.GrowSoil))
            {
                return false;
            }
            // Labourer un sol gelé est du harnais gaspillé : rien n'y poussera
            // avant que la terre ne se tasse. Température par case, pour que
            // les serres chauffées restent labourables en plein hiver.
            if (GenTemperature.GetTemperatureForCell(cellule, map) < TemperatureMin)
            {
                return false;
            }
            return cellule.GetEdifice(map) == null;
        }
    }
}
