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
            if (bete.def.GetModExtension<ModExtension_BeteDeTrait>() == null)
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

            bool enService = composante.EstEnService(bete);

            // Garde-fou famine : le service ne se coupe normalement que faute de
            // travail, mais une bête au champ ne broute pas les récoltes — si
            // elle en vient à la famine réelle (rien d'autre à manger à portée),
            // on la ramène exceptionnellement à l'enclos pour qu'elle mange.
            if (enService
                && bete.needs?.food != null
                && bete.needs.food.CurCategory >= HungerCategory.Starving)
            {
                return JobRamener(pawn, bete);
            }

            TacheTrait tache = ServiceTrait.TacheAServir(bete, composante);

            // Plus rien à faire : ramener à l'enclos la bête qui est encore dehors.
            if (tache == TacheTrait.Aucune)
            {
                return enService ? JobRamener(pawn, bete) : null;
            }

            // Il reste du travail : mettre l'équipement en accord, puis mener.
            ThingDef implement = ServiceTrait.ImplementPour(tache);
            Thing attelage = EquipementUtility.AttelagePorte(bete);

            // Mauvais attelage (changement de saison) : aller chercher le bon.
            // Le colon retirera l'ancien sur place en posant le neuf.
            if (attelage != null && attelage.def != implement)
            {
                return JobEquiper(pawn, bete, implement, forced);
            }
            // Pas de harnais : le poser d'abord.
            if (EquipementUtility.Porte(bete, AAW_DefOf.AAW_HarnaisDeTrait) == null)
            {
                return JobEquiper(pawn, bete, AAW_DefOf.AAW_HarnaisDeTrait, forced);
            }
            // Pas encore d'attelage : poser celui de la tâche.
            if (attelage == null)
            {
                return JobEquiper(pawn, bete, implement, forced);
            }
            // Équipée mais pas encore au travail : la mener au champ.
            if (!enService)
            {
                return JobMener(pawn, bete, tache);
            }
            // En service mais hors de portée de tout travail : elle a été lâchée
            // au mauvais endroit, ou le chemin s'est fermé depuis (portail muré,
            // clôture posée autour d'elle). Sans ce rattrapage elle resterait
            // « au travail » à tourner en rond, le colon la croyant occupée.
            // Une bête déjà à l'ouvrage tranche la question sans rien scanner.
            if (!ALOuvrage(bete) && !ServiceTrait.PeutTravaillerSeule(bete, tache))
            {
                return JobMener(pawn, bete, tache);
            }
            // Équipée et en service : elle travaille seule, le colon la laisse.
            return null;
        }

        // La bête est-elle en train de faire l'un de nos travaux ? Si oui, elle
        // est manifestement à portée du sien, et la question ne mérite pas le
        // scan des zones qu'elle coûterait.
        private static bool ALOuvrage(Pawn bete)
        {
            JobDef job = bete.CurJobDef;
            return job == AAW_DefOf.AAW_Labourer
                || job == AAW_DefOf.AAW_Deneiger
                || job == AAW_DefOf.AAW_ChargerCharrette
                || job == AAW_DefOf.AAW_ViderCharrette;
        }

        private static Job JobEquiper(Pawn pawn, Pawn bete, ThingDef def, bool forced)
        {
            Thing pile = GenClosest.ClosestThingReachable(
                pawn.Position, pawn.Map, ThingRequest.ForDef(def),
                PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999f,
                t => !t.IsForbidden(pawn) && pawn.CanReserve(t, 1, 1, null, forced));
            if (pile == null)
            {
                return null;
            }
            Job job = JobMaker.MakeJob(AAW_DefOf.AAW_ColonEquiper, bete, pile);
            job.count = 1;
            return job;
        }

        private static Job JobMener(Pawn pawn, Pawn bete, TacheTrait tache)
        {
            if (!ServiceTrait.TrouverCelluleTravail(bete, pawn, tache, out IntVec3 cellule))
            {
                return null;
            }
            return JobMaker.MakeJob(AAW_DefOf.AAW_MenerAuTravail, bete, cellule);
        }

        private static Job JobRamener(Pawn pawn, Pawn bete)
        {
            CompAnimalPenMarker pen = AnimalPenUtility.ClosestSuitablePen(bete, false);
            if (pen == null)
            {
                return null; // pas d'enclos convenable : le joueur gère.
            }
            IntVec3 cellule = AnimalPenUtility.FindPlaceInPenToStand(pen, pawn);
            if (!cellule.IsValid)
            {
                cellule = pen.parent.Position;
            }
            Job job = JobMaker.MakeJob(AAW_DefOf.AAW_RamenerAEnclos, bete, cellule);
            // Le marqueur d'enclos en cible C, comme JobDriver_RopeToPen : c'est
            // lui qui dira si la bête a franchi la clôture (voir JobDriver_Mener).
            job.SetTarget(TargetIndex.C, pen.parent);
            return job;
        }
    }
}
