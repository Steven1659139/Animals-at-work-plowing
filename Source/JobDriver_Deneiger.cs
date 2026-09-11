using Verse;

namespace AnimalsAtWork.Plowing
{
    // Déneigement d'une case : le temps écoulé, l'épaisseur de neige tombe à
    // zéro.
    public class JobDriver_Deneiger : JobDriver_TravailDeCase
    {
        // Plus léger que le labour : on racle, on ne retourne pas la terre.
        protected override int DureeBaseTicks => 250;

        // Éclats de neige du déneigement vanilla et bruit de balayage.
        protected override string Effet => "ClearSnow";
        protected override string Son => "Interact_CleanFilth";

        // Usure de la lame : un grattoir de bois tient 400 cases.
        protected override ThingDef Outil => AAW_DefOf.AAW_Grattoir;
        protected override int CasesParOutil => 400;
        protected override string CleOutilRompu => "AAW_GrattoirRompu";

        // Abandonne si la case s'est dégagée entre-temps (fonte, autre bête).
        protected override bool CelluleValide(MapComponent_Labour composante)
        {
            return JobGiver_Deneigeur.CelluleEnneigee(Cellule, pawn.Map);
        }

        protected override void Travailler(MapComponent_Labour composante)
        {
            pawn.Map.snowGrid.SetDepth(Cellule, 0f);
        }
    }
}
