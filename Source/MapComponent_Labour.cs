using System.Collections.Generic;
using RimWorld;
using Verse;

namespace AnimalsAtWork.Plowing
{
    // Suit les cases labourées et les rend à leur terrain d'origine après une
    // saison. Instancié automatiquement par RimWorld pour chaque carte, et
    // sauvegardé avec elle via ExposeData.
    public class MapComponent_Labour : MapComponent
    {
        private const int PlowDurationTicks = 900000; // 15 jours = 1 saison
        private const int CheckIntervalTicks = 2000;

        private List<IntVec3> cells = new List<IntVec3>();
        private List<int> expirations = new List<int>();
        private List<TerrainDef> originalTerrains = new List<TerrainDef>();
        // Zones de culture où le joueur a coupé le labour (gizmo sur la zone).
        // Par ID : les zones supprimées laissent un ID orphelin, sans effet.
        private HashSet<int> noPlowZones = new HashSet<int>();
        // Tâches que le joueur a activées, par bête (interrupteurs sur la bête).
        // Choix explicite (opt-in) : une bête absente n'est pas une bête de
        // trait et ne travaille pas. On ne stocke jamais TacheTrait.Aucune.
        // Par référence de pawn ; sauvegardé via deux listes parallèles (plus bas).
        private Dictionary<Pawn, TacheTrait> enabledTasks = new Dictionary<Pawn, TacheTrait>();
        private List<Pawn> beastsScribe;
        private List<TacheTrait> tasksScribe;
        // Bêtes actuellement en service : équipées par un colon puis menées au
        // champ, elles y travaillent seules. Cet état les dispense de l'enclos
        // et les empêche de brouter les récoltes (patchs Harmony), jusqu'à ce
        // qu'un colon les ramène. Sauvegardé via liste (plus bas).
        private HashSet<Pawn> beastsOnDuty = new HashSet<Pawn>();
        private List<Pawn> dutyBeastsScribe;

        public MapComponent_Labour(Map map) : base(map)
        {
        }

        // Map.GetComponent parcourt la liste des composants de la carte, et les
        // patches de service le demandent à chaque lecture de Pawn.Roamer ou
        // Pawn.FenceBlocked. Les lectures venant par rafales sur une même
        // carte, une seule entrée mémorisée suffit.
        private static Map lastMap;
        private static MapComponent_Labour lastComponent;

        public static MapComponent_Labour Of(Map map)
        {
            if (map == null)
            {
                return null;
            }
            if (map != lastMap)
            {
                lastMap = map;
                lastComponent = map.GetComponent<MapComponent_Labour>();
            }
            return lastComponent;
        }

        public bool PlowingAllowed(Zone zone)
        {
            return !noPlowZones.Contains(zone.ID);
        }

        public void TogglePlowing(Zone zone)
        {
            if (!noPlowZones.Add(zone.ID))
            {
                noPlowZones.Remove(zone.ID);
            }
        }

        // Opt-in : cette bête ne fait la tâche que si le joueur l'a explicitement
        // activée (interrupteur allumé). Défaut : rien, elle ne travaille pas.
        public bool TaskAllowed(Pawn beast, TacheTrait task)
        {
            return enabledTasks.TryGetValue(beast, out TacheTrait enabled)
                && (enabled & task) != 0;
        }

        // Une bête compte comme bête de trait dès qu'au moins une tâche lui est
        // activée. Sert à la sélection (« pas tous les animaux »).
        public bool IsDraftBeast(Pawn beast)
        {
            return enabledTasks.ContainsKey(beast);
        }

        public void ToggleTask(Pawn beast, TacheTrait task)
        {
            enabledTasks.TryGetValue(beast, out TacheTrait enabled);
            enabled ^= task;
            if (enabled == TacheTrait.Aucune)
            {
                enabledTasks.Remove(beast);
            }
            else
            {
                enabledTasks[beast] = enabled;
            }
        }

