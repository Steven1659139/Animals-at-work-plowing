using RimWorld;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Côté meneur. Décide quelle tâche une bête de trait doit servir à l'instant,
    // quel attelage lui poser, et vers quelle case la mener. Le meneur équipe et
    // guide ; la bête, lâchée au champ (en service), fait le reste toute seule.
    //
    // Deux meneurs possibles, un seul arbre de décision (JobDeService) : le colon
    // (WorkGiver_BeteDeTrait), qui sait aussi apporter et boucler l'équipement, et
    // le chien de berger (JobGiver_Meneur, si le module Herding Dogs est présent),
    // qui n'a que la corde.
    public static class ServiceTrait
    {
        private const int PilesMinCharrette = 2; // même seuil que JobGiver_Charretier

        // Ce que le meneur doit faire de cette bête à l'instant, ou null s'il n'y
        // a rien à en faire. Appelé une fois la bête filtrée (bête de trait ou
        // encore en service, ni menée, ni en crise, et réservable).
        //   peutEquiper : le meneur sait-il apporter une pièce d'équipement ?
        //                 vrai pour un colon, faux pour un animal meneur.
        public static Job JobDeService(Pawn meneur, Pawn bete, MapComponent_Labour composante,
            bool forced, bool peutEquiper)
        {
            bool enService = composante.EstEnService(bete);

            // Garde-fou famine : le service ne se coupe normalement que faute de
            // travail, mais une bête au champ ne broute pas les récoltes — si
            // elle en vient à la famine réelle (rien d'autre à manger à portée),
            // on la ramène exceptionnellement à l'enclos pour qu'elle mange.
            if (enService
                && bete.needs?.food != null
                && bete.needs.food.CurCategory >= HungerCategory.Starving)
            {
                return JobRamener(meneur, bete);
            }

            TacheTrait tache = TacheAServir(bete, composante);

            // Plus rien à faire : ramener à l'enclos la bête qui est encore
            // dehors. Une bête harnachée hors de tout enclos compte comme dehors
            // même hors service : le patch de service l'a retirée du bétail géré
            // par enclos, donc personne d'autre ne viendra la chercher.
            if (tache == TacheTrait.Aucune)
            {
                return enService || (Harnachee(bete) && !DansUnEnclos(bete))
                    ? JobRamener(meneur, bete)
                    : null;
            }

            // Il reste du travail : mettre l'équipement en accord, puis mener.
            ThingDef manquante = PieceManquante(bete, tache);
            if (manquante != null)
            {
                // Apporter le harnais et le boucler demande des mains : un animal
                // meneur passe son tour et laisse la bête au colon.
                return peutEquiper ? JobEquiper(meneur, bete, manquante, forced) : null;
            }
            // Équipée mais pas encore au travail : la mener au champ.
            if (!enService)
            {
                // Sauf si elle vient de rentrer. Une bête reste attelée entre
                // deux tournées (seul un colon la déséquipe, et seulement quand
                // elle cesse d'être bête de trait), donc rien ne la retient de
                // repartir à la seconde où du travail réapparaît. Le travail de
                // charrette, lui, va et vient au rythme des piles à ranger : le
                // seuil se franchit dans les deux sens sans arrêt, et le meneur
                // fait la navette entre l'enclos et le champ. Ce répit met un
                // plancher à la fréquence des sorties.
                return composante.EnRepitDeRetour(bete) ? null : JobMener(meneur, bete, tache);
            }
            // En service mais hors de portée de tout travail : elle a été lâchée
            // au mauvais endroit, ou le chemin s'est fermé depuis (portail muré,
            // clôture posée autour d'elle). Sans ce rattrapage elle resterait
            // « au travail » à tourner en rond, le meneur la croyant occupée.
            // Une bête déjà à l'ouvrage tranche la question sans rien scanner.
            if (!ALOuvrage(bete) && !PeutTravaillerSeule(bete, tache))
            {
                return JobMener(meneur, bete, tache);
            }
            // Équipée et en service : elle travaille seule, le meneur la laisse.
            return null;
        }

        // La pièce qui manque à la bête pour cette tâche, ou null si elle est en
        // ordre. Un mauvais attelage (changement de saison) passe avant tout : le
        // colon retirera l'ancien sur place en posant le neuf.
        private static ThingDef PieceManquante(Pawn bete, TacheTrait tache)
        {
            ThingDef implement = ImplementPour(tache);
            Thing attelage = EquipementUtility.AttelagePorte(bete);
            if (attelage != null && attelage.def != implement)
            {
                return implement;
            }
            if (EquipementUtility.Porte(bete, AAW_DefOf.AAW_HarnaisDeTrait) == null)
            {
                return AAW_DefOf.AAW_HarnaisDeTrait;
            }
            return attelage == null ? implement : null;
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

        // Le harnais est le signe qu'une bête est en cours de mise au travail :
        // un colon l'a bouclé, et elle attend d'être menée ou elle en revient.
        private static bool Harnachee(Pawn bete)
        {
            return EquipementUtility.Porte(bete, AAW_DefOf.AAW_HarnaisDeTrait) != null;
        }

        // Pendant tout ce temps-là, elle n'est plus du bétail libre : ni ramenée
        // d'office à l'enclos par un colon ou un chien, ni sujette à la fugue.
        // Voir Patch_Service, qui en fait un Roamer à false.
        public static bool DispenseeDEnclos(Pawn bete, MapComponent_Labour composante)
        {
            return composante.EstEnService(bete)
                || (composante.EstBeteDeTrait(bete) && Harnachee(bete));
        }

        // La bête est-elle à l'intérieur d'un enclos clos ? Test par régions,
        // comme JobDriver_Mener : AnimalPenUtility.GetCurrentPenOf s'ouvre sur
        // « if (!animal.Roamer) return null », et c'est justement ce que le patch
        // de service met à false.
        private static bool DansUnEnclos(Pawn bete)
        {
            Region region = bete.GetRegion();
            if (region == null)
            {
                return false;
            }
            foreach (Building batiment in bete.Map.listerBuildings.allBuildingsAnimalPenMarkers)
            {
                PenMarkerState etat = batiment.TryGetComp<CompAnimalPenMarker>().PenState;
                if (etat.Enclosed && etat.ContainsConnectedRegion(region))
                {
                    return true;
                }
            }
            return false;
        }

        private static Job JobEquiper(Pawn meneur, Pawn bete, ThingDef def, bool forced)
        {
            Thing pile = GenClosest.ClosestThingReachable(
                meneur.Position, meneur.Map, ThingRequest.ForDef(def),
                PathEndMode.ClosestTouch, TraverseParms.For(meneur), 9999f,
                t => !t.IsForbidden(meneur) && meneur.CanReserve(t, 1, 1, null, forced));
            if (pile == null)
            {
                return null;
            }
            Job job = JobMaker.MakeJob(AAW_DefOf.AAW_ColonEquiper, bete, pile);
            job.count = 1;
            return job;
        }

        private static Job JobMener(Pawn meneur, Pawn bete, TacheTrait tache)
        {
            if (!TrouverCelluleTravail(bete, meneur, tache, out IntVec3 cellule))
            {
                return null;
            }
            return JobMaker.MakeJob(AAW_DefOf.AAW_MenerAuTravail, bete, cellule);
        }

        private static Job JobRamener(Pawn meneur, Pawn bete)
        {
            CompAnimalPenMarker pen = AnimalPenUtility.ClosestSuitablePen(bete, false);
            if (pen == null)
            {
                return null; // pas d'enclos convenable : le joueur gère.
            }
            IntVec3 cellule = AnimalPenUtility.FindPlaceInPenToStand(pen, meneur);
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

        // Au moins deux piles qui vaudront le déplacement ?
        //
        // Ce test décide de sortir la bête de l'enclos, et il se pose donc
        // depuis l'enclos : il ne retient que ce qui ne dépend pas de l'endroit
        // où elle est. Surtout pas son accessibilité — une bête est bloquée par
        // les clôtures (Pawn.FenceBlocked), donc rien du dehors ne lui est
        // accessible tant qu'un meneur ne l'a pas fait franchir le portail, et
        // lui poser la question du charretier reviendrait à ne jamais la sortir.
        //
        // Ce qu'il retient du charretier, c'est le point décisif : la pile
        // a-t-elle un stock où aller. Sans lui, des gravats que rien n'accepte
        // comptaient comme du travail en attente, on sortait la bête, elle ne
        // trouvait aucune tournée, et on la ramenait — en boucle.
        public static bool TravailCharretteEnAttente(Map map, Pawn bete)
        {
            int n = 0;
            foreach (Thing t in map.listerHaulables.ThingsPotentiallyNeedingHauling())
            {
                // Filtres bon marché d'abord : la recherche de rangement ne
                // tourne que pour les piles qui ont passé le reste.
                if (EquipementUtility.EstEquipement(t.def) || t.IsForbidden(bete)
                    || !bete.CanReserve(t))
                {
                    continue;
                }
                if (JobGiver_Charretier.ADestination(map, t) && ++n >= PilesMinCharrette)
                {
                    return true;
                }
            }
            return false;
        }

        // Le trajet que fera l'attelage colon + bête au bout de la corde. Ni
        // l'accès du colon seul (il saute les clôtures, la bête non), ni celui de
        // la bête seule (menée, elle franchit les portes que le colon ouvre pour
        // elle — Building_Door consulte roping.RopedByPawn) : c'est l'hybride que
        // vanilla utilise pour ses enclos (AnimalPenUtility.CheckUseAndReach).
        // Départ depuis la bête, paramètres de trajet du colon, clôtures selon la
        // bête.
        public static bool MeneurPeutYMener(Pawn meneur, Pawn bete, IntVec3 cellule)
        {
            return bete.Map.reachability.CanReach(
                bete.Position, cellule, PathEndMode.OnCell,
                TraverseParms.For(meneur, Danger.Some).WithFenceblockedOf(bete));
        }

        // La bête peut-elle rejoindre le travail toute seule, de là où elle est ?
        // Elle est son propre meneur : on mesure donc ses seules capacités, sans
        // les portes qu'un colon lui ouvrirait. Faux pour une bête en service
        // lâchée au mauvais endroit — dans son enclos, par exemple — qui resterait
        // sinon éternellement « au travail » sans pouvoir travailler.
        public static bool PeutTravaillerSeule(Pawn bete, TacheTrait tache)
        {
            return TrouverCelluleTravail(bete, bete, tache, out _);
        }

        // Une case vers laquelle le colon mène la bête pour cette tâche : au plus
        // près d'elle et joignable en la menant. Elle re-scanne depuis là une fois
        // lâchée.
        public static bool TrouverCelluleTravail(Pawn bete, Pawn meneur, TacheTrait tache, out IntVec3 cellule)
        {
            switch (tache)
            {
                case TacheTrait.Labour:
                    return JobGiver_Labourer.TrouverCelluleTravail(bete, meneur, out cellule);
                case TacheTrait.Deneigement:
                    return JobGiver_Deneigeur.TrouverCelluleTravail(bete, meneur, out cellule);
                case TacheTrait.Charrette:
                    return TrouverCharrette(bete, meneur, out cellule);
                default:
                    cellule = IntVec3.Invalid;
                    return false;
            }
        }

        private static bool TrouverCharrette(Pawn bete, Pawn meneur, out IntVec3 cellule)
        {
            Thing pile = GenClosest.ClosestThingReachable(
                bete.Position, bete.Map,
                ThingRequest.ForGroup(ThingRequestGroup.HaulableEver),
                PathEndMode.Touch,
                TraverseParms.For(meneur, Danger.Some).WithFenceblockedOf(bete), 9999f,
                t => !EquipementUtility.EstEquipement(t.def) && !t.IsForbidden(meneur));
            cellule = pile != null ? pile.Position : IntVec3.Invalid;
            return pile != null;
        }
    }
}
