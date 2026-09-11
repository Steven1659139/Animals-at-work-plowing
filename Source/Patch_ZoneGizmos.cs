using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AnimalsAtWork.Plowing
{
    // Ajoute le bouton « autoriser le labour » sur chaque zone de culture,
    // une fois la recherche Harnachement terminée. L'état est conservé par
    // MapComponent_Labour (sauvegardé avec la carte).
    [HarmonyPatch(typeof(Zone_Growing), nameof(Zone_Growing.GetGizmos))]
    public static class Patch_ZoneGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Zone_Growing __instance)
        {
            foreach (Gizmo gizmo in gizmos)
            {
                yield return gizmo;
            }
            if (!AAW_DefOf.AAW_Harnachement.IsFinished)
            {
                yield break;
            }
            MapComponent_Labour composante = MapComponent_Labour.De(__instance.Map);
            yield return new Command_Toggle
            {
                defaultLabel = "AAW_AutoriserLabour".Translate(),
                defaultDesc = "AAW_AutoriserLabourDesc".Translate(),
                icon = TexturesPlowing.IconeLabour,
                isActive = () => composante.LabourAutorise(__instance),
                toggleAction = () => composante.BasculerLabour(__instance),
            };
        }
    }
}
