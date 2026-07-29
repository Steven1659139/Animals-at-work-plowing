using RimWorld;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Cerveau de la bête laboureuse, dans l'arbre de pensée. Ne produit un job
    // que si elle est en service (menée au champ par un colon) et déjà équipée.
    // Elle laboure en sillons droits : chaque case retournée, elle poursuit tout
    // droit tant que la suivante se laboure, sinon elle ouvre un nouveau sillon à
    // la case la plus proche. Retourne null s'il n'y a rien à faire.
    public class JobGiver_Labourer : ThinkNode_JobGiver
    {
        private const float FertiliteMin = 0.9f;
        // En deçà, les plantes vanilla ne poussent plus (Plant.GrowthRateFactor_Temperature).
        private const float TemperatureMin = 0f;

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

            MapComponent_Labour composante = map.GetComponent<MapComponent_Labour>();
            // La bête ne laboure qu'une fois menée au champ par un colon (en
            // service) et déjà équipée du harnais et de la charrue : elle ne
            // s'attelle ni ne sort de l'enclos seule.
            if (!composante.EstEnService(pawn)
                || !composante.TacheAutorisee(pawn, TacheTrait.Labour)
                || EquipementUtility.Porte(pawn, AAW_DefOf.AAW_HarnaisDeTrait) == null
                || EquipementUtility.Porte(pawn, AAW_DefOf.AAW_Charrue) == null)
            {
                return null;
            }
            if (!composante.TravailLabourEnAttente())
            {
                return null;
            }

            IntVec3 cible = ChoisirCase(pawn, map, composante);
            if (!cible.IsValid)
            {
                return null;
            }
            return JobMaker.MakeJob(AAW_DefOf.AAW_Labourer, cible);
        }

        // La prochaine case à labourer, en sillons droits : d'abord tout droit
        // dans le sillon en cours (si la bête est encore dessus et que la case
        // suivante se laboure), sinon la case labourable la plus proche, qui
        // ouvre un nouveau sillon dans la meilleure direction.
        private static IntVec3 ChoisirCase(Pawn pawn, Map map, MapComponent_Labour composante)
        {
            if (composante.EnSillon(pawn, out IntVec3 derniere, out IntVec3 direction)
                && pawn.Position == derniere)
            {
                IntVec3 suite = derniere + direction;
                if (Labourable(suite, map, composante)
                    && pawn.CanReserveAndReach(suite, PathEndMode.OnCell, Danger.Some))
                {
                    composante.NoterSillon(pawn, suite, direction);
                    return suite;
                }
            }

            IntVec3 depart = CaseLabourableLaPlusProche(pawn, true);
            if (!depart.IsValid)
            {
                composante.OublierSillon(pawn);
                return IntVec3.Invalid;
            }
            composante.NoterSillon(pawn, depart, DirectionSillon(depart, map, composante));
            return depart;
        }

        // Cardinaux testés dans cet ordre : les sillons partent horizontaux et
        // serpentent (aller-retour) le long du champ.
        private static readonly IntVec3[] Cardinaux =
            { IntVec3.East, IntVec3.West, IntVec3.North, IntVec3.South };

        // Direction d'un nouveau sillon depuis 'depart' : le premier cardinal
        // dont la case voisine se laboure encore. Est par défaut.
        private static IntVec3 DirectionSillon(IntVec3 depart, Map map, MapComponent_Labour composante)
        {
            foreach (IntVec3 d in Cardinaux)
            {
                if (Labourable(depart + d, map, composante))
                {
                    return d;
                }
            }
            return IntVec3.East;
        }

        // La case se laboure-t-elle, dans une zone de culture où le labour est
        // autorisé ? (Suivi de sillon, case par case.)
        private static bool Labourable(IntVec3 cellule, Map map, MapComponent_Labour composante)
        {
            return cellule.InBounds(map)
                && cellule.GetZone(map) is Zone_Growing zoneCulture
                && composante.LabourAutorise(zoneCulture)
                && CelluleLabourable(cellule, map);
        }

        // Y a-t-il une case à labourer quelque part (réservations mises à
        // part) ? Sert aussi à décider de poser ou de prendre la charrue.
        // Toujours via le cache de MapComponent_Labour, jamais en direct.
        public static bool TravailExiste(Map map, MapComponent_Labour composante)
        {
            foreach (Zone zone in map.zoneManager.AllZones)
            {
                if (!(zone is Zone_Growing zoneCulture) || !composante.LabourAutorise(zoneCulture))
                {
                    continue;
                }
                foreach (IntVec3 cellule in zoneCulture.Cells)
                {
                    if (CelluleLabourable(cellule, map))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // Case labourable la plus proche de 'acteur', atteignable par lui. Sert
        // au colon pour choisir où lâcher la bête (reserver = false : il ne fait
        // que s'y rendre) comme à la bête pour ouvrir un sillon (reserver = true,
        // elle doit pouvoir la réserver). Tests coûteux (atteignabilité) en
        // dernier, seulement pour une case plus proche que la meilleure trouvée.
        public static IntVec3 CaseLabourableLaPlusProche(Pawn acteur, bool reserver)
        {
            Map map = acteur.Map;
            MapComponent_Labour composante = map.GetComponent<MapComponent_Labour>();
            IntVec3 meilleure = IntVec3.Invalid;
            float meilleureDist = float.MaxValue;
            foreach (Zone zone in map.zoneManager.AllZones)
            {
                if (!(zone is Zone_Growing zoneCulture) || !composante.LabourAutorise(zoneCulture))
                {
                    continue;
                }
                foreach (IntVec3 cellule in zoneCulture.Cells)
                {
                    float dist = cellule.DistanceToSquared(acteur.Position);
                    if (dist >= meilleureDist || !CelluleLabourable(cellule, map))
                    {
                        continue;
                    }
                    bool accessible = reserver
                        ? acteur.CanReserveAndReach(cellule, PathEndMode.OnCell, Danger.Some)
                        : acteur.CanReach(cellule, PathEndMode.OnCell, Danger.Some);
                    if (accessible)
                    {
                        meilleure = cellule;
                        meilleureDist = dist;
                    }
                }
            }
            return meilleure;
        }

        // Case vers laquelle le colon mène la bête : la plus proche du colon.
        // La bête re-scanne ensuite depuis là.
        public static bool TrouverCelluleTravail(Pawn reacher, out IntVec3 result)
        {
            result = CaseLabourableLaPlusProche(reacher, false);
            return result.IsValid;
        }

        private static bool CelluleLabourable(IntVec3 cellule, Map map)
        {
            TerrainDef terrain = cellule.GetTerrain(map);
            if (terrain == AAW_DefOf.AAW_SolLaboure)
            {
                return false;
            }
            // Sol cultivable ordinaire uniquement : ni les planchers (fertilité 0),
            // ni le gravier (0.7), ni le sol riche (1.4) qu'on dégraderait.
            if (terrain.fertility < FertiliteMin || terrain.fertility >= AAW_DefOf.AAW_SolLaboure.fertility)
            {
                return false;
            }
            if (!terrain.affordances.Contains(AAW_DefOf.GrowSoil))
            {
                return false;
            }
            // Labourer un sol gelé est du harnais gaspillé : rien n'y poussera
            // avant que la terre ne se tasse. Température par case, pour que
            // les serres chauffées restent labourables en plein hiver.
            if (GenTemperature.GetTemperatureForCell(cellule, map) < TemperatureMin)
            {
                return false;
            }
            return cellule.GetEdifice(map) == null;
        }
    }
}
