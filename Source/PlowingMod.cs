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
        public List<string> especesAjoutees = new List<string>();
        public List<string> especesRetirees = new List<string>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref especesAjoutees, "AAW_especesDeTrait", LookMode.Value);
            Scribe_Collections.Look(ref especesRetirees, "AAW_especesRetirees", LookMode.Value);
            if (Scribe.mode != LoadSaveMode.PostLoadInit)
            {
                return;
            }
            if (especesAjoutees == null)
            {
                especesAjoutees = new List<string>();
            }
            if (especesRetirees == null)
            {
                especesRetirees = new List<string>();
            }
        }
    }

    public class PlowingMod : Mod
    {
        private static PlowingSettings reglages;
        // Doublons en ensembles : la question « cette espèce est-elle de trait ? »
        // est posée par les arbres de pensée à chaque décision de chaque bête.
        private static HashSet<string> ajouteesRapide = new HashSet<string>();
        private static HashSet<string> retireesRapide = new HashSet<string>();

        private Vector2 defilement;
        private string recherche = "";
        private bool harnachablesSeules;
        private List<ThingDef> animaux;
        private readonly List<ThingDef> visibles = new List<ThingDef>();

        public PlowingMod(ModContentPack content) : base(content)
        {
            reglages = GetSettings<PlowingSettings>();
            Reindexer();
        }

        public static bool EspeceAjoutee(ThingDef def)
        {
            return ajouteesRapide.Count > 0 && ajouteesRapide.Contains(def.defName);
        }

        public static bool EspeceRetiree(ThingDef def)
        {
            return retireesRapide.Count > 0 && retireesRapide.Contains(def.defName);
        }

        private static void Reindexer()
        {
            ajouteesRapide = reglages?.especesAjoutees != null
                ? new HashSet<string>(reglages.especesAjoutees)
                : new HashSet<string>();
            retireesRapide = reglages?.especesRetirees != null
                ? new HashSet<string>(reglages.especesRetirees)
                : new HashSet<string>();
        }

        public override string SettingsCategory()
        {
            return "Animals at Work — Plowing";
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            Reindexer();
        }

        private const float HauteurLigne = 30f;

        public override void DoSettingsWindowContents(Rect inRect)
        {
            ConstruireListe();

            float y = inRect.y;

            // Explication du réglage, sur autant de lignes qu'il en faut.
            string explication = "AAW_ReglageEspeces".Translate();
            float hauteurTexte = Text.CalcHeight(explication, inRect.width);
            Widgets.Label(new Rect(inRect.x, y, inRect.width, hauteurTexte), explication);
            y += hauteurTexte + 10f;

            // Ligne de filtres : recherche à gauche, « harnachables seules » à droite.
            Rect ligneFiltres = new Rect(inRect.x, y, inRect.width, 28f);
            float largeurCase = 200f;
            // La croix se tient hors du champ, jamais par-dessus : le TextField
            // est dessiné avant elle et consommerait le clic, la laissant inerte.
            Rect zoneRecherche = new Rect(ligneFiltres.x, ligneFiltres.y,
                ligneFiltres.width - largeurCase - 10f, ligneFiltres.height);
            Rect champ = new Rect(zoneRecherche.x, zoneRecherche.y,
                zoneRecherche.width - 28f, zoneRecherche.height);
            Rect croix = new Rect(zoneRecherche.xMax - 22f, zoneRecherche.y + 5f, 18f, 18f);
            recherche = Widgets.TextField(champ, recherche);
            if (recherche.NullOrEmpty())
            {
                // Invite en filigrane : sans elle, le champ vide ne dit pas à
                // quoi il sert. Décalée du bord comme le texte saisi.
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                Widgets.Label(new Rect(champ.x + 6f, champ.y, champ.width - 12f, champ.height),
                    "AAW_ReglageRecherche".Translate());
                GUI.color = Color.white;
            }
            else if (Widgets.ButtonImage(croix, TexButton.CloseXSmall))
            {
                recherche = "";
                // Tant que le champ garde le focus clavier, son éditeur réaffiche
                // son propre contenu et la remise à vide passe inaperçue.
                UI.UnfocusCurrentControl();
            }
            Widgets.CheckboxLabeled(
                new Rect(ligneFiltres.xMax - largeurCase, ligneFiltres.y, largeurCase, ligneFiltres.height),
                "AAW_ReglageHarnachablesSeules".Translate(), ref harnachablesSeules);
            y += ligneFiltres.height + 6f;

            FiltrerListe();

            // Ligne de bilan : combien d'espèces cochées, et de quoi tout remettre
            // au défaut — un joueur qui s'est perdu dans ses coches n'a sinon aucun
            // moyen de retrouver l'état de départ.
            Rect ligneBilan = new Rect(inRect.x, y, inRect.width, 28f);
            Rect bouton = new Rect(ligneBilan.xMax - 160f, ligneBilan.y, 160f, ligneBilan.height);
            int total = 0;
            for (int i = 0; i < animaux.Count; i++)
            {
                if (BeteDeTrait.Est(animaux[i]))
                {
                    total++;
                }
            }
            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(ligneBilan.x, ligneBilan.y + 4f, ligneBilan.width - 170f, ligneBilan.height),
                "AAW_ReglageCompte".Translate(total, visibles.Count));
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            bool auDefaut = reglages.especesAjoutees.Count == 0 && reglages.especesRetirees.Count == 0;
            TooltipHandler.TipRegion(bouton, "AAW_ReglageReinitDesc".Translate());
            if (Widgets.ButtonText(bouton, "AAW_ReglageReinit".Translate(), active: !auDefaut))
            {
                reglages.especesAjoutees.Clear();
                reglages.especesRetirees.Clear();
                Reindexer();
            }
            y += ligneBilan.height + 6f;

            Widgets.DrawLineHorizontal(inRect.x, y, inRect.width);
            y += 6f;

            Rect cadre = new Rect(inRect.x, y, inRect.width, inRect.yMax - y);
            Rect contenu = new Rect(0f, 0f, cadre.width - 20f, visibles.Count * HauteurLigne);
            Widgets.BeginScrollView(cadre, ref defilement, contenu);
            // On ne dessine que les lignes réellement à l'écran : la liste suit le
            // nombre d'espèces installées, et un gros pack animalier en aligne
            // plusieurs centaines.
            int premiere = Mathf.Max(0, (int)(defilement.y / HauteurLigne) - 1);
            int derniere = Mathf.Min(visibles.Count,
                premiere + (int)(cadre.height / HauteurLigne) + 3);
            for (int i = premiere; i < derniere; i++)
            {
                DessinerLigne(new Rect(0f, i * HauteurLigne, contenu.width, HauteurLigne), visibles[i], i);
            }
            Widgets.EndScrollView();
        }

        // La base de défs n'est pas prête à la construction du mod : on dresse la
        // liste au premier affichage de la fenêtre. L'ordre est figé une fois pour
        // toutes — un tri qui suivrait l'éligibilité ferait sauter les lignes sous
        // le curseur à chaque clic.
        private void ConstruireListe()
        {
            if (animaux != null)
            {
                return;
            }
            animaux = DefDatabase<ThingDef>.AllDefsListForReading
                // category, et pas seulement race : le jeu engendre une def de
                // cadavre par espèce, qui partage le RaceProperties de la bête
                // vivante (jusqu'à son packAnimal). Sans ce filtre, la liste
                // affichait « Elephant corpse — pack animal » sous chaque animal.
                .Where(d => d.category == ThingCategory.Pawn && d.race != null && d.race.Animal)
                .OrderByDescending(d => d.race.baseBodySize)
                .ThenBy(d => d.label)
                .ToList();
            PurgerCadavres();
        }

        // Les défs de cadavre étant listées jusqu'ici, elles étaient cochables :
        // on efface ces choix-là, qui n'ont jamais rien pu faire. Une def qu'on ne
        // trouve pas est laissée en place — c'est le cas normal d'un mod
        // temporairement désactivé, et tout le réglage tient à ne pas l'oublier.
        private static void PurgerCadavres()
        {
            int efface = reglages.especesAjoutees.RemoveAll(EstCadavre)
                + reglages.especesRetirees.RemoveAll(EstCadavre);
            if (efface > 0)
            {
                Reindexer();
            }
        }

        private static bool EstCadavre(string defName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            return def != null && def.category != ThingCategory.Pawn;
        }

        private void FiltrerListe()
        {
            visibles.Clear();
            for (int i = 0; i < animaux.Count; i++)
            {
                ThingDef espece = animaux[i];
                if (harnachablesSeules && !BeteDeTrait.Est(espece))
                {
                    continue;
                }
                if (!recherche.NullOrEmpty()
                    && (espece.label == null
                        || espece.label.IndexOf(recherche, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    continue;
                }
                visibles.Add(espece);
            }
        }

        private static void DessinerLigne(Rect ligne, ThingDef espece, int index)
        {
            if (index % 2 == 1)
            {
                Widgets.DrawLightHighlight(ligne);
            }
            Widgets.DrawHighlightIfMouseover(ligne);
            TooltipHandler.TipRegion(ligne, () => Infobulle(espece), espece.shortHash);

            Rect icone = new Rect(ligne.x + 2f, ligne.y + 2f, 26f, 26f);
            PawnKindDef genre = espece.race.AnyPawnKind;
            if (genre != null)
            {
                Widgets.DefIcon(icone, genre);
            }

            Rect reste = new Rect(icone.xMax + 6f, ligne.y + 3f, ligne.width - icone.width - 8f, 24f);
            bool cochee = BeteDeTrait.Est(espece);
            bool avant = cochee;
            Widgets.CheckboxLabeled(reste, Libelle(espece), ref cochee);
            if (cochee != avant)
            {
                Definir(espece, cochee);
            }
        }

        // Pourquoi cette ligne est-elle cochée ou non, et le joueur y est-il pour
        // quelque chose ? Le libellé seul ne peut pas le dire.
        private static string Infobulle(ThingDef espece)
        {
            string origine;
            if (BeteDeTrait.ParExtension(espece))
            {
                origine = "AAW_EspeceTipIntegree".Translate(espece.LabelCap);
            }
            else if (BeteDeTrait.ParBat(espece))
            {
                origine = "AAW_EspeceTipBat".Translate(espece.LabelCap);
            }
            else
            {
                origine = "AAW_EspeceTipLibre".Translate(espece.LabelCap);
            }
            if (PlowingMod.EspeceAjoutee(espece) || PlowingMod.EspeceRetiree(espece))
            {
                origine += "\n\n" + "AAW_EspeceTipModifiee".Translate();
            }
            return origine;
        }

        // Le libellé dit d'où vient l'éligibilité, sans quoi une case cochée
        // que le joueur n'a jamais touchée serait inexplicable.
        private static string Libelle(ThingDef espece)
        {
            string taille = espece.race.baseBodySize.ToString("0.0");
            if (BeteDeTrait.ParExtension(espece))
            {
                return "AAW_EspeceIntegree".Translate(espece.LabelCap, taille);
            }
            if (BeteDeTrait.ParBat(espece))
            {
                return "AAW_EspeceBat".Translate(espece.LabelCap, taille);
            }
            return "AAW_EspeceLibre".Translate(espece.LabelCap, taille);
        }

        // On ne retient que l'écart au défaut : cocher une espèce déjà éligible
        // efface simplement son retrait, et la décocher efface son ajout. Les
        // deux listes restent donc courtes et ne se contredisent jamais.
        private static void Definir(ThingDef espece, bool voulu)
        {
            bool dorigine = BeteDeTrait.EstDorigine(espece);
            reglages.especesAjoutees.Remove(espece.defName);
            reglages.especesRetirees.Remove(espece.defName);
            if (voulu && !dorigine)
            {
                reglages.especesAjoutees.Add(espece.defName);
            }
            else if (!voulu && dorigine)
            {
                reglages.especesRetirees.Add(espece.defName);
            }
            Reindexer();
        }
    }
}
