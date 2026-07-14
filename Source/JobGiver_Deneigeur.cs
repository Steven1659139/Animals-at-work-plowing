using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Cerveau de la bête déneigeuse. Aucun dressage : elle enfile un harnais,
    // s'attelle à un grattoir, puis racle la neige de la zone de déneigement
    // vanilla. Quand la neige est partie, elle dépose le grattoir et se rend
    // disponible pour la charrue ou la charrette. C'est le pendant hivernal du
    // labour, qui s'arrête justement quand le sol gèle.
    public class JobGiver_Deneigeur : ThinkNode_JobGiver
    {
        // Épaisseur en deçà de laquelle une case est considérée dégagée
        // (même seuil que le déneigement des colons vanilla).
        public const float NeigeMin = 0.2f;

        // Si toutes les cases tirées sont réservées ou inaccessibles, la
        // bête retentera à sa prochaine décision plutôt que de tester la
        // zone entière d'un coup.
        private const int EssaisReservation = 8;

        // Tampon des cases enneigées de la zone, rebâti à chaque sélection
        // (les JobGivers ne sont jamais réentrants) : on tire au hasard
        // dedans plutôt que d'InRandomOrder tout ActiveCells, qui copiait et
        // mélangeait des milliers de cellules à chaque décision.
        private static readonly List<IntVec3> candidates = new List<IntVec3>();

        protected override Job TryGiveJob(Pawn pawn)
        {
            Map map = pawn.Map;
            if (map == null || pawn.Faction != Faction.OfPlayer)
            {
                return null;
            }
            if (pawn.def.GetModExtension<ModExtension_BeteDeTrait>() == null)
            {
                return null;
            }
            if (!AAW_DefOf.AAW_Harnachement.IsFinished)
            {
                return null;
            }

            bool travailEnAttente = map.GetComponent<MapComponent_Labour>().TravailDeneigementEnAttente();
            // Sans neige, le grattoir n'est que du bois mort sur le dos : la
            // bête le pose et l'attelage d'été redevient possible.
            if (EquipementUtility.Porte(pawn, AAW_DefOf.AAW_Grattoir) != null && !travailEnAttente)
            {
                EquipementUtility.DeposerAttelage(pawn, AAW_DefOf.AAW_Grattoir);
                return null;
            }
            if (!travailEnAttente)
            {
                return null;
            }
            // Sans harnais sur le dos, la bête va d'abord en enfiler un.
            if (EquipementUtility.Porte(pawn, AAW_DefOf.AAW_HarnaisDeTrait) == null)
            {
                return EquipementUtility.AllerChercher(pawn, AAW_DefOf.AAW_HarnaisDeTrait, AAW_DefOf.AAW_Harnacher);
            }
            // Puis il lui faut un grattoir, jamais en plus d'un autre
            // attelage : cette bête-là tire déjà autre chose.
            if (EquipementUtility.Porte(pawn, AAW_DefOf.AAW_Grattoir) == null)
            {
                if (EquipementUtility.AttelagePorte(pawn) != null)
                {
                    return null;
                }
                return EquipementUtility.AllerChercher(pawn, AAW_DefOf.AAW_Grattoir, AAW_DefOf.AAW_Atteler);
            }

            candidates.Clear();
            foreach (IntVec3 cellule in map.areaManager.SnowOrSandClear.ActiveCells)
            {
                if (CelluleEnneigee(cellule, map))
                {
                    candidates.Add(cellule);
                }
            }
            for (int essai = 0; essai < EssaisReservation && candidates.Count > 0; essai++)
            {
                int i = Rand.Range(0, candidates.Count);
                IntVec3 cellule = candidates[i];
                if (pawn.CanReserveAndReach(cellule, PathEndMode.OnCell, Danger.Some))
                {
                    candidates.Clear();
                    return JobMaker.MakeJob(AAW_DefOf.AAW_Deneiger, cellule);
                }
                candidates[i] = candidates[candidates.Count - 1];
                candidates.RemoveAt(candidates.Count - 1);
            }
            candidates.Clear();
            return null;
        }

        public static bool CelluleEnneigee(IntVec3 cellule, Map map)
        {
            return map.snowGrid.GetDepth(cellule) >= NeigeMin;
        }

        // Y a-t-il de la neige à racler quelque part dans la zone de
        // déneigement ? Sert aussi à décider de poser ou de prendre l'outil.
        // Toujours via le cache de MapComponent_Labour, jamais en direct.
        public static bool TravailExiste(Map map)
        {
            foreach (IntVec3 cellule in map.areaManager.SnowOrSandClear.ActiveCells)
            {
                if (CelluleEnneigee(cellule, map))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
