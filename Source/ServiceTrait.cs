using RimWorld;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Côté meneur. Décide quelle tâche une bête de trait doit servir à l'instant,
    // quel attelage lui poser, et vers quelle case la mener. Le meneur équipe et
    // guide ; la bête, lâchée au champ (en service), fait le reste toute seule.
    //
    // Deux meneurs possibles, un seul arbre de décision (ServiceJob) : le colon
    // (WorkGiver_BeteDeTrait), qui sait aussi apporter et boucler l'équipement, et
    // le chien de berger (JobGiver_Meneur, si le module Herding Dogs est présent),
    // qui n'a que la corde.
    public static class ServiceTrait
    {
        private const int MinCartStacks = 2; // même seuil que JobGiver_Charretier

        // Filtres communs aux cerveaux des bêtes (JobGiver_Labourer, Deneigeur,
        // Charretier) : la bête est à la colonie, son espèce est de trait, et un
        // colon l'a menée dehors (en service). Elle ne s'attelle ni ne sort de
        // l'enclos seule. Renseigne la composante de sa carte au passage.
        public static bool OnDutyAtColony(Pawn beast, out MapComponent_Labour component)
        {
            component = null;
            Map map = beast.Map;
            if (map == null || beast.Faction != Faction.OfPlayer || !BeteDeTrait.Is(beast.def))
            {
                return false;
            }
            component = MapComponent_Labour.Of(map);
            return component.IsOnDuty(beast);
        }

        // La bête peut-elle faire cette tâche, là, tout de suite : tâche activée
        // par le joueur, recherche faite, harnais et attelage de la tâche sur le
        // dos.
        public static bool Equipped(Pawn beast, TacheTrait task, MapComponent_Labour component)
        {
            return component.TaskAllowed(beast, task)
                && ResearchDone(task)
                && EquipementUtility.Carries(beast, AAW_DefOf.AAW_HarnaisDeTrait) != null
                && EquipementUtility.Carries(beast, ImplementFor(task)) != null;
        }

        // Les deux à la fois : ce que le labour et le déneigement demandent
        // avant de chercher une case.
        public static bool ReadyForWork(Pawn beast, TacheTrait task, out MapComponent_Labour component)
        {
            return OnDutyAtColony(beast, out component) && Equipped(beast, task, component);
        }

        // La recherche qui débloque la tâche : le harnachement pour la charrue
        // et le grattoir, la charretterie pour la charrette.
        public static bool ResearchDone(TacheTrait task)
        {
            ResearchProjectDef searchText = task == TacheTrait.Charrette
                ? AAW_DefOf.AAW_Charretterie
                : AAW_DefOf.AAW_Harnachement;
            return searchText.IsFinished;
        }

        // Ce que le meneur doit faire de cette bête à l'instant, ou null s'il n'y
        // a rien à en faire. Appelé une fois la bête filtrée (bête de trait ou
        // encore en service, ni menée, ni en crise, et réservable).
        //   peutEquiper : le meneur sait-il apporter une pièce d'équipement ?
        //                 vrai pour un colon, faux pour un animal meneur.
        public static Job ServiceJob(Pawn handler, Pawn beast, MapComponent_Labour component,
            bool forced, bool canEquip)
        {
            bool onDuty = component.IsOnDuty(beast);

            // Garde-fou famine : le service ne se coupe normalement que faute de
            // travail, mais une bête au champ ne broute pas les récoltes : si
            // elle en vient à la famine réelle (rien d'autre à manger à portée),
            // on la ramène exceptionnellement à l'enclos pour qu'elle mange.
            if (onDuty && Starving(beast))
            {
                return BringBackJob(handler, beast);
            }

            TacheTrait task = TaskToServe(beast, component);

            // Plus rien à faire : ramener à l'enclos la bête qui est encore
            // dehors. Une bête harnachée hors de tout enclos compte comme dehors
            // même hors service : le patch de service l'a retirée du bétail géré
            // par enclos, donc personne d'autre ne viendra la chercher.
            if (task == TacheTrait.Aucune)
            {
                return onDuty || (Harnessed(beast) && !InsideAPen(beast))
                    ? BringBackJob(handler, beast)
                    : null;
            }

            // Il reste du travail : mettre l'équipement en accord, puis mener.
            ThingDef missing = MissingPiece(beast, task);
            if (missing != null)
            {
                // Apporter le harnais et le boucler demande des mains : un animal
                // meneur passe son tour et laisse la bête au colon.
                return canEquip ? EquipJob(handler, beast, missing, forced) : null;
            }
            // Équipée mais pas encore au travail : la mener au champ.
            if (!onDuty)
            {
                // Sauf si elle vient de rentrer. Une bête reste attelée entre
                // deux tournées (seul un colon la déséquipe, et seulement quand
                // elle cesse d'être bête de trait), donc rien ne la retient de
                // repartir à la seconde où du travail réapparaît. Le travail de
                // charrette, lui, va et vient au rythme des piles à ranger : le
                // seuil se franchit dans les deux sens sans arrêt, et le meneur
                // fait la navette entre l'enclos et le champ. Ce répit met un
                // plancher à la fréquence des sorties.
                return component.InReturnGrace(beast) ? null : LeadJob(handler, beast, task);
            }
            // En service mais hors de portée de tout travail : elle a été lâchée
            // au mauvais endroit, ou le chemin s'est fermé depuis (portail muré,
            // clôture posée autour d'elle). Sans ce rattrapage elle resterait
            // « au travail » à tourner en rond, le meneur la croyant occupée.
            // Une bête déjà à l'ouvrage tranche la question sans rien scanner.
            if (!AtWork(beast) && !CanWorkAlone(beast, task))
            {
                return LeadJob(handler, beast, task);
            }
            // Équipée et en service : elle travaille seule, le meneur la laisse.
            return null;
        }

        // La pièce qui manque à la bête pour cette tâche, ou null si elle est en
        // ordre. Un mauvais attelage (changement de saison) passe avant tout : le
        // colon retirera l'ancien sur place en posant le neuf.
        private static ThingDef MissingPiece(Pawn beast, TacheTrait task)
        {
            ThingDef wantedImplement = ImplementFor(task);
            Thing implement = EquipementUtility.CarriedImplement(beast);
            if (implement != null && implement.def != wantedImplement)
            {
                return wantedImplement;
            }
            if (EquipementUtility.Carries(beast, AAW_DefOf.AAW_HarnaisDeTrait) == null)
            {
                return AAW_DefOf.AAW_HarnaisDeTrait;
            }
            return implement == null ? wantedImplement : null;
        }

        // La bête est-elle en train de faire l'un de nos travaux ? Si oui, elle
        // est manifestement à portée du sien, et la question ne mérite pas le
        // scan des zones qu'elle coûterait.
        private static bool AtWork(Pawn beast)
        {
            JobDef job = beast.CurJobDef;
            return job == AAW_DefOf.AAW_Labourer
                || job == AAW_DefOf.AAW_Deneiger
                || job == AAW_DefOf.AAW_ChargerCharrette
                || job == AAW_DefOf.AAW_ViderCharrette;
        }

        // Le harnais est le signe qu'une bête est en cours de mise au travail :
        // un colon l'a bouclé, et elle attend d'être menée ou elle en revient.
        private static bool Harnessed(Pawn beast)
        {
            return EquipementUtility.Carries(beast, AAW_DefOf.AAW_HarnaisDeTrait) != null;
        }

        // Pendant tout ce temps-là, elle n'est plus du bétail libre : ni ramenée
        // d'office à l'enclos par un colon ou un chien, ni sujette à la fugue.
        // Voir Patch_Service, qui en fait un Roamer à false.
        public static bool ExemptFromPen(Pawn beast, MapComponent_Labour component)
        {
            // La famine lève toutes les dispenses : la bête redevient du bétail
            // ordinaire, que les colons ramènent et que rien de notre fait ne
            // retient plus. Le meneur la ramène déjà de son côté, mais faute
            // d'enclos convenable il n'a parfois rien à proposer, et la bête
            // restait alors sous notre régime, à jeun, jusqu'à la mort.
            if (Starving(beast))
            {
                return false;
            }
            return component.IsOnDuty(beast)
                || (component.IsDraftBeast(beast) && Harnessed(beast));
        }

        // Les deux questions que posent les patches de service (Patch_Service),
        // avec toutes les gardes : la bête est sur une carte, son espèce est de
        // trait, et la carte a sa composante. Faux dès qu'une garde manque.
        public static bool IsExempt(Pawn beast)
        {
            MapComponent_Labour component = ComponentIfDraft(beast);
            return component != null && ExemptFromPen(beast, component);
        }

        public static bool IsOnDuty(Pawn beast)
        {
            MapComponent_Labour component = ComponentIfDraft(beast);
            return component != null && component.IsOnDuty(beast);
        }

        private static MapComponent_Labour ComponentIfDraft(Pawn beast)
        {
            if (beast == null || !beast.Spawned || !BeteDeTrait.Is(beast.def))
            {
                return null;
            }
            return MapComponent_Labour.Of(beast.Map);
        }

        // Famine réelle : le dernier cran, celui où la bête commence à s'abîmer.
        public static bool Starving(Pawn beast)
        {
            return beast.needs?.food != null
                && beast.needs.food.CurCategory >= HungerCategory.Starving;
        }

        // La bête est-elle à l'intérieur d'un enclos clos ?
        private static bool InsideAPen(Pawn beast)
        {
            return PenContaining(beast.Map, beast.GetRegion()) != null;
        }

        // Le marqueur du premier enclos clos qui contient cette région, ou null
        // si elle est à l'air libre. Test par régions plutôt que par
        // AnimalPenUtility.GetCurrentPenOf : celui-ci s'ouvre sur
        // « if (!animal.Roamer) return null », et c'est justement ce que le
        // patch de service met à false. Sert aussi à JobDriver_Mener pour
        // savoir si la bête et sa destination sont du même côté des clôtures.
        public static CompAnimalPenMarker PenContaining(Map map, Region region)
        {
            if (region == null)
            {
                return null;
            }
            foreach (Building batiment in map.listerBuildings.allBuildingsAnimalPenMarkers)
            {
                CompAnimalPenMarker marker = batiment.TryGetComp<CompAnimalPenMarker>();
                PenMarkerState state = marker.PenState;
                if (state.Enclosed && state.ContainsConnectedRegion(region))
                {
                    return marker;
                }
            }
            return null;
        }

        private static Job EquipJob(Pawn handler, Pawn beast, ThingDef def, bool forced)
        {
            Thing stack = GenClosest.ClosestThingReachable(
                handler.Position, handler.Map, ThingRequest.ForDef(def),
                PathEndMode.ClosestTouch, TraverseParms.For(handler), 9999f,
                t => !t.IsForbidden(handler) && handler.CanReserve(t, 1, 1, null, forced));
            if (stack == null)
            {
                return null;
            }
            Job job = JobMaker.MakeJob(AAW_DefOf.AAW_ColonEquiper, beast, stack);
            job.count = 1;
            return job;
        }

        private static Job LeadJob(Pawn handler, Pawn beast, TacheTrait task)
        {
            if (!FindWorkCell(beast, handler, task, out IntVec3 cell))
            {
                return null;
            }
            return JobMaker.MakeJob(AAW_DefOf.AAW_MenerAuTravail, beast, cell);
        }

        private static Job BringBackJob(Pawn handler, Pawn beast)
        {
            CompAnimalPenMarker pen = AnimalPenUtility.ClosestSuitablePen(beast, false);
            if (pen == null)
            {
                return null; // pas d'enclos convenable : le joueur gère.
            }
            IntVec3 cell = AnimalPenUtility.FindPlaceInPenToStand(pen, handler);
            if (!cell.IsValid)
            {
                cell = pen.parent.Position;
            }
            Job job = JobMaker.MakeJob(AAW_DefOf.AAW_RamenerAEnclos, beast, cell);
            // Le marqueur d'enclos en cible C, comme JobDriver_RopeToPen : c'est
            // lui qui dira si la bête a franchi la clôture (voir JobDriver_Mener).
            job.SetTarget(TargetIndex.C, pen.parent);
            return job;
        }

        // La tâche à servir maintenant : cargaison à bord d'abord (il faut la
        // livrer), sinon la première tâche activée dont le travail attend, dans
        // l'ordre labour → déneigement → charrette. Aucune si rien n'attend :
        // la bête peut alors rentrer à l'enclos.
        public static TacheTrait TaskToServe(Pawn beast, MapComponent_Labour component)
        {
            if (EquipementUtility.FirstCargo(beast) != null
                && component.TaskAllowed(beast, TacheTrait.Charrette))
            {
                return TacheTrait.Charrette;
            }
            if (ResearchDone(TacheTrait.Labour)
                && component.TaskAllowed(beast, TacheTrait.Labour)
                && component.PlowWorkPending())
            {
                return TacheTrait.Labour;
            }
            if (ResearchDone(TacheTrait.Deneigement)
                && component.TaskAllowed(beast, TacheTrait.Deneigement)
                && component.SnowWorkPending())
            {
                return TacheTrait.Deneigement;
            }
            if (ResearchDone(TacheTrait.Charrette)
                && component.TaskAllowed(beast, TacheTrait.Charrette)
                && CartWorkPending(beast.Map, beast))
            {
                return TacheTrait.Charrette;
            }
            return TacheTrait.Aucune;
        }

        // L'attelage qu'il faut à la bête pour cette tâche.
        public static ThingDef ImplementFor(TacheTrait task)
        {
            switch (task)
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
        // où elle est. Surtout pas son accessibilité : une bête est bloquée par
        // les clôtures (Pawn.FenceBlocked), donc rien du dehors ne lui est
        // accessible tant qu'un meneur ne l'a pas fait franchir le portail, et
        // lui poser la question du charretier reviendrait à ne jamais la sortir.
        //
        // Ce qu'il retient du charretier, c'est le point décisif : la pile
        // a-t-elle un stock où aller. Sans ce test, des gravats que rien
        // n'accepte compteraient comme du travail en attente, et la bête ferait
        // la navette entre l'enclos et un champ sans tournée.
        public static bool CartWorkPending(Map map, Pawn beast)
        {
            return CountStacks(map, beast) >= MinCartStacks;
        }

        // Les piles qui comptent, plafonné au seuil : sert au test ci-dessus et
        // au diagnostic dév, qui a besoin du nombre et pas seulement du oui/non.
        public static int CountStacks(Map map, Pawn beast)
        {
            int n = 0;
            foreach (Thing t in map.listerHaulables.ThingsPotentiallyNeedingHauling())
            {
                // Pas de CanReserve ici : une pile qu'un colon vient de réserver
                // reste du travail qui attend, et dans une colonie active il
                // s'en réserve sans cesse, si bien que le seuil ne serait jamais
                // atteint. C'est au charretier de trancher, une fois la bête au
                // champ et pile par pile (CanBeCarted).
                if (EquipementUtility.IsEquipment(t.def) || t.IsForbidden(beast))
                {
                    continue;
                }
                // Coûteux : en dernier, et on s'arrête au seuil.
                if (JobGiver_Charretier.HasDestination(map, t) && ++n >= MinCartStacks)
                {
                    break;
                }
            }
            return n;
        }

        // Le trajet que fera l'attelage colon + bête au bout de la corde. Ni
        // l'accès du colon seul (il saute les clôtures, la bête non), ni celui de
        // la bête seule (menée, elle franchit les portes que le colon ouvre pour
        // elle : Building_Door consulte roping.RopedByPawn). C'est l'hybride que
        // vanilla utilise pour ses enclos (AnimalPenUtility.CheckUseAndReach).
        // Départ depuis la bête, paramètres de trajet du colon, clôtures selon la
        // bête.
        public static bool HandlerCanLeadThere(Pawn handler, Pawn beast, IntVec3 cell)
        {
            return beast.Map.reachability.CanReach(
                beast.Position, cell, PathEndMode.OnCell,
                TraverseParms.For(handler, Danger.Some).WithFenceblockedOf(beast));
        }

        // La case est-elle joignable pour ce travail ? Seule (meneur null), la
        // bête doit pouvoir y aller et la réserver ; menée, c'est l'attelage
        // colon + bête au bout de la corde qui compte.
        public static bool Reachable(Pawn beast, Pawn handler, IntVec3 cell)
        {
            return handler == null
                ? beast.CanReserveAndReach(cell, PathEndMode.OnCell, Danger.Some)
                : HandlerCanLeadThere(handler, beast, cell);
        }

        // La bête peut-elle rejoindre le travail toute seule, de là où elle est ?
        // Elle est son propre meneur : on mesure donc ses seules capacités, sans
        // les portes qu'un colon lui ouvrirait. Faux pour une bête en service
        // lâchée au mauvais endroit (dans son enclos, par exemple), qui resterait
        // sinon éternellement « au travail » sans pouvoir travailler.
        public static bool CanWorkAlone(Pawn beast, TacheTrait task)
        {
            return FindWorkCell(beast, beast, task, out _);
        }

        // Une case vers laquelle le colon mène la bête pour cette tâche : au plus
        // près d'elle et joignable en la menant. Elle re-scanne depuis là une fois
        // lâchée.
        public static bool FindWorkCell(Pawn beast, Pawn handler, TacheTrait task, out IntVec3 cell)
        {
            switch (task)
            {
                case TacheTrait.Labour:
                    return JobGiver_Labourer.FindWorkCell(beast, handler, out cell);
                case TacheTrait.Deneigement:
                    return JobGiver_Deneigeur.FindWorkCell(beast, handler, out cell);
                case TacheTrait.Charrette:
                    return FindCart(beast, handler, out cell);
                default:
                    cell = IntVec3.Invalid;
                    return false;
            }
        }

        private static bool FindCart(Pawn beast, Pawn handler, out IntVec3 cell)
        {
            Thing stack = GenClosest.ClosestThingReachable(
                beast.Position, beast.Map,
                ThingRequest.ForGroup(ThingRequestGroup.HaulableEver),
                PathEndMode.Touch,
                TraverseParms.For(handler, Danger.Some).WithFenceblockedOf(beast), 9999f,
                t => !EquipementUtility.IsEquipment(t.def) && !t.IsForbidden(handler));
            cell = stack != null ? stack.Position : IntVec3.Invalid;
            return stack != null;
        }
    }
}
