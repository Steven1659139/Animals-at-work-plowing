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
            MapComponent_Labour composante = pawn.Map.GetComponent<MapComponent_Labour>();
            foreach (Pawn bete in pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction))
            {
                // Les bêtes marquées (à équiper/mener) et celles encore en
                // service (à ramener, même si on vient de les dé-marquer).
                if (bete.RaceProps.Animal
                    && (composante.EstBeteDeTrait(bete) || composante.EstEnService(bete)))
                {
                    yield return bete;
                }
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobOnThing(pawn, t, forced) != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Pawn bete) || bete == pawn)
            {
                return null;
            }
            if (!BeteDeTrait.Est(bete.def))
            {
                return null;
            }
            MapComponent_Labour composante = pawn.Map.GetComponent<MapComponent_Labour>();
            // Ni marquée, ni en service : rien à faire avec elle.
            if (!composante.EstBeteDeTrait(bete) && !composante.EstEnService(bete))
            {
                return null;
            }
            // Déjà menée par quelqu'un, ou en crise : on n'y touche pas.
            if (bete.roping.IsRoped || bete.InMentalState)
            {
                return null;
            }
            if (!pawn.CanReserve(bete, 1, -1, null, forced))
            {
                return null;
            }

            // Le reste de la décision est commun aux deux meneurs (colon et chien
            // de berger) ; le colon est celui des deux qui sait aussi équiper.
            return ServiceTrait.JobDeService(pawn, bete, composante, forced, true);
        }
    }
}
