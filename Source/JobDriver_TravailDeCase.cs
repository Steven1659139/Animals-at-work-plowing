using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Tronc commun du labour et du déneigement : aller sur la case, y
    // travailler le temps que dicte le gabarit de la bête (barre de
    // progression, effet et son vanilla), abandonner si la case ne s'y prête
    // plus, puis agir sur la case et user l'outil et le harnais. Les
    // sous-classes ne disent que ce qui change d'un travail à l'autre.
    public abstract class JobDriver_TravailDeCase : JobDriver
    {
        // Durée d'une case pour le gabarit bovin (bodySize 2.4) ; la courbe des
        // gabarits vit dans BeteDeTrait.DurationFactor.
        protected abstract int BaseDurationTicks { get; }

        // Effet visuel et son vanilla du travail (voir Ambiance).
        protected abstract string Effect { get; }
        protected abstract string Sound { get; }

        // L'outil que le travail use, le nombre de cases qu'en tire un
        // exemplaire du matériau ordinaire, et le message quand il casse.
        protected abstract ThingDef Tool { get; }
        protected abstract int CellsPerTool { get; }
        protected abstract string BrokenToolKey { get; }

        protected IntVec3 Cell => job.targetA.Cell;

        // La case se prête-t-elle encore au travail ? Vérifié à chaque tick
        // pendant l'ouvrage.
        protected abstract bool CellStillValid(MapComponent_Labour component);

        // Ce que le travail fait de la case, une fois le temps écoulé.
        protected abstract void DoWork(MapComponent_Labour component);

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            int duration = Mathf.RoundToInt(BaseDurationTicks * BeteDeTrait.DurationFactor(pawn));
            Toil work = Toils_General.Wait(duration);
            work.WithProgressBarToilDelay(TargetIndex.A);
            Ambiance.Dress(work, TargetIndex.A, Effect, Sound);
            work.FailOn(() => !CellStillValid(MapComponent_Labour.Of(pawn.Map)));
            yield return work;

            yield return Toils_General.Do(delegate
            {
                DoWork(MapComponent_Labour.Of(pawn.Map));
                // Chaque case use l'outil, et un peu le harnais ; brisés, un
                // colon rééquipera la bête.
                EquipementUtility.Wear(pawn, Tool, CellsPerTool, BrokenToolKey);
                EquipementUtility.Wear(pawn, AAW_DefOf.AAW_HarnaisDeTrait,
                    EquipementUtility.UsesPerHarness, "AAW_HarnaisRompu");
            });
        }
    }
}
