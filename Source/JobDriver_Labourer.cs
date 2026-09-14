using Verse;

namespace AnimalsAtWork.Plowing
{
    // Labour d'une case : le temps écoulé, le terrain devient du sol labouré
    // et la composante note la case pour la rendre à son terrain d'origine en
    // fin de saison.
    public class JobDriver_Labourer : JobDriver_TravailDeCase
    {
        // L'âne peine, l'éléphant expédie.
        protected override int BaseDurationTicks => 400;

        // Terre grattée et souffle de la bête, façon semailles vanilla.
        protected override string Effect => "Sow";
        protected override string Sound => "Interact_Sow";

        // Usure du soc : une charrue de bois tient 200 cases.
        protected override ThingDef Tool => AAW_DefOf.AAW_Charrue;
        protected override int CellsPerTool => 200;
        protected override string BrokenToolKey => "AAW_CharrueRompue";

        // Abandonne si la case ne se laboure plus : déjà retournée par une
        // autre bête, zone supprimée ou labour coupé dessus, sol gelé,
        // bâtiment posé entre-temps.
        protected override bool CellStillValid(MapComponent_Labour component)
        {
            return JobGiver_Labourer.Plowable(Cell, pawn.Map, component);
        }

        protected override void DoWork(MapComponent_Labour component)
        {
            TerrainDef terrainBefore = Cell.GetTerrain(pawn.Map);
            pawn.Map.terrainGrid.SetTerrain(Cell, AAW_DefOf.AAW_SolLaboure);
            component.RecordPlowing(Cell, terrainBefore);
        }
    }
}
