using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Tournée de ramassage : la bête enchaîne les piles mises en file par le
    // charretier et les hisse dans sa charrette (inventaire). La livraison
    // suit dans un job séparé (JobDriver_ViderCharrette).
    public class JobDriver_ChargerCharrette : JobDriver
    {
        private const int LoadingDurationTicks = 60;
        private const int StacksPerCart = 400; // usure : ~400 piles hissées

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            pawn.ReserveAsManyAsPossible(job.GetTargetQueue(TargetIndex.A), job);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil extract = Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A);
            yield return extract;

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.A);

            Toil hoist = Toils_General.Wait(LoadingDurationTicks);
            hoist.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            hoist.WithProgressBarToilDelay(TargetIndex.A);
            yield return hoist;

            yield return Toils_General.Do(delegate
            {
                Thing stack = job.targetA.Thing;
                // Une pile détruite pendant le hissage (feu, explosion) n'a plus
                // rien à donner : sans ce test, SplitOff en tirerait une copie.
                if (stack == null || !stack.Spawned)
                {
                    return;
                }
                // Re-borne au cas où la situation a changé depuis la file :
                // pile entamée par un colon, cargaison déjà à bord, etc.
                int n = Mathf.Min(stack.stackCount, job.count > 0 ? job.count : stack.stackCount);
                float perUnit = stack.GetStatValue(StatDefOf.Mass);
                if (perUnit > 0f)
                {
                    n = Mathf.Min(n, Mathf.FloorToInt(EquipementUtility.FreeMass(pawn) / perUnit));
                }
                if (n <= 0)
                {
                    return;
                }
                Thing taken = stack.SplitOff(n);
                if (taken.Spawned)
                {
                    taken.DeSpawn();
                }
                if (!pawn.inventory.innerContainer.TryAdd(taken, false))
                {
                    GenPlace.TryPlaceThing(taken, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    return;
                }
                // Chaque pile hissée use la charrette, et un peu le harnais.
                EquipementUtility.Wear(pawn, AAW_DefOf.AAW_Charrette, StacksPerCart, "AAW_CharretteRompue");
                EquipementUtility.Wear(pawn, AAW_DefOf.AAW_HarnaisDeTrait, EquipementUtility.UsesPerHarness, "AAW_HarnaisRompu");
            });

            yield return Toils_Jump.JumpIf(extract, () => !job.targetQueueA.NullOrEmpty());
        }
    }
}