        // État « en service » : posé quand un colon lâche la bête au champ,
        // retiré quand un colon l'a ramenée à l'enclos et déséquipée.
        public bool IsOnDuty(Pawn beast)
        {
            return beastsOnDuty.Contains(beast);
        }

        public void BeginService(Pawn beast)
        {
            beastsOnDuty.Add(beast);
        }

        public void EndService(Pawn beast)
        {
            beastsOnDuty.Remove(beast);
            ForgetFurrow(beast);
        }

        // Suivi du sillon en cours de chaque laboureuse (transitoire, jamais
        // sauvegardé) : dernière case retournée et direction du sillon, pour
        // labourer en lignes droites plutôt qu'au hasard.
        private readonly Dictionary<Pawn, IntVec3> furrowCell = new Dictionary<Pawn, IntVec3>();
        private readonly Dictionary<Pawn, IntVec3> furrowDir = new Dictionary<Pawn, IntVec3>();

        public bool InFurrow(Pawn beast, out IntVec3 last, out IntVec3 direction)
        {
            if (furrowCell.TryGetValue(beast, out last)
                && furrowDir.TryGetValue(beast, out direction))
            {
                return true;
            }
            last = IntVec3.Invalid;
            direction = IntVec3.Invalid;
            return false;
        }

        public void NoteFurrow(Pawn beast, IntVec3 cell, IntVec3 direction)
        {
            furrowCell[beast] = cell;
            furrowDir[beast] = direction;
        }

        public void ForgetFurrow(Pawn beast)
        {
            furrowCell.Remove(beast);
            furrowDir.Remove(beast);
        }

        public void RecordPlowing(IntVec3 cell, TerrainDef terrainBefore)
        {
            cells.Add(cell);
            expirations.Add(Find.TickManager.TicksGame + PlowDurationTicks);
            originalTerrains.Add(terrainBefore);
        }

        // Cache des scans « du travail attend-il ? » : chaque bête de trait
        // les redemandait à chaque décision, et le pire cas est l'état
        // durable (zones gelées en hiver → parcours complet avec température
        // par case). Rafraîchis au plus toutes les ~4 s. Le déneigement loge
        // ici aussi : même carte, même cadence, pas de composant dédié pour
        // deux booléens. État transitoire, volontairement pas sauvegardé.
        private const int ScanCacheDuration = 250;
        private int plowScanTick = int.MinValue;
        private bool plowPending;
        private int snowScanTick = int.MinValue;
        private bool snowPending;

        public bool PlowWorkPending()
        {
            if (CacheStale(plowScanTick))
            {
                plowScanTick = Find.TickManager.TicksGame;
                plowPending = JobGiver_Labourer.WorkExists(map, this);
            }
            return plowPending;
        }

        public bool SnowWorkPending()
        {
            if (CacheStale(snowScanTick))
            {
                snowScanTick = Find.TickManager.TicksGame;
                snowPending = JobGiver_Deneigeur.WorkExists(map);
            }
            return snowPending;
        }

        // Jamais scanné (sentinelle), périmé, ou horloge revenue en arrière
        // (rechargement d'une partie plus ancienne) : dans tous ces cas, on
        // refait le scan. La sentinelle est traitée à part : « tick - int.MinValue »
        // déborde et retomberait négatif, ce qui empêcherait le tout premier scan.
        private static bool CacheStale(int scanTick)
        {
            if (scanTick == int.MinValue)
            {
                return true;
            }
            int tick = Find.TickManager.TicksGame;
            return tick < scanTick || tick - scanTick >= ScanCacheDuration;
        }

