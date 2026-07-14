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
        private const int DureeChargementTicks = 60;
        private const int PilesParCharrette = 100; // usure : ~100 piles hissées
        private const int UsuresParHarnais = 200;  // le harnais fatigue aussi, plus lentement

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            pawn.ReserveAsManyAsPossible(job.GetTargetQueue(TargetIndex.A), job);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil extraire = Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A);
            yield return extraire;

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.A);

            Toil hisser = Toils_General.Wait(DureeChargementTicks);
            hisser.WithProgressBarToilDelay(TargetIndex.A);
            yield return hisser;

            yield return Toils_General.Do(delegate
            {
                Thing pile = job.targetA.Thing;
                // Re-borne au cas où la situation a changé depuis la file :
                // pile entamée par un colon, cargaison déjà à bord, etc.
                int n = Mathf.Min(pile.stackCount, job.count > 0 ? job.count : pile.stackCount);
                float unitaire = pile.GetStatValue(StatDefOf.Mass);
                if (unitaire > 0f)
                {
                    float masseLibre = EquipementUtility.CapaciteCharrette - EquipementUtility.MasseCargaison(pawn);
                    n = Mathf.Min(n, Mathf.FloorToInt(masseLibre / unitaire));
                }
                if (n <= 0)
                {
                    return;
                }
                Thing pris = pile.SplitOff(n);
                if (pris.Spawned)
                {
                    pris.DeSpawn();
                }
                if (!pawn.inventory.innerContainer.TryAdd(pris, false))
                {
                    GenPlace.TryPlaceThing(pris, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    return;
                }
                // Chaque pile hissée use la charrette, et un peu le harnais.
                EquipementUtility.User(pawn, AAW_DefOf.AAW_Charrette, PilesParCharrette, "AAW_CharretteRompue");
                EquipementUtility.User(pawn, AAW_DefOf.AAW_HarnaisDeTrait, UsuresParHarnais, "AAW_HarnaisRompu");
            });

            yield return Toils_Jump.JumpIf(extraire, () => !job.targetQueueA.NullOrEmpty());
        }
    }
}
