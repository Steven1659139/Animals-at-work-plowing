using HarmonyLib;
using RimWorld;
using Verse;

namespace AnimalsAtWork.Plowing
{
    // Le temps du service (menée au champ), une bête de trait n'est plus
    // « errante » : elle n'est ni ramenée d'office à l'enclos, ni sujette à la
    // fugue, et son cerveau ne cherche pas à vagabonder. Tout cela se réduit à
    // une seule propriété vanilla, Pawn.Roamer, que consultent la gestion
    // d'enclos (AnimalPenUtility.NeedsToBeManagedByRope), l'arbre de pensée
    // (ThinkNode_ConditionalRoamer) et l'état mental d'errance
    // (MentalStateWorker_Roaming). On la force donc à false.
    //
    // La fenêtre s'ouvre dès le harnais bouclé, et pas seulement au départ pour
    // le champ (ServiceTrait.ExemptFromPen) : entre les deux, un meneur
    // interrompu peut lâcher la bête en chemin, et sans ça un autre meneur,
    // colon ou chien de berger, la ramènerait aussitôt à l'enclos pour qu'on
    // l'en ressorte juste après. Une bête harnachée qui n'a plus rien à faire
    // dehors est ramenée par ServiceTrait.ServiceJob, pas par les enclos.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Roamer), MethodType.Getter)]
    public static class Patch_RoamerEnService
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (__result && ServiceTrait.IsExempt(__instance))
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
            if (__result)
            {
                return;
            }
            // Seulement pour les bêtes que les clôtures retiennent déjà : ce
            // postfix ne rend que ce que le nôtre sur Roamer vient de retirer,
            // il n'ajoute rien. Une bête hors bétail (un prédateur de mod
            // marqué bête de somme) n'a jamais été retenue par une clôture, et
            // l'atteler ne doit pas l'enfermer : elle se retrouvait sinon prise
            // dans l'enclos où on la ramène, sans rien y manger et sans pouvoir
            // en sortir chasser.
            // RoamMtbDays et non Roamer : Roamer, c'est justement ce que notre
            // autre postfix a mis à false. La propriété d'où il le tire
            // (Pawn.Roamer => RoamMtbDays.HasValue) n'est pas patchée, elle, et
            // donne la réponse vanilla.
            if (!__instance.RoamMtbDays.HasValue)
            {
                return;
            }
            // On respecte l'exemption vanilla par job (ignoreFenceBlocked).
            JobDef job = __instance.CurJobDef;
            if (job != null && job.ignoreFenceBlocked)
            {
                return;
            }
            if (ServiceTrait.IsExempt(__instance))
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
            if (__result && food is Plant plant && plant.sown && ServiceTrait.IsOnDuty(p))
            {
                __result = false;
            }
        }
    }
}
