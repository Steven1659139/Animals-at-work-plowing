using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // L'animal va jusqu'au harnais stocké et l'enfile : l'objet rejoint son
    // inventaire (onglet Équipement), le suit partout et tombe à sa mort.
    public class JobDriver_Harnacher : JobDriver
    {
        private const int DureeHarnachementTicks = 180;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil ajuster = Toils_General.Wait(DureeHarnachementTicks);
            ajuster.WithProgressBarToilDelay(TargetIndex.A);
            yield return ajuster;

            yield return Toils_General.Do(delegate
            {
                Thing pile = job.targetA.Thing;
                IntVec3 position = pile.Position;
                Map map = pawn.Map;
                Thing harnais = pile.SplitOff(1);
                if (harnais.Spawned)
                {
                    harnais.DeSpawn();
                }
                if (!pawn.inventory.innerContainer.TryAdd(harnais, false))
                {
                    GenPlace.TryPlaceThing(harnais, position, map, ThingPlaceMode.Near);
                }
            });
        }
    }
}
