using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AnimalsAtWork.Plowing
{
    public class PlowingSettings : ModSettings
    {
        // On ne mémorise que les écarts au défaut : les espèces que le joueur a
        // ajoutées alors que rien ne les désignait, et celles qu'il a retirées
        // alors que tout les désignait. Une liste complète se serait périmée au
        // premier mod activé ou désactivé.
        //
        // Par defName et non par def : un mod retiré puis remis ne doit pas
        // faire perdre le réglage, et une def absente au chargement ne doit
        // rien casser.
        public List<string> addedSpecies = new List<string>();
        public List<string> removedSpecies = new List<string>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref addedSpecies, "AAW_especesDeTrait", LookMode.Value);
            Scribe_Collections.Look(ref removedSpecies, "AAW_especesRetirees", LookMode.Value);
            if (Scribe.mode != LoadSaveMode.PostLoadInit)
            {
                return;
            }
            if (addedSpecies == null)
            {
                addedSpecies = new List<string>();
            }
            if (removedSpecies == null)
            {
                removedSpecies = new List<string>();
            }
        }
    }

    public class PlowingMod : Mod
    {
        private static PlowingSettings settings;
        // Doublons en ensembles : la question « cette espèce est-elle de trait ? »
        // est posée par les arbres de pensée à chaque décision de chaque bête.
        private static HashSet<string> addedFast = new HashSet<string>();
        private static HashSet<string> removedFast = new HashSet<string>();

        private Vector2 scroll;
        private string searchText = "";
        private bool harnessableOnly;
        private List<ThingDef> animals;
        private readonly List<ThingDef> visible = new List<ThingDef>();

        public PlowingMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<PlowingSettings>();
            Reindex();
        }

        public static bool SpeciesAdded(ThingDef def)
        {
            return addedFast.Count > 0 && addedFast.Contains(def.defName);
        }

        public static bool SpeciesRemoved(ThingDef def)
        {
            return removedFast.Count > 0 && removedFast.Contains(def.defName);
        }

        private static void Reindex()
        {
            addedFast = settings?.addedSpecies != null
                ? new HashSet<string>(settings.addedSpecies)
                : new HashSet<string>();
            removedFast = settings?.removedSpecies != null
                ? new HashSet<string>(settings.removedSpecies)
                : new HashSet<string>();
        }

        public override string SettingsCategory()
        {
            return "Animals at Work — Plowing";
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            Reindex();
        }

        private const float HauteurLigne = 30f;

        public override void DoSettingsWindowContents(Rect inRect)
        {
            BuildList();

            float y = inRect.y;

            // Explication du réglage, sur autant de lignes qu'il en faut.
            string explanation = "AAW_ReglageEspeces".Translate();
            float textHeight = Text.CalcHeight(explanation, inRect.width);
            Widgets.Label(new Rect(inRect.x, y, inRect.width, textHeight), explanation);
            y += textHeight + 10f;

            // Ligne de filtres : recherche à gauche, « harnachables seules » à droite.
            Rect filterLine = new Rect(inRect.x, y, inRect.width, 28f);
            float cellWidth = 200f;
            // La croix se tient hors du champ, jamais par-dessus : le TextField
            // est dessiné avant elle et consommerait le clic, la laissant inerte.
            Rect searchRect = new Rect(filterLine.x, filterLine.y,
                filterLine.width - cellWidth - 10f, filterLine.height);
            Rect field = new Rect(searchRect.x, searchRect.y,
                searchRect.width - 28f, searchRect.height);
            Rect cross = new Rect(searchRect.xMax - 22f, searchRect.y + 5f, 18f, 18f);
            searchText = Widgets.TextField(field, searchText);
            if (searchText.NullOrEmpty())
            {
                // Invite en filigrane : sans elle, le champ vide ne dit pas à
                // quoi il sert. Décalée du bord comme le texte saisi.
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                Widgets.Label(new Rect(field.x + 6f, field.y, field.width - 12f, field.height),
                    "AAW_ReglageRecherche".Translate());
                GUI.color = Color.white;
            }
            else if (Widgets.ButtonImage(cross, TexButton.CloseXSmall))
            {
                searchText = "";
                // Tant que le champ garde le focus clavier, son éditeur réaffiche
                // son propre contenu et la remise à vide passe inaperçue.
                UI.UnfocusCurrentControl();
            }
            Widgets.CheckboxLabeled(
                new Rect(filterLine.xMax - cellWidth, filterLine.y, cellWidth, filterLine.height),
                "AAW_ReglageHarnachablesSeules".Translate(), ref harnessableOnly);
            y += filterLine.height + 6f;

            FilterList();

            // Ligne de bilan : combien d'espèces cochées, et de quoi tout remettre
            // au défaut : un joueur qui s'est perdu dans ses coches n'a sinon aucun
            // moyen de retrouver l'état de départ.
            Rect summaryLine = new Rect(inRect.x, y, inRect.width, 28f);
            Rect button = new Rect(summaryLine.xMax - 160f, summaryLine.y, 160f, summaryLine.height);
            int total = 0;
            for (int i = 0; i < animals.Count; i++)
            {
                if (BeteDeTrait.Is(animals[i]))
                {
                    total++;
                }
            }
            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(summaryLine.x, summaryLine.y + 4f, summaryLine.width - 170f, summaryLine.height),
                "AAW_ReglageCompte".Translate(total, visible.Count));
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            bool atDefault = settings.addedSpecies.Count == 0 && settings.removedSpecies.Count == 0;
            TooltipHandler.TipRegion(button, "AAW_ReglageReinitDesc".Translate());
            if (Widgets.ButtonText(button, "AAW_ReglageReinit".Translate(), active: !atDefault))
            {
                settings.addedSpecies.Clear();
                settings.removedSpecies.Clear();
                Reindex();
            }
            y += summaryLine.height + 6f;

            Widgets.DrawLineHorizontal(inRect.x, y, inRect.width);
            y += 6f;

            Rect frame = new Rect(inRect.x, y, inRect.width, inRect.yMax - y);
            Rect contents = new Rect(0f, 0f, frame.width - 20f, visible.Count * HauteurLigne);
            Widgets.BeginScrollView(frame, ref scroll, contents);
            // On ne dessine que les lignes réellement à l'écran : la liste suit le
            // nombre d'espèces installées, et un gros pack animalier en aligne
            // plusieurs centaines.
            int first = Mathf.Max(0, (int)(scroll.y / HauteurLigne) - 1);
            int last = Mathf.Min(visible.Count,
                first + (int)(frame.height / HauteurLigne) + 3);
            for (int i = first; i < last; i++)
            {
                DrawLine(new Rect(0f, i * HauteurLigne, contents.width, HauteurLigne), visible[i], i);
            }
            Widgets.EndScrollView();
        }

        // La base de défs n'est pas prête à la construction du mod : on dresse la
        // liste au premier affichage de la fenêtre. L'ordre est figé une fois pour
        // toutes : un tri qui suivrait l'éligibilité ferait sauter les lignes sous
        // le curseur à chaque clic.
        private void BuildList()
        {
            if (animals != null)
            {
                return;
            }
            animals = DefDatabase<ThingDef>.AllDefsListForReading
                // category, et pas seulement race : le jeu engendre une def de
                // cadavre par espèce, qui partage le RaceProperties de la bête
                // vivante (jusqu'à son packAnimal). Sans ce filtre, la liste
                // donnerait « Elephant corpse » comme bête de somme.
                .Where(d => d.category == ThingCategory.Pawn && d.race != null && d.race.Animal)
                .OrderByDescending(d => d.race.baseBodySize)
                .ThenBy(d => d.label)
                .ToList();
            PurgeCorpses();
        }

        // Une def de cadavre cochée n'a jamais rien pu faire : on efface ces
        // choix-là (les réglages d'anciennes versions peuvent en contenir). Une
        // def qu'on ne trouve pas est laissée en place : c'est le cas normal
        // d'un mod temporairement désactivé, et tout le réglage tient à ne pas
        // l'oublier.
        private static void PurgeCorpses()
        {
            int cleared = settings.addedSpecies.RemoveAll(IsCorpse)
                + settings.removedSpecies.RemoveAll(IsCorpse);
            if (cleared > 0)
            {
                Reindex();
            }
        }

        private static bool IsCorpse(string defName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            return def != null && def.category != ThingCategory.Pawn;
        }

        private void FilterList()
        {
            visible.Clear();
            for (int i = 0; i < animals.Count; i++)
            {
                ThingDef species = animals[i];
                if (harnessableOnly && !BeteDeTrait.Is(species))
                {
                    continue;
                }
                if (!searchText.NullOrEmpty()
                    && (species.label == null
                        || species.label.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    continue;
                }
                visible.Add(species);
            }
        }

        private static void DrawLine(Rect line, ThingDef species, int index)
        {
            if (index % 2 == 1)
            {
                Widgets.DrawLightHighlight(line);
            }
            Widgets.DrawHighlightIfMouseover(line);
            TooltipHandler.TipRegion(line, () => Tooltip(species), species.shortHash);

            Rect icon = new Rect(line.x + 2f, line.y + 2f, 26f, 26f);
            PawnKindDef kind = species.race.AnyPawnKind;
            if (kind != null)
            {
                Widgets.DefIcon(icon, kind);
            }

            Rect rest = new Rect(icon.xMax + 6f, line.y + 3f, line.width - icon.width - 8f, 24f);
            bool ticked = BeteDeTrait.Is(species);
            bool before = ticked;
            Widgets.CheckboxLabeled(rest, Label(species), ref ticked);
            if (ticked != before)
            {
                Define(species, ticked);
            }
        }

        // Pourquoi cette ligne est-elle cochée ou non, et le joueur y est-il pour
        // quelque chose ? Le libellé seul ne peut pas le dire.
        private static string Tooltip(ThingDef species)
        {
            string origin;
            if (BeteDeTrait.ByExtension(species))
            {
                origin = "AAW_EspeceTipIntegree".Translate(species.LabelCap);
            }
            else if (BeteDeTrait.ByPackAnimal(species))
            {
                origin = "AAW_EspeceTipBat".Translate(species.LabelCap);
            }
            else
            {
                origin = "AAW_EspeceTipLibre".Translate(species.LabelCap);
            }
            if (SpeciesAdded(species) || SpeciesRemoved(species))
            {
                origin += "\n\n" + "AAW_EspeceTipModifiee".Translate();
            }
            return origin;
        }

        // Le libellé dit d'où vient l'éligibilité, sans quoi une case cochée
        // que le joueur n'a jamais touchée serait inexplicable.
        private static string Label(ThingDef species)
        {
            string size = species.race.baseBodySize.ToString("0.0");
            if (BeteDeTrait.ByExtension(species))
            {
                return "AAW_EspeceIntegree".Translate(species.LabelCap, size);
            }
            if (BeteDeTrait.ByPackAnimal(species))
            {
                return "AAW_EspeceBat".Translate(species.LabelCap, size);
            }
            return "AAW_EspeceLibre".Translate(species.LabelCap, size);
        }

        // On ne retient que l'écart au défaut : cocher une espèce déjà éligible
        // efface simplement son retrait, et la décocher efface son ajout. Les
        // deux listes restent donc courtes et ne se contredisent jamais.
        private static void Define(ThingDef species, bool wantedImplement)
        {
            bool original = BeteDeTrait.IsOriginal(species);
            settings.addedSpecies.Remove(species.defName);
            settings.removedSpecies.Remove(species.defName);
            if (wantedImplement && !original)
            {
                settings.addedSpecies.Add(species.defName);
            }
            else if (!wantedImplement && original)
            {
                settings.removedSpecies.Add(species.defName);
            }
            Reindex();
        }
    }
}
