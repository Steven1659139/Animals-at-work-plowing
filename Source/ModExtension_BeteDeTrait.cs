using Verse;

namespace AnimalsAtWork.Plowing
{
    // Étiquette d'espèce : l'une des façons de rendre une race éligible au
    // trait (voir BeteDeTrait), à côté du drapeau packAnimal vanilla et des
    // réglages du joueur. Posée sur les exceptions vanilla (vache, âne) via
    // Patches/Patch_EspecesDeTrait.xml ; tout autre mod peut rendre son espèce
    // éligible avec :
    //   <Operation Class="PatchOperationAddModExtension">
    //     <xpath>Defs/ThingDef[defName="SonAnimal"]</xpath>
    //     <value><li Class="AnimalsAtWork.Plowing.ModExtension_BeteDeTrait"/></value>
    //   </Operation>
    public class ModExtension_BeteDeTrait : DefModExtension
    {
    }
}
