using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // L'animal va jusqu'à la charrette stockée et s'y attelle : l'objet
    // rejoint son inventaire (onglet Équipement), est dessiné derrière lui
    // (MapComponent_Charrettes) et tombe à sa mort.
    public class JobDriver_Atteler : JobDriver
    {
        private const int DureeAttelageTicks = 240;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil ajuster = Toils_General.Wait(DureeAttelageTicks);
            ajuster.WithProgressBarToilDelay(TargetIndex.A);
            yield return ajuster;

            yield return Toils_General.Do(delegate
            {
                Thing pile = job.targetA.Thing;
                IntVec3 position = pile.Position;
                Map map = pawn.Map;
                Thing charrette = pile.SplitOff(1);
                if (charrette.Spawned)
                {
                    charrette.DeSpawn();
                }
                if (!pawn.inventory.innerContainer.TryAdd(charrette, false))
                {
                    GenPlace.TryPlaceThing(charrette, position, map, ThingPlaceMode.Near);
                }
            });
        }
    }
}
