using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Un colon prend la bête de trait à la corde et la mène jusqu'à une case.
    // Un même pilote sert les deux sens, selon la JobDef :
    //   AAW_MenerAuTravail  → mène au champ, puis DÉBUT de service (elle bosse)
    //   AAW_RamenerAEnclos  → mène à l'enclos, puis FIN de service (elle reste)
    // La corde est lâchée à l'arrivée ; la bête suit toute seule en chemin
    // (comportement vanilla des animaux encordés).
    //   targetA = la bête    targetB = la case destination
    public class JobDriver_Mener : JobDriver
    {
        private Pawn Bete => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            pawn.Map.pawnDestinationReservationManager.Reserve(
                pawn, job, job.GetTarget(TargetIndex.B).Cell);
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            // Quoi qu'il arrive, on ne laisse pas la bête encordée dans le vide.
            AddFinishAction(delegate
            {
                pawn?.roping?.DropRopes();
            });

            yield return Toils_Rope.GotoRopeAttachmentInteractionCell(TargetIndex.A);
            yield return Toils_Rope.RopePawn(TargetIndex.A);

            // Corde perdue en chemin (bête paniquée, downed…) : le job échoue,
            // le WorkGiver reprendra la bête au prochain scan.
            Toil mener = Toils_Goto.Goto(TargetIndex.B, PathEndMode.OnCell);
            mener.FailOn(() => !pawn.roping.IsRopingOthers);
            yield return mener;

            yield return Toils_General.Do(delegate
            {
                Pawn bete = Bete;
                MapComponent_Labour composante = pawn.Map.GetComponent<MapComponent_Labour>();
                if (job.def == AAW_DefOf.AAW_RamenerAEnclos)
                {
                    composante.FinService(bete);
                    // Ramenée parce qu'elle n'est plus une bête de trait : lui
                    // retirer l'équipement, qui sera rangé au râtelier.
                    if (!composante.EstBeteDeTrait(bete))
                    {
                        EquipementUtility.ToutDeposer(bete);
                    }
                }
                else
                {
                    composante.DebutService(bete);
                }
                pawn.roping.DropRope(bete);
            });
        }
    }
}
