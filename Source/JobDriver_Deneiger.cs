using Verse;

namespace AnimalsAtWork.Plowing
{
    // Déneigement d'une case : le temps écoulé, l'épaisseur de neige tombe à
    // zéro.
    public class JobDriver_Deneiger : JobDriver_TravailDeCase
    {
        // Plus léger que le labour : on racle, on ne retourne pas la terre.
        protected override int BaseDurationTicks => 250;

        // Éclats de neige du déneigement vanilla et bruit de balayage.
        protected override string Effect => "ClearSnow";
        protected override string Sound => "Interact_CleanFilth";

        // Usure de la lame : un grattoir de bois tient 400 cases.
        protected override ThingDef Tool => AAW_DefOf.AAW_Grattoir;
        protected override int CellsPerTool => 400;
        protected override string BrokenToolKey => "AAW_GrattoirRompu";

        // Abandonne si la case s'est dégagée entre-temps (fonte, autre bête).
        protected override bool CellStillValid(MapComponent_Labour component)
        {
            return JobGiver_Deneigeur.CellIsSnowy(Cell, pawn.Map);
        }

        protected override void DoWork(MapComponent_Labour component)
        {
            pawn.Map.snowGrid.SetDepth(Cell, 0f);
        }
    }
}
