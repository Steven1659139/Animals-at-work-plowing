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
                || !BeteDeTrait.Est(__instance.def))
            {
                yield break;
            }
            Pawn bete = __instance;
            MapComponent_Labour composante = MapComponent_Labour.De(bete.Map);
            // Labour et déneigement dépendent du harnachement ; la charrette a sa
            // propre recherche. On n'affiche un interrupteur que si sa tâche est
            // seulement possible.
            if (AAW_DefOf.AAW_Harnachement.IsFinished)
            {
                yield return Interrupteur(bete, composante, TacheTrait.Labour,
                    "AAW_ToggleLabour", "AAW_ToggleLabourDesc", TexturesPlowing.IconeCharrue);
                yield return Interrupteur(bete, composante, TacheTrait.Deneigement,
                    "AAW_ToggleDeneigement", "AAW_ToggleDeneigementDesc", TexturesPlowing.IconeGrattoir);
            }
            if (AAW_DefOf.AAW_Charretterie.IsFinished)
            {
                yield return Interrupteur(bete, composante, TacheTrait.Charrette,
                    "AAW_ToggleCharrette", "AAW_ToggleCharretteDesc", TexturesPlowing.IconeCharrette);
            }
        }

        private static Command_Toggle Interrupteur(Pawn bete, MapComponent_Labour composante,
            TacheTrait tache, string cleLabel, string cleDesc, Texture2D icone)
        {
            return new Command_Toggle
            {
                defaultLabel = cleLabel.Translate(),
                defaultDesc = cleDesc.Translate(),
                icon = icone,
                isActive = () => composante.TacheAutorisee(bete, tache),
                toggleAction = () => Basculer(bete, composante, tache),
            };
        }

        // Bascule la tâche, remet l'équipement en accord (la bête pose aussitôt
        // l'attelage d'une tâche coupée, et son harnais si plus rien ne
        // l'occupe), puis coupe son travail en cours pour qu'elle reconsidère.
        private static void Basculer(Pawn bete, MapComponent_Labour composante, TacheTrait tache)
        {
            composante.BasculerTache(bete, tache);
            ReconcilierEquipement(bete, composante);
            if (bete.jobs != null && bete.CurJob != null)
            {
                bete.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }

        private static void ReconcilierEquipement(Pawn bete, MapComponent_Labour composante)
        {
            // En plein service (au champ) : ne rien lâcher ici, ce tomberait au
            // milieu des cultures. Si la bête n'est plus bête de trait, le
            // WorkGiver la fera ramener puis déséquiper à l'enclos.
            if (composante.EstEnService(bete))
            {
                return;
            }
            bool labour = composante.TacheAutorisee(bete, TacheTrait.Labour);
            bool charrette = composante.TacheAutorisee(bete, TacheTrait.Charrette);
            bool deneige = composante.TacheAutorisee(bete, TacheTrait.Deneigement);
            if (!labour)
            {
                EquipementUtility.DeposerAttelage(bete, AAW_DefOf.AAW_Charrue);
            }
            if (!charrette)
            {
                // La cargaison part avec la charrette, sinon elle reste bloquée
                // dans l'inventaire de la bête sans personne pour l'en sortir.
                EquipementUtility.DeposerCargaison(bete);
                EquipementUtility.DeposerAttelage(bete, AAW_DefOf.AAW_Charrette);
            }
            if (!deneige)
            {
                EquipementUtility.DeposerAttelage(bete, AAW_DefOf.AAW_Grattoir);
            }
            // Plus aucune tâche permise : le harnais lui-même ne sert plus à rien.
            if (!labour && !charrette && !deneige)
            {
                EquipementUtility.DeposerAttelage(bete, AAW_DefOf.AAW_HarnaisDeTrait);
            }
        }
    }
}
