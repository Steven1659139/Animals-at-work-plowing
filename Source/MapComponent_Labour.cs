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
        private const int DureeLabourTicks = 900000; // 15 jours = 1 saison
        private const int IntervalleVerifTicks = 2000;

        private List<IntVec3> cases = new List<IntVec3>();
        private List<int> expirations = new List<int>();
        private List<TerrainDef> terrainsOrigine = new List<TerrainDef>();
        // Zones de culture où le joueur a coupé le labour (gizmo sur la zone).
        // Par ID : les zones supprimées laissent un ID orphelin, sans effet.
        private HashSet<int> zonesSansLabour = new HashSet<int>();

        public MapComponent_Labour(Map map) : base(map)
        {
        }

        public bool LabourAutorise(Zone zone)
        {
            return !zonesSansLabour.Contains(zone.ID);
        }

        public void BasculerLabour(Zone zone)
        {
            if (!zonesSansLabour.Add(zone.ID))
            {
                zonesSansLabour.Remove(zone.ID);
            }
        }

        public void EnregistrerLabour(IntVec3 cellule, TerrainDef terrainAvant)
        {
            cases.Add(cellule);
            expirations.Add(Find.TickManager.TicksGame + DureeLabourTicks);
            terrainsOrigine.Add(terrainAvant);
        }

        // Cache des scans « du travail attend-il ? » : chaque bête de trait
        // les redemandait à chaque décision, et le pire cas est l'état
        // durable (zones gelées en hiver → parcours complet avec température
        // par case). Rafraîchis au plus toutes les ~4 s. Le déneigement loge
        // ici aussi : même carte, même cadence, pas de composant dédié pour
        // deux booléens. État transitoire, volontairement pas sauvegardé.
        private const int DureeCacheScans = 250;
        private int tickScanLabour = int.MinValue;
        private bool labourEnAttente;
        private int tickScanNeige = int.MinValue;
        private bool neigeEnAttente;

        public bool TravailLabourEnAttente()
        {
            if (CachePerime(tickScanLabour))
            {
                tickScanLabour = Find.TickManager.TicksGame;
                labourEnAttente = JobGiver_Labourer.TravailExiste(map, this);
            }
            return labourEnAttente;
        }

        public bool TravailDeneigementEnAttente()
        {
            if (CachePerime(tickScanNeige))
            {
                tickScanNeige = Find.TickManager.TicksGame;
                neigeEnAttente = JobGiver_Deneigeur.TravailExiste(map);
            }
            return neigeEnAttente;
        }

        // Périmé, ou horloge revenue en arrière (rechargement d'une partie
        // plus ancienne) : dans les deux cas, on refait le scan.
        private static bool CachePerime(int tickScan)
        {
            int tick = Find.TickManager.TicksGame;
            return tick < tickScan || tick - tickScan >= DureeCacheScans;
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % IntervalleVerifTicks != 0)
            {
                return;
            }
            int maintenant = Find.TickManager.TicksGame;
            for (int i = cases.Count - 1; i >= 0; i--)
            {
                if (maintenant < expirations[i])
                {
                    continue;
                }
                IntVec3 cellule = cases[i];
                // Ne restaure que si la case est toujours labourée (le joueur a pu
                // construire un plancher par-dessus entre-temps).
                if (cellule.GetTerrain(map) == AAW_DefOf.AAW_SolLaboure)
                {
                    map.terrainGrid.SetTerrain(cellule, terrainsOrigine[i] ?? TerrainDefOf.Soil);
                }
                cases.RemoveAt(i);
                expirations.RemoveAt(i);
                terrainsOrigine.RemoveAt(i);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref cases, "AAW_casesLabourees", LookMode.Value);
            Scribe_Collections.Look(ref expirations, "AAW_expirations", LookMode.Value);
            Scribe_Collections.Look(ref terrainsOrigine, "AAW_terrainsOrigine", LookMode.Def);
            Scribe_Collections.Look(ref zonesSansLabour, "AAW_zonesSansLabour", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (cases == null) cases = new List<IntVec3>();
                if (expirations == null) expirations = new List<int>();
                if (terrainsOrigine == null) terrainsOrigine = new List<TerrainDef>();
                if (zonesSansLabour == null) zonesSansLabour = new HashSet<int>();
            }
        }
    }
}
