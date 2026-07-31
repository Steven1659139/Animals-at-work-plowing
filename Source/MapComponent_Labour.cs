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
        // Tâches que le joueur a activées, par bête (interrupteurs sur la bête).
        // Choix explicite (opt-in) : une bête absente n'est pas une bête de
        // trait et ne travaille pas. On ne stocke jamais TacheTrait.Aucune.
        // Par référence de pawn ; sauvegardé via deux listes parallèles (plus bas).
        private Dictionary<Pawn, TacheTrait> tachesActivees = new Dictionary<Pawn, TacheTrait>();
        private List<Pawn> betesScribe;
        private List<TacheTrait> tachesScribe;
        // Bêtes actuellement en service : équipées par un colon puis menées au
        // champ, elles y travaillent seules. Cet état les dispense de l'enclos
        // et les empêche de brouter les récoltes (patchs Harmony), jusqu'à ce
        // qu'un colon les ramène. Sauvegardé via liste (plus bas).
        private HashSet<Pawn> betesEnService = new HashSet<Pawn>();
        private List<Pawn> betesServiceScribe;

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

        // Opt-in : cette bête ne fait la tâche que si le joueur l'a explicitement
        // activée (interrupteur allumé). Défaut : rien, elle ne travaille pas.
        public bool TacheAutorisee(Pawn bete, TacheTrait tache)
        {
            return tachesActivees.TryGetValue(bete, out TacheTrait activees)
                && (activees & tache) != 0;
        }

        // Une bête compte comme bête de trait dès qu'au moins une tâche lui est
        // activée. Sert à la sélection (« pas tous les animaux »).
        public bool EstBeteDeTrait(Pawn bete)
        {
            return tachesActivees.ContainsKey(bete);
        }

        public void BasculerTache(Pawn bete, TacheTrait tache)
        {
            tachesActivees.TryGetValue(bete, out TacheTrait activees);
            activees ^= tache;
            if (activees == TacheTrait.Aucune)
            {
                tachesActivees.Remove(bete);
            }
            else
            {
                tachesActivees[bete] = activees;
            }
        }

        // État « en service » : posé quand un colon lâche la bête au champ,
        // retiré quand un colon l'a ramenée à l'enclos et déséquipée.
        public bool EstEnService(Pawn bete)
        {
            return betesEnService.Contains(bete);
        }

        public void DebutService(Pawn bete)
        {
            betesEnService.Add(bete);
        }

        public void FinService(Pawn bete)
        {
            betesEnService.Remove(bete);
            OublierSillon(bete);
        }

        // Suivi du sillon en cours de chaque laboureuse (transitoire, jamais
        // sauvegardé) : dernière case retournée et direction du sillon, pour
        // labourer en lignes droites plutôt qu'au hasard.
        private readonly Dictionary<Pawn, IntVec3> sillonCase = new Dictionary<Pawn, IntVec3>();
        private readonly Dictionary<Pawn, IntVec3> sillonDir = new Dictionary<Pawn, IntVec3>();

        public bool EnSillon(Pawn bete, out IntVec3 derniere, out IntVec3 direction)
        {
            if (sillonCase.TryGetValue(bete, out derniere)
                && sillonDir.TryGetValue(bete, out direction))
            {
                return true;
            }
            derniere = IntVec3.Invalid;
            direction = IntVec3.Invalid;
            return false;
        }

        public void NoterSillon(Pawn bete, IntVec3 cellule, IntVec3 direction)
        {
            sillonCase[bete] = cellule;
            sillonDir[bete] = direction;
        }

        public void OublierSillon(Pawn bete)
        {
            sillonCase.Remove(bete);
            sillonDir.Remove(bete);
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

        // Jamais scanné (sentinelle), périmé, ou horloge revenue en arrière
        // (rechargement d'une partie plus ancienne) : dans tous ces cas, on
        // refait le scan. La sentinelle est traitée à part : « tick - int.MinValue »
        // déborde et retomberait négatif, ce qui empêcherait le tout premier scan.
        private static bool CachePerime(int tickScan)
        {
            if (tickScan == int.MinValue)
            {
                return true;
            }
            int tick = Find.TickManager.TicksGame;
            return tick < tickScan || tick - tickScan >= DureeCacheScans;
        }

        // Cadence maximale du scan des animaux meneurs (JobGiver_Meneur), par
        // meneur : ils sont naturellement décalés entre eux par le tick courant.
        // Rien de ce qu'ils décident ne demande une réaction immédiate, et un
        // chien oisif repasse par son arbre de pensée bien plus souvent que ça.
        private const int IntervalleMeneur = 150;
        // Passé ce délai sans mise à jour, l'entrée d'un meneur disparu (mort,
        // vendu, dressage perdu) est purgée. État transitoire, jamais sauvegardé.
        private const int TtlScanMeneur = 60000;
        private readonly Dictionary<int, int> dernierScanMeneur = new Dictionary<int, int>();
        private static readonly List<int> aPurger = new List<int>();

        // Renvoie true (et note le tick) si ce meneur peut relancer son scan,
        // false sinon — le JobGiver rend alors la main au reste de l'arbre.
        public bool PeutScannerMeneur(Pawn meneur)
        {
            int tick = Find.TickManager.TicksGame;
            // dernier <= tick garde contre le rechargement d'une partie plus
            // ancienne (l'horloge recule) : dans ce cas, on relance le scan.
            if (dernierScanMeneur.TryGetValue(meneur.thingIDNumber, out int dernier)
                && dernier <= tick && tick - dernier < IntervalleMeneur)
            {
                return false;
            }
            dernierScanMeneur[meneur.thingIDNumber] = tick;
            return true;
        }

        // Sans ça le dictionnaire enflerait sur toute la partie, au fil des
        // meneurs croisés.
        private void PurgerScansMeneur(int tick)
        {
            aPurger.Clear();
            foreach (KeyValuePair<int, int> entree in dernierScanMeneur)
            {
                // Périmée, ou horloge revenue en arrière (rechargement).
                if (tick < entree.Value || tick - entree.Value >= TtlScanMeneur)
                {
                    aPurger.Add(entree.Key);
                }
            }
            for (int i = 0; i < aPurger.Count; i++)
            {
                dernierScanMeneur.Remove(aPurger[i]);
            }
            aPurger.Clear();
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % IntervalleVerifTicks != 0)
            {
                return;
            }
            int maintenant = Find.TickManager.TicksGame;
            PurgerScansMeneur(maintenant);
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
            // Le dictionnaire des tâches activées se sauvegarde en deux listes
            // parallèles (comme cases/expirations) : rebâtir à la main écarte
            // proprement les clés pointant vers une bête disparue (null).
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                betesScribe = new List<Pawn>();
                tachesScribe = new List<TacheTrait>();
                foreach (KeyValuePair<Pawn, TacheTrait> paire in tachesActivees)
                {
                    betesScribe.Add(paire.Key);
                    tachesScribe.Add(paire.Value);
                }
                // Même précaution pour les bêtes en service disparues (null).
                betesServiceScribe = new List<Pawn>();
                foreach (Pawn bete in betesEnService)
                {
                    if (bete != null)
                    {
                        betesServiceScribe.Add(bete);
                    }
                }
            }
            Scribe_Collections.Look(ref betesScribe, "AAW_betesTaches", LookMode.Reference);
            Scribe_Collections.Look(ref tachesScribe, "AAW_tachesActivees", LookMode.Value);
            Scribe_Collections.Look(ref betesServiceScribe, "AAW_betesEnService", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (cases == null) cases = new List<IntVec3>();
                if (expirations == null) expirations = new List<int>();
                if (terrainsOrigine == null) terrainsOrigine = new List<TerrainDef>();
                if (zonesSansLabour == null) zonesSansLabour = new HashSet<int>();
                tachesActivees = new Dictionary<Pawn, TacheTrait>();
                if (betesScribe != null && tachesScribe != null)
                {
                    for (int i = 0; i < betesScribe.Count && i < tachesScribe.Count; i++)
                    {
                        // Bête disparue (référence null) : on l'ignore.
                        if (betesScribe[i] != null && tachesScribe[i] != TacheTrait.Aucune)
                        {
                            tachesActivees[betesScribe[i]] = tachesScribe[i];
                        }
                    }
                }
                betesEnService = new HashSet<Pawn>();
                if (betesServiceScribe != null)
                {
                    foreach (Pawn bete in betesServiceScribe)
                    {
                        if (bete != null)
                        {
                            betesEnService.Add(bete);
                        }
                    }
                }
                betesScribe = null;
                tachesScribe = null;
                betesServiceScribe = null;
            }
        }
    }
}
