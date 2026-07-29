using HarmonyLib;
using RimWorld;
using Verse;

namespace AnimalsAtWork.Plowing
{
    // Affiche l'état du harnais, de la charrette et de la cargaison dans le
    // panneau d'inspection de l'animal (là où le joueur regarde) plutôt
    // que d'enfouir l'information dans l'onglet Équipement.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
    public static class Patch_InspectString
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            // Diagnostic (mode Dév uniquement) : pourquoi le colon (n')agit(-il)
            // (pas) sur cette bête. Sélectionne la bête et lis la ligne [AAW dev].
            if (Prefs.DevMode)
            {
                string diag = Diagnostic(__instance);
                if (diag != null)
                {
                    Ajouter(ref __result, diag);
                }
            }

            Thing harnais = EquipementUtility.Porte(__instance, AAW_DefOf.AAW_HarnaisDeTrait);
            if (harnais != null)
            {
                Ajouter(ref __result, "AAW_InspectHarnais".Translate(Pourcent(harnais)));
            }
            Thing charrue = EquipementUtility.Porte(__instance, AAW_DefOf.AAW_Charrue);
            if (charrue != null)
            {
                Ajouter(ref __result, "AAW_InspectCharrue".Translate(Pourcent(charrue)));
            }
            Thing charrette = EquipementUtility.Porte(__instance, AAW_DefOf.AAW_Charrette);
            if (charrette != null)
            {
                Ajouter(ref __result, "AAW_InspectCharrette".Translate(Pourcent(charrette)));
            }
            Thing grattoir = EquipementUtility.Porte(__instance, AAW_DefOf.AAW_Grattoir);
            if (grattoir != null)
            {
                Ajouter(ref __result, "AAW_InspectGrattoir".Translate(Pourcent(grattoir)));
            }
            float masse = EquipementUtility.MasseCargaison(__instance);
            if (masse > 0f)
            {
                Ajouter(ref __result, "AAW_InspectCargaison".Translate(
                    masse.ToString("F0"), EquipementUtility.CapaciteCharrette.ToString("F0")));
            }
        }

        // État de la bête au moment où le colon devrait l'équiper puis la mener :
        // service, tâche, job en cours (révèle une boucle), équipement porté,
        // et état de la corde. Null si ce n'est pas une bête de trait du joueur.
        private static string Diagnostic(Pawn bete)
        {
            if (!bete.Spawned
                || bete.Faction != Faction.OfPlayer
                || bete.def.GetModExtension<ModExtension_BeteDeTrait>() == null)
            {
                return null;
            }
            MapComponent_Labour c = bete.Map.GetComponent<MapComponent_Labour>();
            Thing attelage = EquipementUtility.AttelagePorte(bete);
            return "[AAW dev] "
                + $"service:{c.EstEnService(bete)} "
                + $"tâche:{ServiceTrait.TacheAServir(bete, c)} "
                + $"jobBête:{bete.CurJob?.def.defName ?? "-"}\n"
                + $"harnais:{EquipementUtility.Porte(bete, AAW_DefOf.AAW_HarnaisDeTrait) != null} "
                + $"attelage:{attelage?.def.defName ?? "-"} "
                + $"(surCarte h:{bete.Map.listerThings.ThingsOfDef(AAW_DefOf.AAW_HarnaisDeTrait).Count} "
                + $"c:{bete.Map.listerThings.ThingsOfDef(AAW_DefOf.AAW_Charrue).Count})\n"
                + $"encordée:{bete.roping.IsRoped} "
                + $"par:{bete.roping.RopedByPawn?.LabelShort ?? "-"}";
        }

        private static string Pourcent(Thing objet)
        {
            return (100f * objet.HitPoints / objet.MaxHitPoints).ToString("F0");
        }

        private static void Ajouter(ref string texte, string ligne)
        {
            texte = texte.NullOrEmpty() ? ligne : texte + "\n" + ligne;
        }
    }
}
