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
            if (!ServiceTrait.ReadyForWork(pawn, TacheTrait.Labour, out MapComponent_Labour component)
                || !component.PlowWorkPending())
            {
                return null;
            }

            IntVec3 target = ChooseCell(pawn, pawn.Map, component);
            if (!target.IsValid)
            {
                return null;
            }
            return JobMaker.MakeJob(AAW_DefOf.AAW_Labourer, target);
        }

        // La prochaine case à labourer, en sillons droits : d'abord tout droit
        // dans le sillon en cours (si la bête est encore dessus et que la case
        // suivante se laboure), sinon la case labourable la plus proche, qui
        // ouvre un nouveau sillon dans la meilleure direction.
        private static IntVec3 ChooseCell(Pawn pawn, Map map, MapComponent_Labour component)
        {
            if (component.InFurrow(pawn, out IntVec3 last, out IntVec3 direction)
                && pawn.Position == last)
            {
                IntVec3 rest = last + direction;
                if (Plowable(rest, map, component)
                    && pawn.CanReserveAndReach(rest, PathEndMode.OnCell, Danger.Some))
                {
                    component.NoteFurrow(pawn, rest, direction);
                    return rest;
                }
            }

            IntVec3 start = ClosestPlowableCell(pawn);
            if (!start.IsValid)
            {
                component.ForgetFurrow(pawn);
                return IntVec3.Invalid;
            }
            component.NoteFurrow(pawn, start, FurrowDirection(start, map, component));
            return start;
        }

        // Cardinaux testés dans cet ordre : les sillons partent horizontaux et
        // serpentent (aller-retour) le long du champ.
        private static readonly IntVec3[] Cardinaux =
            { IntVec3.East, IntVec3.West, IntVec3.North, IntVec3.South };

        // Direction d'un nouveau sillon depuis 'depart' : le premier cardinal
        // dont la case voisine se laboure encore. Est par défaut.
        private static IntVec3 FurrowDirection(IntVec3 start, Map map, MapComponent_Labour component)
        {
            foreach (IntVec3 d in Cardinaux)
            {
                if (Plowable(start + d, map, component))
                {
                    return d;
                }
            }
            return IntVec3.East;
        }

        // La case se laboure-t-elle, dans une zone de culture où le labour est
        // autorisé ? Sert au suivi de sillon, case par case, et au JobDriver
        // pour abandonner une case qui ne s'y prête plus en cours d'ouvrage.
        public static bool Plowable(IntVec3 cell, Map map, MapComponent_Labour component)
        {
            return cell.InBounds(map)
                && cell.GetZone(map) is Zone_Growing growZone
                && component.PlowingAllowed(growZone)
                && CellIsPlowable(cell, map);
        }

        // Y a-t-il une case à labourer quelque part (réservations mises à
        // part) ? Sert aussi à décider de poser ou de prendre la charrue.
        // Toujours via le cache de MapComponent_Labour, jamais en direct.
        public static bool WorkExists(Map map, MapComponent_Labour component)
        {
            foreach (Zone zone in map.zoneManager.AllZones)
            {
                if (!(zone is Zone_Growing growZone) || !component.PlowingAllowed(growZone))
                {
                    continue;
                }
                foreach (IntVec3 cell in growZone.Cells)
                {
                    if (CellIsPlowable(cell, map))
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
        public static IntVec3 ClosestPlowableCell(Pawn beast, Pawn handler = null)
        {
            Map map = beast.Map;
            MapComponent_Labour component = MapComponent_Labour.Of(map);
            IntVec3 best = IntVec3.Invalid;
            float bestDist = float.MaxValue;
            foreach (Zone zone in map.zoneManager.AllZones)
            {
                if (!(zone is Zone_Growing growZone) || !component.PlowingAllowed(growZone))
                {
                    continue;
                }
                foreach (IntVec3 cell in growZone.Cells)
                {
                    float dist = cell.DistanceToSquared(beast.Position);
                    if (dist >= bestDist || !CellIsPlowable(cell, map))
                    {
                        continue;
                    }
                    if (ServiceTrait.Reachable(beast, handler, cell))
                    {
                        best = cell;
                        bestDist = dist;
                    }
                }
            }
            return best;
        }

        // Case vers laquelle le colon mène la bête : la plus proche d'elle et
        // joignable en la menant. Elle re-scanne ensuite depuis là.
        public static bool FindWorkCell(Pawn beast, Pawn handler, out IntVec3 result)
        {
            result = ClosestPlowableCell(beast, handler);
            return result.IsValid;
        }

        private static bool CellIsPlowable(IntVec3 cell, Map map)
        {
            TerrainDef terrain = cell.GetTerrain(map);
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
            if (GenTemperature.GetTemperatureForCell(cell, map) < TemperatureMin)
            {
                return false;
            }
            return cell.GetEdifice(map) == null;
        }
    }
}
