using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AnimalsAtWork.Plowing
{
    // Bouton « Détacher » sur toute bête de trait équipée : harnais et
    // attelage tombent au sol. C'est le levier de contrôle du joueur :
    // sans dressage, une bête équipée travaille toujours.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_GizmoDetacher
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Pawn __instance)
        {
            foreach (Gizmo gizmo in gizmos)
            {
                yield return gizmo;
            }
            if (!__instance.Spawned
                || __instance.Faction != Faction.OfPlayer
                || __instance.def.GetModExtension<ModExtension_BeteDeTrait>() == null)
            {
                yield break;
            }
            if (EquipementUtility.Porte(__instance, AAW_DefOf.AAW_HarnaisDeTrait) == null
                && EquipementUtility.AttelagePorte(__instance) == null)
            {
                yield break;
            }
            Pawn bete = __instance;
            yield return new Command_Action
            {
                defaultLabel = "AAW_Detacher".Translate(),
                defaultDesc = "AAW_DetacherDesc".Translate(),
                icon = TexturesPlowing.IconeDetacher,
                action = () => Deteler(bete),
            };
        }

        private static void Deteler(Pawn bete)
        {
            ThingOwner contenu = bete.inventory.innerContainer;
            for (int i = contenu.Count - 1; i >= 0; i--)
            {
                if (EquipementUtility.EstEquipement(contenu[i].def))
                {
                    contenu.TryDrop(contenu[i], bete.Position, bete.Map, ThingPlaceMode.Near, out _);
                }
            }
        }
    }
}
