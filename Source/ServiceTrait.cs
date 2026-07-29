using RimWorld;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Côté colon. Décide quelle tâche une bête de trait doit servir à l'instant,
    // quel attelage lui poser, et vers quelle case la mener. Le colon équipe et
    // guide ; la bête, lâchée au champ (en service), fait le reste toute seule.
    public static class ServiceTrait
    {
        private const int PilesMinCharrette = 2; // même seuil que JobGiver_Charretier

        // La tâche à servir maintenant : cargaison à bord d'abord (il faut la
        // livrer), sinon la première tâche activée dont le travail attend, dans
        // l'ordre labour → déneigement → charrette. Aucune si rien n'attend :
        // la bête peut alors rentrer à l'enclos.
        public static TacheTrait TacheAServir(Pawn bete, MapComponent_Labour composante)
        {
            if (EquipementUtility.PremierCargo(bete) != null
                && composante.TacheAutorisee(bete, TacheTrait.Charrette))
            {
                return TacheTrait.Charrette;
            }
            if (AAW_DefOf.AAW_Harnachement.IsFinished
                && composante.TacheAutorisee(bete, TacheTrait.Labour)
                && composante.TravailLabourEnAttente())
            {
                return TacheTrait.Labour;
            }
            if (AAW_DefOf.AAW_Harnachement.IsFinished
                && composante.TacheAutorisee(bete, TacheTrait.Deneigement)
                && composante.TravailDeneigementEnAttente())
            {
                return TacheTrait.Deneigement;
            }
            if (AAW_DefOf.AAW_Charretterie.IsFinished
                && composante.TacheAutorisee(bete, TacheTrait.Charrette)
                && TravailCharretteEnAttente(bete.Map, bete))
            {
                return TacheTrait.Charrette;
            }
            return TacheTrait.Aucune;
        }

        // L'attelage qu'il faut à la bête pour cette tâche.
        public static ThingDef ImplementPour(TacheTrait tache)
        {
            switch (tache)
            {
                case TacheTrait.Labour: return AAW_DefOf.AAW_Charrue;
                case TacheTrait.Deneigement: return AAW_DefOf.AAW_Grattoir;
                case TacheTrait.Charrette: return AAW_DefOf.AAW_Charrette;
                default: return null;
            }
        }

        // Au moins deux piles à charrier quelque part (hors équipement de trait) ?
        public static bool TravailCharretteEnAttente(Map map, Pawn bete)
        {
            int n = 0;
            foreach (Thing t in map.listerHaulables.ThingsPotentiallyNeedingHauling())
            {
                if (!EquipementUtility.EstEquipement(t.def) && !t.IsForbidden(bete) && ++n >= PilesMinCharrette)
                {
                    return true;
                }
            }
            return false;
        }

        // Une case vers laquelle le colon mène la bête pour cette tâche : au plus
        // près du travail, atteignable par le colon (la bête est encore à
        // l'enclos, donc c'est l'accès du colon qui compte). La bête re-scanne
        // depuis là une fois lâchée.
        public static bool TrouverCelluleTravail(Pawn roper, TacheTrait tache, out IntVec3 cellule)
        {
            switch (tache)
            {
                case TacheTrait.Labour:
                    return JobGiver_Labourer.TrouverCelluleTravail(roper, out cellule);
                case TacheTrait.Deneigement:
                    return JobGiver_Deneigeur.TrouverCelluleTravail(roper, out cellule);
                case TacheTrait.Charrette:
                    return TrouverCharrette(roper, out cellule);
                default:
                    cellule = IntVec3.Invalid;
                    return false;
            }
        }

        private static bool TrouverCharrette(Pawn roper, out IntVec3 cellule)
        {
            Thing pile = GenClosest.ClosestThingReachable(
                roper.Position, roper.Map,
                ThingRequest.ForGroup(ThingRequestGroup.HaulableEver),
                PathEndMode.Touch, TraverseParms.For(roper), 9999f,
                t => !EquipementUtility.EstEquipement(t.def) && !t.IsForbidden(roper));
            cellule = pile != null ? pile.Position : IntVec3.Invalid;
            return pile != null;
        }
    }
}
