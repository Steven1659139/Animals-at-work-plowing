using RimWorld;
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
        public const float NeigeMin = 0.2f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            Map map = pawn.Map;
            if (map == null || pawn.Faction != Faction.OfPlayer)
            {
                return null;
            }
            if (!BeteDeTrait.Est(pawn.def))
            {
                return null;
            }
            if (!AAW_DefOf.AAW_Harnachement.IsFinished)
            {
                return null;
            }

            MapComponent_Labour composante = map.GetComponent<MapComponent_Labour>();
            // La bête ne racle qu'une fois menée sur zone par un colon (en
            // service) et déjà équipée du harnais et du grattoir : elle ne
            // s'attelle ni ne sort de l'enclos seule.
            if (!composante.EstEnService(pawn)
                || !composante.TacheAutorisee(pawn, TacheTrait.Deneigement)
                || EquipementUtility.Porte(pawn, AAW_DefOf.AAW_HarnaisDeTrait) == null
                || EquipementUtility.Porte(pawn, AAW_DefOf.AAW_Grattoir) == null)
            {
                return null;
            }
            if (!composante.TravailDeneigementEnAttente())
            {
                return null;
            }

            IntVec3 cible = CaseEnneigeeLaPlusProche(pawn);
            if (!cible.IsValid)
            {
                return null;
            }
            return JobMaker.MakeJob(AAW_DefOf.AAW_Deneiger, cible);
        }

        public static bool CelluleEnneigee(IntVec3 cellule, Map map)
        {
            return map.snowGrid.GetDepth(cellule) >= NeigeMin;
        }

        // Case enneigée la plus proche de la bête, dans les deux usages :
        //   meneur == null → la bête racle seule, elle doit pouvoir réserver.
        //   meneur != null → un colon l'y mène à la corde (voir ServiceTrait).
        // Test coûteux (atteignabilité) en dernier, seulement pour une case plus
        // proche que la meilleure trouvée.
        public static IntVec3 CaseEnneigeeLaPlusProche(Pawn bete, Pawn meneur = null)
        {
            Map map = bete.Map;
            IntVec3 meilleure = IntVec3.Invalid;
            float meilleureDist = float.MaxValue;
            foreach (IntVec3 cellule in map.areaManager.SnowOrSandClear.ActiveCells)
            {
                float dist = cellule.DistanceToSquared(bete.Position);
                if (dist >= meilleureDist || !CelluleEnneigee(cellule, map))
                {
                    continue;
                }
                bool accessible = meneur == null
                    ? bete.CanReserveAndReach(cellule, PathEndMode.OnCell, Danger.Some)
                    : ServiceTrait.MeneurPeutYMener(meneur, bete, cellule);
                if (accessible)
                {
                    meilleure = cellule;
                    meilleureDist = dist;
                }
            }
            return meilleure;
        }

        // Case vers laquelle le colon mène la bête : la plus proche d'elle et
        // joignable en la menant. Elle re-scanne ensuite depuis là.
        public static bool TrouverCelluleTravail(Pawn bete, Pawn meneur, out IntVec3 result)
        {
            result = CaseEnneigeeLaPlusProche(bete, meneur);
            return result.IsValid;
        }

        // Y a-t-il de la neige à racler quelque part dans la zone de
        // déneigement ? Sert aussi à décider de poser ou de prendre l'outil.
        // Toujours via le cache de MapComponent_Labour, jamais en direct.
        public static bool TravailExiste(Map map)
        {
            foreach (IntVec3 cellule in map.areaManager.SnowOrSandClear.ActiveCells)
            {
                if (CelluleEnneigee(cellule, map))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
