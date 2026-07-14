using HarmonyLib;
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
