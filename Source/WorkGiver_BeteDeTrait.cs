using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Côté colon (Manipulation d'animaux). Prend en charge chaque bête de trait
    // marquée : il l'équipe (harnais puis attelage de la tâche du moment), la
    // mène à la corde jusqu'au champ, puis la ramène à l'enclos quand plus rien
    // n'attend. La bête ne s'équipe jamais et ne sort jamais seule de l'enclos.
    // La décision elle-même vit dans ServiceTrait, partagée avec le chien de
    // berger meneur (JobGiver_Meneur) ; ici ne restent que les filtres du
    // scanner et l'habillage de l'ordre prioritaire.
    public class WorkGiver_BeteDeTrait : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        // Libellé du clic droit « Prioriser… » : précisé selon l'étape en cours
        // (harnacher / mener au travail / ramener), plutôt qu'un générique.
        // directOrderable étant vrai par défaut, RimWorld génère l'option dès
        // que JobOnThing(forced) renvoie un job sur la bête cliquée.
        public override string PostProcessedGerund(Job job)
        {
            if (job.def == AAW_DefOf.AAW_MenerAuTravail)
            {
                return "AAW_GerundMener".Translate();
            }
            if (job.def == AAW_DefOf.AAW_RamenerAEnclos)
            {
                return "AAW_GerundRamener".Translate();
            }
            if (job.def == AAW_DefOf.AAW_ColonEquiper)
            {
                return "AAW_GerundEquiper".Translate();
            }
            return base.PostProcessedGerund(job);
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            MapComponent_Labour component = MapComponent_Labour.Of(pawn.Map);
            foreach (Pawn beast in pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction))
            {
                // Les bêtes marquées (à équiper/mener) et celles encore en
                // service (à ramener, même si on vient de les dé-marquer).
                if (beast.RaceProps.Animal
                    && (component.IsDraftBeast(beast) || component.IsOnDuty(beast)))
                {
                    yield return beast;
                }
            }
        }

        // JobGiver_Work demande HasJobOnThing pour retenir la bête, puis
        // JobOnThing pour obtenir le job, au même tick : la décision
        // (parcours des zones avec atteignabilité, recherche de rangement par
        // pile) tournait donc deux fois par bête retenue. On garde le dernier
        // job calculé le temps de ce second appel. Le worker est partagé par
        // tous les colons, d'où le meneur dans la clé.
        private Pawn lastHandler;
        private Pawn lastBeast;
        private bool lastForced;
        private int lastTick = -1;
        private Job lastJob;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Job job = Decide(pawn, t, forced);
            lastHandler = pawn;
            lastBeast = t as Pawn;
            lastForced = forced;
            lastTick = Find.TickManager.TicksGame;
            lastJob = job;
            return job != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (lastJob != null
                && lastHandler == pawn
                && lastBeast == t
                && lastForced == forced
                && lastTick == Find.TickManager.TicksGame)
            {
                Job job = lastJob;
                lastJob = null;
                return job;
            }
            return Decide(pawn, t, forced);
        }

        private static Job Decide(Pawn pawn, Thing t, bool forced)
        {
            if (!(t is Pawn beast) || beast == pawn)
            {
                return null;
            }
            if (!BeteDeTrait.Is(beast.def))
            {
                return null;
            }
            MapComponent_Labour component = MapComponent_Labour.Of(pawn.Map);
            // Ni marquée, ni en service : rien à faire avec elle.
            if (!component.IsDraftBeast(beast) && !component.IsOnDuty(beast))
            {
                return null;
            }
            // Déjà menée par quelqu'un, ou en crise : on n'y touche pas.
            if (beast.roping.IsRoped || beast.InMentalState)
            {
                return null;
            }
            if (!pawn.CanReserve(beast, 1, -1, null, forced))
            {
                return null;
            }

            // Le reste de la décision est commun aux deux meneurs (colon et chien
            // de berger) ; le colon est celui des deux qui sait aussi équiper.
            return ServiceTrait.ServiceJob(pawn, beast, component, forced, true);
        }
    }
}
