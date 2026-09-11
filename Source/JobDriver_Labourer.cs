using Verse;

namespace AnimalsAtWork.Plowing
{
    // Labour d'une case : le temps écoulé, le terrain devient du sol labouré
    // et la composante note la case pour la rendre à son terrain d'origine en
    // fin de saison.
    public class JobDriver_Labourer : JobDriver_TravailDeCase
    {
        // L'âne peine, l'éléphant expédie.
        protected override int DureeBaseTicks => 400;

        // Terre grattée et souffle de la bête, façon semailles vanilla.
        protected override string Effet => "Sow";
        protected override string Son => "Interact_Sow";

        // Usure du soc : une charrue de bois tient 200 cases.
        protected override ThingDef Outil => AAW_DefOf.AAW_Charrue;
        protected override int CasesParOutil => 200;
        protected override string CleOutilRompu => "AAW_CharrueRompue";

        // Abandonne si la case ne se laboure plus : déjà retournée par une
        // autre bête, zone supprimée ou labour coupé dessus, sol gelé,
        // bâtiment posé entre-temps.
        protected override bool CelluleValide(MapComponent_Labour composante)
        {
            return JobGiver_Labourer.Labourable(Cellule, pawn.Map, composante);
        }

        protected override void Travailler(MapComponent_Labour composante)
        {
            TerrainDef terrainAvant = Cellule.GetTerrain(pawn.Map);
            pawn.Map.terrainGrid.SetTerrain(Cellule, AAW_DefOf.AAW_SolLaboure);
            composante.EnregistrerLabour(Cellule, terrainAvant);
        }
    }
}
