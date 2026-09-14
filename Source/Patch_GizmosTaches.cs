using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Un interrupteur par tâche sur chaque bête de trait : labour, charrette,
    // déneigement. C'est la sélection opt-in (« pas tous les animaux ») : une
    // bête ne devient bête de trait qu'en activant une tâche dessus, et un colon
    // se charge alors de l'équiper puis de la mener au champ. Le toggle de zone,
    // lui, coupe le labour pour toutes à la fois. Chaque interrupteur n'apparaît
    // qu'une fois sa recherche débloquée. Couper une tâche à une bête restée à
    // l'enclos lui fait aussitôt poser l'attelage correspondant (rangé au
    // râtelier) ; en plein service, on attend son retour pour la déséquiper.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_GizmosTaches
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Pawn __instance)
        {
            foreach (Gizmo gizmo in gizmos)
            {
                yield return gizmo;
            }
            if (!__instance.Spawned
                || __instance.Faction != Faction.OfPlayer
                || !BeteDeTrait.Is(__instance.def))
            {
                yield break;
            }
            Pawn beast = __instance;
            MapComponent_Labour component = MapComponent_Labour.Of(beast.Map);
            // Labour et déneigement dépendent du harnachement ; la charrette a sa
            // propre recherche. On n'affiche un interrupteur que si sa tâche est
            // seulement possible.
            if (AAW_DefOf.AAW_Harnachement.IsFinished)
            {
                yield return Toggle(beast, component, TacheTrait.Labour,
                    "AAW_ToggleLabour", "AAW_ToggleLabourDesc", TexturesPlowing.PlowIcon);
                yield return Toggle(beast, component, TacheTrait.Deneigement,
                    "AAW_ToggleDeneigement", "AAW_ToggleDeneigementDesc", TexturesPlowing.ScraperIcon);
            }
            if (AAW_DefOf.AAW_Charretterie.IsFinished)
            {
                yield return Toggle(beast, component, TacheTrait.Charrette,
                    "AAW_ToggleCharrette", "AAW_ToggleCharretteDesc", TexturesPlowing.CartIcon);
            }
        }

        private static Command_Toggle Toggle(Pawn beast, MapComponent_Labour component,
            TacheTrait task, string labelKey, string descKey, Texture2D icon)
        {
            return new Command_Toggle
            {
                defaultLabel = labelKey.Translate(),
                defaultDesc = descKey.Translate(),
                icon = icon,
                isActive = () => component.TaskAllowed(beast, task),
                toggleAction = () => Toggle(beast, component, task),
            };
        }

        // Bascule la tâche, remet l'équipement en accord (la bête pose aussitôt
        // l'attelage d'une tâche coupée, et son harnais si plus rien ne
        // l'occupe), puis coupe son travail en cours pour qu'elle reconsidère.
        private static void Toggle(Pawn beast, MapComponent_Labour component, TacheTrait task)
        {
            component.ToggleTask(beast, task);
            ReconcileEquipment(beast, component);
            if (beast.jobs != null && beast.CurJob != null)
            {
                beast.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }

        private static void ReconcileEquipment(Pawn beast, MapComponent_Labour component)
        {
            // En plein service (au champ) : ne rien lâcher ici, ce tomberait au
            // milieu des cultures. Si la bête n'est plus bête de trait, le
            // WorkGiver la fera ramener puis déséquiper à l'enclos.
            if (component.IsOnDuty(beast))
            {
                return;
            }
            bool plowing = component.TaskAllowed(beast, TacheTrait.Labour);
            bool cart = component.TaskAllowed(beast, TacheTrait.Charrette);
            bool snowCleared = component.TaskAllowed(beast, TacheTrait.Deneigement);
            if (!plowing)
            {
                EquipementUtility.DropImplement(beast, AAW_DefOf.AAW_Charrue);
            }
            if (!cart)
            {
                // La cargaison part avec la charrette, sinon elle reste bloquée
                // dans l'inventaire de la bête sans personne pour l'en sortir.
                EquipementUtility.DropCargo(beast);
                EquipementUtility.DropImplement(beast, AAW_DefOf.AAW_Charrette);
            }
            if (!snowCleared)
            {
                EquipementUtility.DropImplement(beast, AAW_DefOf.AAW_Grattoir);
            }
            // Plus aucune tâche permise : le harnais lui-même ne sert plus à rien.
            if (!plowing && !cart && !snowCleared)
            {
                EquipementUtility.DropImplement(beast, AAW_DefOf.AAW_HarnaisDeTrait);
            }
        }
    }
}
