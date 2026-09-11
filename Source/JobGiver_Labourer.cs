using RimWorld;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Cerveau de la bête laboureuse, dans l'arbre de pensée. Ne produit un job
    // que si elle est en service (menée au champ par un colon) et déjà équipée.
    // Elle laboure en sillons droits : chaque case retournée, elle poursuit tout
    // droit tant que la suivante se laboure, sinon elle ouvre un nouveau sillon à
    // la case la plus proche. Retourne null s'il n'y a rien à faire.
    public class JobGiver_Labourer : ThinkNode_JobGiver
    {
        private const float FertiliteMin = 0.9f;
        // En deçà, les plantes vanilla ne poussent plus (Plant.GrowthRateFactor_Temperature).
        private const float TemperatureMin = 0f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            // La bête ne laboure qu'une fois menée au champ par un colon (en
            // service) et déjà équipée du harnais et de la charrue.
            if (!ServiceTrait.PreteAuTravail(pawn, TacheTrait.Labour, out MapComponent_Labour composante)
                || !composante.TravailLabourEnAttente())
            {
                return null;
            }

            IntVec3 cible = ChoisirCase(pawn, pawn.Map, composante);
            if (!cible.IsValid)
            {
                return null;
            }
            return JobMaker.MakeJob(AAW_DefOf.AAW_Labourer, cible);
        }

        // La prochaine case à labourer, en sillons droits : d'abord tout droit
        // dans le sillon en cours (si la bête est encore dessus et que la case
        // suivante se laboure), sinon la case labourable la plus proche, qui
        // ouvre un nouveau sillon dans la meilleure direction.
        private static IntVec3 ChoisirCase(Pawn pawn, Map map, MapComponent_Labour composante)
        {
            if (composante.EnSillon(pawn, out IntVec3 derniere, out IntVec3 direction)
                && pawn.Position == derniere)
            {
                IntVec3 suite = derniere + direction;
                if (Labourable(suite, map, composante)
                    && pawn.CanReserveAndReach(suite, PathEndMode.OnCell, Danger.Some))
                {
                    composante.NoterSillon(pawn, suite, direction);
                    return suite;
                }
            }

            IntVec3 depart = CaseLabourableLaPlusProche(pawn);
            if (!depart.IsValid)
            {
                composante.OublierSillon(pawn);
                return IntVec3.Invalid;
            }
            composante.NoterSillon(pawn, depart, DirectionSillon(depart, map, composante));
            return depart;
        }

        // Cardinaux testés dans cet ordre : les sillons partent horizontaux et
        // serpentent (aller-retour) le long du champ.
        private static readonly IntVec3[] Cardinaux =
            { IntVec3.East, IntVec3.West, IntVec3.North, IntVec3.South };

        // Direction d'un nouveau sillon depuis 'depart' : le premier cardinal
        // dont la case voisine se laboure encore. Est par défaut.
        private static IntVec3 DirectionSillon(IntVec3 depart, Map map, MapComponent_Labour composante)
        {
            foreach (IntVec3 d in Cardinaux)
            {
                if (Labourable(depart + d, map, composante))
                {
                    return d;
                }
            }
            return IntVec3.East;
        }

        // La case se laboure-t-elle, dans une zone de culture où le labour est
        // autorisé ? Sert au suivi de sillon, case par case, et au JobDriver
        // pour abandonner une case qui ne s'y prête plus en cours d'ouvrage.
        public static bool Labourable(IntVec3 cellule, Map map, MapComponent_Labour composante)
        {
            return cellule.InBounds(map)
                && cellule.GetZone(map) is Zone_Growing zoneCulture
                && composante.LabourAutorise(zoneCulture)
                && CelluleLabourable(cellule, map);
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

        // Case labourable la plus proche de la bête, dans les deux usages :
        //   meneur == null → la bête ouvre un sillon seule, elle doit réserver.
        //   meneur != null → un colon l'y mène à la corde (voir ServiceTrait).
        // Tests coûteux (atteignabilité) en dernier, seulement pour une case plus
        // proche que la meilleure trouvée.
        public static IntVec3 CaseLabourableLaPlusProche(Pawn bete, Pawn meneur = null)
        {
            Map map = bete.Map;
            MapComponent_Labour composante = MapComponent_Labour.De(map);
            IntVec3 meilleure = IntVec3.Invalid;
            float meilleureDist = float.MaxValue;
            foreach (Zone zone in map.zoneManager.AllZones)
            {
                if (!(zone is Zone_Growing zoneCulture) || !composante.LabourAutorise(zoneCulture))
                {
                    continue;
                }
                foreach (IntVec3 cellule in zoneCulture.Cells)
                {
                    float dist = cellule.DistanceToSquared(bete.Position);
                    if (dist >= meilleureDist || !CelluleLabourable(cellule, map))
                    {
                        continue;
                    }
                    if (ServiceTrait.Accessible(bete, meneur, cellule))
                    {
                        meilleure = cellule;
                        meilleureDist = dist;
                    }
                }
            }
            return meilleure;
        }

        // Case vers laquelle le colon mène la bête : la plus proche d'elle et
        // joignable en la menant. Elle re-scanne ensuite depuis là.
        public static bool TrouverCelluleTravail(Pawn bete, Pawn meneur, out IntVec3 result)
        {
            result = CaseLabourableLaPlusProche(bete, meneur);
            return result.IsValid;
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
