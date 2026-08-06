using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Séquence du labour : aller sur la case, travailler (barre de progression),
    // puis convertir le terrain en sol labouré.
    public class JobDriver_Labourer : JobDriver
    {
        // Durée pour un gabarit bovin (bodySize 2.4) : l'âne peine, l'éléphant
        // expédie. La courbe des gabarits vit dans BeteDeTrait.FacteurDuree.
        private const int DureeLabourBaseTicks = 400;
        private const int CasesParCharrue = 200;   // usure du soc : une charrue de bois = 200 cases

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            int duree = Mathf.RoundToInt(DureeLabourBaseTicks * BeteDeTrait.FacteurDuree(pawn));
            Toil labour = Toils_General.Wait(duree);
            labour.WithProgressBarToilDelay(TargetIndex.A);
            // Terre grattée et souffle de la bête, façon semailles vanilla.
            Ambiance.Habiller(labour, TargetIndex.A, "Sow", "Interact_Sow");
            // Abandonne si la case a été labourée entre-temps (autre bête, etc.)
            labour.FailOn(() => job.targetA.Cell.GetTerrain(pawn.Map) == AAW_DefOf.AAW_SolLaboure);
            yield return labour;

            yield return Toils_General.Do(delegate
            {
                IntVec3 cellule = job.targetA.Cell;
                TerrainDef terrainAvant = cellule.GetTerrain(pawn.Map);
                pawn.Map.terrainGrid.SetTerrain(cellule, AAW_DefOf.AAW_SolLaboure);
                pawn.Map.GetComponent<MapComponent_Labour>().EnregistrerLabour(cellule, terrainAvant);

                // Le sillon use la charrue, et un peu le harnais ; brisés, la
                // bête ira s'équiper à neuf.
                EquipementUtility.User(pawn, AAW_DefOf.AAW_Charrue, CasesParCharrue, "AAW_CharrueRompue");
                EquipementUtility.User(pawn, AAW_DefOf.AAW_HarnaisDeTrait, EquipementUtility.UsagesParHarnais, "AAW_HarnaisRompu");
            });
        }
    }
}
