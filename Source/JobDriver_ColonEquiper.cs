using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Un colon porte une pièce d'équipement (harnais, charrue, charrette ou
    // grattoir) jusqu'à la bête de trait et la lui pose sur le dos : la pièce
    // rejoint l'inventaire de la bête (onglet Équipement). Si la bête portait
    // déjà un autre attelage (changement de saison), le colon le retire d'abord
    // pour le laisser au sol — un autre colon le rangera au râtelier.
    //   targetA = la bête    targetB = la pile d'équipement au sol
    public class JobDriver_ColonEquiper : JobDriver
    {
        private const int DureePoseTicks = 180;

        private Pawn Bete => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed)
                && pawn.Reserve(job.GetTarget(TargetIndex.B), job, 1, 1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil versPile = Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);
            versPile.FailOnDespawnedNullOrForbidden(TargetIndex.B);
            yield return versPile;

            yield return Toils_Haul.StartCarryThing(TargetIndex.B);

            Toil versBete = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            versBete.FailOnDespawnedOrNull(TargetIndex.A);
            yield return versBete;

            Toil poser = Toils_General.Wait(DureePoseTicks);
            poser.FailOnDespawnedOrNull(TargetIndex.A);
            poser.WithProgressBarToilDelay(TargetIndex.A);
            yield return poser;

            yield return Toils_General.Do(delegate
            {
                Pawn bete = Bete;
                Thing porte = pawn.carryTracker.CarriedThing;
                if (bete == null || porte == null)
                {
                    return;
                }
                Thing piece = porte.SplitOff(1);
                // Pose d'un attelage : retirer l'ancien s'il diffère.
                if (EquipementUtility.EstAttelage(piece.def))
                {
                    Thing ancien = EquipementUtility.AttelagePorte(bete);
                    if (ancien != null && ancien.def != piece.def)
                    {
                        bete.inventory.innerContainer.TryDrop(
                            ancien, bete.Position, bete.Map, ThingPlaceMode.Near, out _);
                    }
                }
                if (!bete.inventory.innerContainer.TryAdd(piece, false))
                {
                    GenPlace.TryPlaceThing(piece, bete.Position, bete.Map, ThingPlaceMode.Near);
                }
                // Reliquat éventuel dans les mains du colon : le reposer au sol.
                if (pawn.carryTracker.CarriedThing != null)
                {
                    pawn.carryTracker.TryDropCarriedThing(
                        pawn.Position, ThingPlaceMode.Near, out _);
                }
            });
        }
    }
}
