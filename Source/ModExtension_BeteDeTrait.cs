using Verse;

namespace AnimalsAtWork.Plowing
{
    // Étiquette d'espèce : seules les races portant cette extension peuvent
    // apprendre le labour. Posée sur les bêtes de trait vanilla via
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
