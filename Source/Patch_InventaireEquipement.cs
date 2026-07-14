using HarmonyLib;
using Verse;

namespace AnimalsAtWork.Plowing
{
    // Au retour de caravane, le jeu marque les bêtes de somme « à décharger »
    // et les colons vident leur inventaire pièce par pièce. Sans ce patch,
    // ils confisqueraient aussi le harnais et la charrette : on les saute,
    // l'équipement reste sur la bête.
    [HarmonyPatch(typeof(Pawn_InventoryTracker), "FirstUnloadableThing", MethodType.Getter)]
    public static class Patch_InventaireEquipement
    {
        public static void Postfix(Pawn_InventoryTracker __instance, ref ThingCount __result)
        {
            if (__result.Thing == null || !EquipementUtility.EstEquipement(__result.Thing.def))
            {
                return;
            }
            ThingOwner contenu = __instance.innerContainer;
            for (int i = 0; i < contenu.Count; i++)
            {
                if (!EquipementUtility.EstEquipement(contenu[i].def))
                {
                    __result = new ThingCount(contenu[i], contenu[i].stackCount);
                    return;
                }
            }
            __result = default;
        }
    }
}