        // Cadence maximale du scan des animaux meneurs (JobGiver_Meneur), par
        // meneur : ils sont naturellement décalés entre eux par le tick courant.
        // Rien de ce qu'ils décident ne demande une réaction immédiate, et un
        // chien oisif repasse par son arbre de pensée bien plus souvent que ça.
        private const int HandlerInterval = 150;
        // Passé ce délai sans mise à jour, l'entrée d'un meneur disparu (mort,
        // vendu, dressage perdu) est purgée. État transitoire, jamais sauvegardé.
        private const int HandlerScanTtl = 60000;
        private readonly Dictionary<int, int> lastHandlerScan = new Dictionary<int, int>();
        // Répit après un retour à l'enclos : ce laps de temps sans qu'un meneur
        // ne ressorte cette bête. Voir ServiceTrait.ServiceJob : sans lui, une
        // bête qui garde son attelage repart dès que le travail de charrette
        // repasse son seuil, ce qui arrive sans cesse. Une heure de jeu.
        // État transitoire, jamais sauvegardé : au pire, une bête ressort une
        // fois tout de suite après le chargement d'une partie.
        private const int ReturnGrace = 2500;
        private readonly Dictionary<int, int> penReturns = new Dictionary<int, int>();
        // Répit après un déversement : ce laps de temps sans nouvelle tournée
        // pour cette bête. Une charrette qui n'a rien trouvé où livrer vide son
        // chargement par terre ; sans ce répit, la tournée suivante reprend
        // aussitôt les mêmes piles (à distance nulle, donc les premières
        // trouvées) pour les redéverser, et ainsi de suite. La boucle use la
        // charrette à chaque pile hissée et finit par la détruire.
        // Voir JobGiver_Charretier. Même durée et même nature transitoire.
        private const int DumpGrace = 2500;
        private readonly Dictionary<int, int> dumps = new Dictionary<int, int>();
        private static readonly List<int> toPurge = new List<int>();

        // Cette bête vient d'être ramenée à l'enclos par un meneur.
        public void NoteReturn(Pawn beast)
        {
            penReturns[beast.thingIDNumber] = Find.TickManager.TicksGame;
        }

        public bool InReturnGrace(Pawn beast)
        {
            return InGrace(penReturns, beast, ReturnGrace);
        }

        // Cette bête vient de vider sa charrette faute de rangement.
        public void NoteDump(Pawn beast)
        {
            dumps[beast.thingIDNumber] = Find.TickManager.TicksGame;
        }

        public bool InDumpGrace(Pawn beast)
        {
            return InGrace(dumps, beast, DumpGrace);
        }

        private static bool InGrace(Dictionary<int, int> registry, Pawn beast, int grace)
        {
            int tick = Find.TickManager.TicksGame;
            // dernier <= tick : garde contre une horloge revenue en arrière.
            return registry.TryGetValue(beast.thingIDNumber, out int last)
                && last <= tick && tick - last < grace;
        }

        // Renvoie true (et note le tick) si ce meneur peut relancer son scan,
        // false sinon : le JobGiver rend alors la main au reste de l'arbre.
        public bool HandlerMayScan(Pawn handler)
        {
            int tick = Find.TickManager.TicksGame;
            // dernier <= tick garde contre le rechargement d'une partie plus
            // ancienne (l'horloge recule) : dans ce cas, on relance le scan.
            if (lastHandlerScan.TryGetValue(handler.thingIDNumber, out int last)
                && last <= tick && tick - last < HandlerInterval)
            {
                return false;
            }
            lastHandlerScan[handler.thingIDNumber] = tick;
            return true;
        }

        // Sans ça les dictionnaires enfleraient sur toute la partie, au fil des
        // meneurs et des bêtes croisés.
        private void PurgeRegistries(int tick)
        {
            PurgeRegistry(lastHandlerScan, tick, HandlerScanTtl);
            PurgeRegistry(penReturns, tick, ReturnGrace);
            PurgeRegistry(dumps, tick, DumpGrace);
        }

