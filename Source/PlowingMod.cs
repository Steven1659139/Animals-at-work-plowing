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
        private List<ThingDef> animaux;

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

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // La base de défs n'est pas prête à la construction du mod : on
            // dresse la liste au premier affichage de la fenêtre. L'ordre est
            // figé une fois pour toutes — un tri qui suivrait l'éligibilité
            // ferait sauter les lignes sous le curseur à chaque clic.
            if (animaux == null)
            {
                animaux = DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(d => d.race != null && d.race.Animal)
                    .OrderByDescending(d => d.race.baseBodySize)
                    .ThenBy(d => d.label)
                    .ToList();
            }

            Listing_Standard entete = new Listing_Standard();
            entete.Begin(new Rect(inRect.x, inRect.y, inRect.width, 92f));
            entete.Label("AAW_ReglageEspeces".Translate());
            recherche = entete.TextEntry(recherche);
            entete.End();

            Rect cadre = new Rect(inRect.x, inRect.y + 100f, inRect.width, inRect.height - 100f);
            List<ThingDef> visibles = recherche.NullOrEmpty()
                ? animaux
                : animaux.Where(d => d.label != null
                    && d.label.IndexOf(recherche, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            Rect contenu = new Rect(0f, 0f, cadre.width - 20f, visibles.Count * 26f);
            Widgets.BeginScrollView(cadre, ref defilement, contenu);
            float y = 0f;
            for (int i = 0; i < visibles.Count; i++)
            {
                ThingDef espece = visibles[i];
                bool cochee = BeteDeTrait.Est(espece);
                bool avant = cochee;
                Widgets.CheckboxLabeled(new Rect(0f, y, contenu.width, 24f), Libelle(espece), ref cochee);
                if (cochee != avant)
                {
                    Definir(espece, cochee);
                }
                y += 26f;
            }
            Widgets.EndScrollView();
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
