using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Un colon porte une pièce d'équipement (harnais, charrue, charrette ou
    // grattoir) jusqu'à la bête de trait et la lui pose sur le dos : la pièce
    // rejoint l'inventaire de la bête (onglet Équipement). Si la bête portait
    // déjà un autre attelage (changement de saison), le colon le retire d'abord
    // pour le laisser au sol, et un autre colon le rangera au râtelier.
    //   targetA = la bête    targetB = la pile d'équipement au sol
    public class JobDriver_ColonEquiper : JobDriver
    {
        private const int PlacingDurationTicks = 180;

        private Pawn Beast => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed)
                && pawn.Reserve(job.GetTarget(TargetIndex.B), job, 1, 1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil towardStack = Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);
            towardStack.FailOnDespawnedNullOrForbidden(TargetIndex.B);
            yield return towardStack;

            yield return Toils_Haul.StartCarryThing(TargetIndex.B);

            Toil towardBeast = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            towardBeast.FailOnDespawnedOrNull(TargetIndex.A);
            yield return towardBeast;

            Toil place = Toils_General.Wait(PlacingDurationTicks);
            place.FailOnDespawnedOrNull(TargetIndex.A);
            place.WithProgressBarToilDelay(TargetIndex.A);
            yield return place;

            yield return Toils_General.Do(delegate
            {
                Pawn beast = Beast;
                Thing carried = pawn.carryTracker.CarriedThing;
                if (beast == null || carried == null)
                {
                    return;
                }
                Thing piece = carried.SplitOff(1);
                // Pose d'un attelage : retirer l'ancien s'il diffère.
                if (EquipementUtility.IsImplement(piece.def))
                {
                    Thing previous = EquipementUtility.CarriedImplement(beast);
                    if (previous != null && previous.def != piece.def)
                    {
                        // La cargaison vit dans la charrette : elle descend avec
                        // elle, sinon elle voyagerait invisible sous la charrue.
                        if (previous.def == AAW_DefOf.AAW_Charrette)
                        {
                            EquipementUtility.DropCargo(beast);
                        }
                        beast.inventory.innerContainer.TryDrop(
                            previous, beast.Position, beast.Map, ThingPlaceMode.Near, out _);
                    }
                }
                if (!beast.inventory.innerContainer.TryAdd(piece, false))
                {
                    GenPlace.TryPlaceThing(piece, beast.Position, beast.Map, ThingPlaceMode.Near);
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
