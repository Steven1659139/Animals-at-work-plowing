using HarmonyLib;
using Verse;

namespace AnimalsAtWork.Plowing
{
    // Exécuté une seule fois par RimWorld après le chargement de toutes les défs.
    [StaticConstructorOnStartup]
    public static class PlowingInit
    {
        static PlowingInit()
        {
            new Harmony("royaltea.animalsatwork.plowing").PatchAll();
            Log.Message("[Animals at Work — Plowing] Assembly chargée, patch Harmony actif.");
        }
    }
}
