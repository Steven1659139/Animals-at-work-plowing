using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AnimalsAtWork.Plowing
{
    // Une bête attelée à sa charrette emporte 100 kg de plus en caravane.
    // MassUtility.Capacity n'est pas piloté par une stat (bodySize × 35),
    // d'où le postfix. On rembourse aussi le poids de la charrette elle-même,
    // comptée comme inventaire par ailleurs : le bonus affiché est net.
    [HarmonyPatch(typeof(MassUtility), nameof(MassUtility.Capacity))]
    public static class Patch_CapaciteCaravane
    {
        private const float BonusCharrette = 100f;

        public static void Postfix(Pawn p, StringBuilder explanation, ref float __result)
        {
            // Capacité nulle (bébé, espèce inapte) : la charrette n'y change rien.
            if (__result <= 0f)
            {
                return;
            }
            Thing charrette = EquipementUtility.Porte(p, AAW_DefOf.AAW_Charrette);
            if (charrette == null)
            {
                return;
            }
            __result += BonusCharrette + charrette.GetStatValue(StatDefOf.Mass);
            if (explanation != null)
            {
                explanation.AppendLine();
                explanation.Append("  - " + "AAW_BonusCharrette".Translate() + ": +" + BonusCharrette.ToStringMass());
            }
        }
    }
}
