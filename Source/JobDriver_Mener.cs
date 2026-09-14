using RimWorld;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Un colon prend la bête de trait à la corde et la mène jusqu'à une case.
    // Un même pilote sert les deux sens, selon la JobDef :
    //   AAW_MenerAuTravail  → mène au champ, puis DÉBUT de service (elle bosse)
    //   AAW_RamenerAEnclos  → mène à l'enclos, puis FIN de service (elle reste)
    //   targetA = la bête    targetB = la case destination
    //
    // Hérite du pilote de roping vanilla, et non de JobDriver directement :
    // Pawn_RopeTracker.RopingTick() rompt toutes les cordes du colon dès que
    // son pilote n'est pas un JobDriver_RopeToDestination
    // (IsStillDoingRopingJob), et le job échouerait au tick suivant
    // l'encordage, pour être réémis aussitôt par le WorkGiver.
    // La classe de base fournit tout le trajet (aller encorder, mener au rythme
    // de la bête, lâcher à l'arrivée) ; il ne reste qu'à dire quand la bête est
    // arrivée et ce qu'on fait d'elle à ce moment-là.
    public class JobDriver_Mener : JobDriver_RopeToDestination
    {
        // Marge d'arrivée : la bête traîne au bout de la corde (8 cases chez
        // vanilla), inutile d'exiger la case exacte pour la lâcher au travail.
        private const float ArrivalProximity = 3f;

        private IntVec3 Destination => job.GetTarget(TargetIndex.B).Cell;

        protected override bool HasRopeeArrived(Pawn ropee, bool roperWaitingAtDest)
        {
            // Retour : c'est la BÊTE qui doit être dans l'enclos, pas le colon :
            // il arrive sur sa case avant elle, et la lâcher à ce moment-là la
            // laisserait devant le portail. Même test que JobDriver_RopeToPen, via
            // le marqueur d'enclos plutôt que AnimalPenUtility.GetCurrentPenOf :
            // celui-ci s'ouvre sur « if (!animal.Roamer) return null », et le
            // patch de service force justement Roamer à false (Patch_Service).
            if (job.def == AAW_DefOf.AAW_RamenerAEnclos)
            {
                CompAnimalPenMarker marker =
                    job.GetTarget(TargetIndex.C).Thing?.TryGetComp<CompAnimalPenMarker>();
                if (marker == null)
                {
                    return roperWaitingAtDest; // enclos disparu en route
                }
                PenMarkerState state = marker.PenState;
                return !state.Enclosed || state.ContainsConnectedRegion(ropee.GetRegion());
            }
            // Aller : trois conditions, chacune rattrapant l'angle mort des
            // autres.
            //   – la distance dit qu'elle est ARRIVÉE, mais traverse les
            //     clôtures du regard ;
            //   – l'atteignabilité dit qu'elle n'est pas COINCÉE derrière un
            //     obstacle, mais est vraie dès avant le départ ;
            //   – l'enclos dit qu'elle est DU BON CÔTÉ, ce qu'aucune des deux
            //     autres ne voit : Building_Door laisse passer un animal tenu à
            //     la corde (« IsRopedByPawn »), donc tant que le colon la tient,
            //     CanReach répond vrai depuis l'intérieur de l'enclos. Lâchée
            //     là, elle redevient prisonnière du portail.
            return ropee.Position.InHorDistOf(Destination, ArrivalProximity)
                && SamePen(ropee)
                && ropee.CanReach(Destination, PathEndMode.OnCell, Danger.Some);
        }

        // La bête et la case visée sont-elles du même côté de toute clôture ?
        // Test symétrique : il vaut pour la bête qu'on fait sortir d'un enclos
        // comme pour celle qu'on fait entrer dans un champ enclos.
        private bool SamePen(Pawn ropee)
        {
            Map map = ropee.Map;
            Region targetRegion = Destination.GetRegion(map);
            if (targetRegion == null)
            {
                return true;
            }
            // Même enclos, ou aucun des deux : l'une dedans et l'autre dehors,
            // ce n'est pas encore le moment de lâcher.
            return ServiceTrait.PenContaining(map, targetRegion)
                == ServiceTrait.PenContaining(map, ropee.Position.GetRegion(map));
        }

        // La corde vient d'être lâchée par la classe de base : on bascule l'état
        // de service, ce qui rend la bête autonome au champ ou la rend à l'enclos.
        protected override void ProcessArrivedRopee(Pawn ropee)
        {
            MapComponent_Labour component = MapComponent_Labour.Of(pawn.Map);
            if (job.def == AAW_DefOf.AAW_RamenerAEnclos)
            {
                component.EndService(ropee);
                // Elle reste attelée : rien ne l'empêcherait de repartir à la
                // seconde où du travail réapparaît. On note l'heure du retour.
                component.NoteReturn(ropee);
                // Ramenée parce qu'elle n'est plus une bête de trait : lui
                // retirer l'équipement, qui sera rangé au râtelier.
                if (!component.IsDraftBeast(ropee))
                {
                    EquipementUtility.DropAll(ropee);
                }
            }
            else
            {
                component.BeginService(ropee);
            }
            // Juste avant de nous appeler, la classe de base coupe le job
            // « suivre le meneur » de la bête, et EndCurrentJob lui en fait
            // aussitôt chercher un autre, alors que l'état de service n'était
            // pas encore posé. Nos JobGiver refusaient donc, la bête partait
            // vagabonder, et ne revenait travailler qu'une fois sa promenade
            // finie. On la refait décider maintenant que son état est à jour.
            if (ropee.jobs != null && ropee.CurJob != null)
            {
                ropee.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }

        // Pas de ramassage opportuniste en chemin : chaque bête a sa tâche et sa
        // destination, le WorkGiver les prend une par une.
        protected override bool ShouldOpportunisticallyRopeAnimal(Pawn animal)
        {
            return false;
        }
    }
}
