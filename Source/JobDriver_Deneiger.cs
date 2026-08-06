using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Séquence du déneigement : aller sur la case, racler (barre de
    // progression), puis mettre l'épaisseur de neige à zéro.
    public class JobDriver_Deneiger : JobDriver
    {
        // Durée pour un gabarit bovin (bodySize 2.4), plus légère que le
        // labour : on racle, on ne retourne pas la terre.
        private const int DureeRaclageBaseTicks = 250;
        private const int CasesParGrattoir = 100;  // usure de la lame : un grattoir neuf = 100 cases
        private const int UsuresParHarnais = 200;  // le harnais fatigue aussi, bien plus lentement

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            int duree = Mathf.RoundToInt(DureeRaclageBaseTicks * BeteDeTrait.FacteurDuree(pawn));
            Toil raclage = Toils_General.Wait(duree);
            raclage.WithProgressBarToilDelay(TargetIndex.A);
            // Éclats de neige du déneigement vanilla et bruit de balayage.
            Ambiance.Habiller(raclage, TargetIndex.A, "ClearSnow", "Interact_CleanFilth");
            // Abandonne si la case s'est dégagée entre-temps (fonte, autre bête...)
            raclage.FailOn(() => !JobGiver_Deneigeur.CelluleEnneigee(job.targetA.Cell, pawn.Map));
            yield return raclage;

            yield return Toils_General.Do(delegate
            {
                pawn.Map.snowGrid.SetDepth(job.targetA.Cell, 0f);

                // Le raclage use la lame, et un peu le harnais ; brisés, la
                // bête ira s'équiper à neuf.
                EquipementUtility.User(pawn, AAW_DefOf.AAW_Grattoir, CasesParGrattoir, "AAW_GrattoirRompu");
                EquipementUtility.User(pawn, AAW_DefOf.AAW_HarnaisDeTrait, UsuresParHarnais, "AAW_HarnaisRompu");
            });
        }
    }
}