        private static void PurgeRegistry(Dictionary<int, int> registry, int tick, int ttl)
        {
            toPurge.Clear();
            foreach (KeyValuePair<int, int> entree in registry)
            {
                // Périmée, ou horloge revenue en arrière (rechargement).
                if (tick < entree.Value || tick - entree.Value >= ttl)
                {
                    toPurge.Add(entree.Key);
                }
            }
            for (int i = 0; i < toPurge.Count; i++)
            {
                registry.Remove(toPurge[i]);
            }
            toPurge.Clear();
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % CheckIntervalTicks != 0)
            {
                return;
            }
            int now = Find.TickManager.TicksGame;
            PurgeRegistries(now);
            for (int i = cells.Count - 1; i >= 0; i--)
            {
                if (now < expirations[i])
                {
                    continue;
                }
                IntVec3 cell = cells[i];
                // Ne restaure que si la case est toujours labourée (le joueur a pu
                // construire un plancher par-dessus entre-temps).
                if (cell.GetTerrain(map) == AAW_DefOf.AAW_SolLaboure)
                {
                    map.terrainGrid.SetTerrain(cell, originalTerrains[i] ?? TerrainDefOf.Soil);
                }
                cells.RemoveAt(i);
                expirations.RemoveAt(i);
                originalTerrains.RemoveAt(i);
            }
        }

        // Destroyed couvre aussi Discarded (le pion retiré du jeu pour de bon).
        private static bool Savable(Pawn beast)
        {
            return beast != null && !beast.Destroyed;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref cells, "AAW_casesLabourees", LookMode.Value);
            Scribe_Collections.Look(ref expirations, "AAW_expirations", LookMode.Value);
            Scribe_Collections.Look(ref originalTerrains, "AAW_terrainsOrigine", LookMode.Def);
            Scribe_Collections.Look(ref noPlowZones, "AAW_zonesSansLabour", LookMode.Value);
            // Le dictionnaire des tâches activées se sauvegarde en deux listes
            // parallèles (comme cases/expirations) : rebâtir à la main écarte
            // proprement les clés pointant vers une bête disparue. Une bête
            // détruite (cadavre disparu, vendue puis purgée des pions du monde)
            // ne s'écrit pas : sa référence ne se résoudrait plus au chargement
            // et le jeu s'en plaindrait dans le log.
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                beastsScribe = new List<Pawn>();
                tasksScribe = new List<TacheTrait>();
                foreach (KeyValuePair<Pawn, TacheTrait> paire in enabledTasks)
                {
                    if (Savable(paire.Key))
                    {
                        beastsScribe.Add(paire.Key);
                        tasksScribe.Add(paire.Value);
                    }
                }
                dutyBeastsScribe = new List<Pawn>();
                foreach (Pawn beast in beastsOnDuty)
                {
                    if (Savable(beast))
                    {
                        dutyBeastsScribe.Add(beast);
                    }
                }
            }
            Scribe_Collections.Look(ref beastsScribe, "AAW_betesTaches", LookMode.Reference);
            Scribe_Collections.Look(ref tasksScribe, "AAW_tachesActivees", LookMode.Value);
            Scribe_Collections.Look(ref dutyBeastsScribe, "AAW_betesEnService", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (cells == null) cells = new List<IntVec3>();
                if (expirations == null) expirations = new List<int>();
                if (originalTerrains == null) originalTerrains = new List<TerrainDef>();
                if (noPlowZones == null) noPlowZones = new HashSet<int>();
                enabledTasks = new Dictionary<Pawn, TacheTrait>();
                if (beastsScribe != null && tasksScribe != null)
                {
                    for (int i = 0; i < beastsScribe.Count && i < tasksScribe.Count; i++)
                    {
                        // Bête disparue (référence null) : on l'ignore.
                        if (beastsScribe[i] != null && tasksScribe[i] != TacheTrait.Aucune)
                        {
                            enabledTasks[beastsScribe[i]] = tasksScribe[i];
                        }
                    }
                }
                beastsOnDuty = new HashSet<Pawn>();
                if (dutyBeastsScribe != null)
                {
                    foreach (Pawn beast in dutyBeastsScribe)
                    {
                        if (beast != null)
                        {
                            beastsOnDuty.Add(beast);
                        }
                    }
                }
                beastsScribe = null;
                tasksScribe = null;
                dutyBeastsScribe = null;
            }
        }
    }
}
