using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Cerveau de la bête déneigeuse, dans l'arbre de pensée. Ne produit un job
    // que si elle est en service (menée sur zone par un colon) et déjà équipée.
    // Elle racle la case enneigée la plus proche d'elle, de proche en proche,
    // dans la zone de déneigement vanilla. C'est le pendant hivernal du labour,
    // qui s'arrête justement quand le sol gèle.
    public class JobGiver_Deneigeur : ThinkNode_JobGiver
    {
        // Épaisseur en deçà de laquelle une case est considérée dégagée
        // (même seuil que le déneigement des colons vanilla).
        public const float MinSnow = 0.2f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            // La bête ne racle qu'une fois menée sur zone par un colon (en
            // service) et déjà équipée du harnais et du grattoir.
            if (!ServiceTrait.ReadyForWork(pawn, TacheTrait.Deneigement, out MapComponent_Labour component)
                || !component.SnowWorkPending())
            {
                return null;
            }

            IntVec3 target = ClosestSnowyCell(pawn);
            if (!target.IsValid)
            {
                return null;
            }
            return JobMaker.MakeJob(AAW_DefOf.AAW_Deneiger, target);
        }

        public static bool CellIsSnowy(IntVec3 cell, Map map)
        {
            return map.snowGrid.GetDepth(cell) >= MinSnow;
        }

        // Case enneigée la plus proche de la bête, dans les deux usages :
        //   meneur == null → la bête racle seule, elle doit pouvoir réserver.
        //   meneur != null → un colon l'y mène à la corde (voir ServiceTrait).
        // Test coûteux (atteignabilité) en dernier, seulement pour une case plus
        // proche que la meilleure trouvée.
        public static IntVec3 ClosestSnowyCell(Pawn beast, Pawn handler = null)
        {
            Map map = beast.Map;
            IntVec3 best = IntVec3.Invalid;
            float bestDist = float.MaxValue;
            foreach (IntVec3 cell in map.areaManager.SnowOrSandClear.ActiveCells)
            {
                float dist = cell.DistanceToSquared(beast.Position);
                if (dist >= bestDist || !CellIsSnowy(cell, map))
                {
                    continue;
                }
                if (ServiceTrait.Reachable(beast, handler, cell))
                {
                    best = cell;
                    bestDist = dist;
                }
            }
            return best;
        }

        // Case vers laquelle le colon mène la bête : la plus proche d'elle et
        // joignable en la menant. Elle re-scanne ensuite depuis là.
        public static bool FindWorkCell(Pawn beast, Pawn handler, out IntVec3 result)
        {
            result = ClosestSnowyCell(beast, handler);
            return result.IsValid;
        }

        // Y a-t-il de la neige à racler quelque part dans la zone de
        // déneigement ? Sert aussi à décider de poser ou de prendre l'outil.
        // Toujours via le cache de MapComponent_Labour, jamais en direct.
        public static bool WorkExists(Map map)
        {
            foreach (IntVec3 cell in map.areaManager.SnowOrSandClear.ActiveCells)
            {
                if (CellIsSnowy(cell, map))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
