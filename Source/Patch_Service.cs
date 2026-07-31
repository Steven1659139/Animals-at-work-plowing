using HarmonyLib;
using RimWorld;
using Verse;

namespace AnimalsAtWork.Plowing
{
    // Le temps du service (menée au champ par un colon), une bête de trait
    // n'est plus « errante » : elle n'est ni ramenée d'office à l'enclos, ni
    // sujette à la fugue, et son cerveau ne cherche pas à vagabonder. Tout cela
    // se réduit à une seule propriété vanilla — Pawn.Roamer — que consultent la
    // gestion d'enclos (AnimalPenUtility.NeedsToBeManagedByRope), l'arbre de
    // pensée (ThinkNode_ConditionalRoamer) et l'état mental d'errance
    // (MentalStateWorker_Roaming). On la force donc à false pendant le service.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Roamer), MethodType.Getter)]
    public static class Patch_RoamerEnService
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (!__result
                || !__instance.Spawned
                || __instance.def.GetModExtension<ModExtension_BeteDeTrait>() == null)
            {
                return;
            }
            MapComponent_Labour composante = __instance.Map.GetComponent<MapComponent_Labour>();
            if (composante != null && composante.EstEnService(__instance))
            {
                __result = false;
            }
        }
    }

    // Vanilla se sert de Roamer pour deux choses différentes : « cette bête
    // vagabonde et doit être gérée à la corde », qu'on veut bien couper pendant
    // le service, mais aussi « c'est du bétail que les clôtures retiennent »
    //   Pawn.FenceBlocked => Roamer && (CurJobDef == null || !ignoreFenceBlocked)
    // qu'on ne veut surtout pas perdre. Sans ça, la bête en service traverse les
    // clôtures, et surtout Pawn.ShouldAvoidFences (qui consulte
    // Pawn_RopeTracker.AnyRopeesFenceBlocked) devient faux chez le colon qui la
    // tient : il rentre par-dessus la clôture au lieu de la faire passer par le
    // portail. On rend donc le blocage par clôture à la bête en service.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.FenceBlocked), MethodType.Getter)]
    public static class Patch_ClotureEnService
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (__result
                || !__instance.Spawned
                || __instance.def.GetModExtension<ModExtension_BeteDeTrait>() == null)
            {
                return;
            }
            // On respecte l'exemption vanilla par job (ignoreFenceBlocked).
            JobDef job = __instance.CurJobDef;
            if (job != null && job.ignoreFenceBlocked)
            {
                return;
            }
            MapComponent_Labour composante = __instance.Map.GetComponent<MapComponent_Labour>();
            if (composante != null && composante.EstEnService(__instance))
            {
                __result = true;
            }
        }
    }

    // Une bête en service, lâchée au milieu d'un champ, brouterait les récoltes
    // que la protection de l'enclos ne joue plus. On lui interdit donc de
    // choisir une plante semée comme repas : le scan de nourriture passe alors
    // à l'auge ou à l'herbe sauvage. Elle peut toujours manger tout le reste.
    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.WillEat),
        new[] { typeof(Pawn), typeof(Thing), typeof(Pawn), typeof(bool), typeof(bool) })]
    public static class Patch_BroutageRecoltes
    {
        public static void Postfix(Pawn p, Thing food, ref bool __result)
        {
            if (!__result
                || p == null
                || !p.Spawned
                || !(food is Plant plant)
                || !plant.sown
                || p.def.GetModExtension<ModExtension_BeteDeTrait>() == null)
            {
                return;
            }
            MapComponent_Labour composante = p.Map.GetComponent<MapComponent_Labour>();
            if (composante != null && composante.EstEnService(p))
            {
                __result = false;
            }
        }
    }
}
