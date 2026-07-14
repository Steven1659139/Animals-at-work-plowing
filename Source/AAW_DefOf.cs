using RimWorld;
using Verse;

namespace AnimalsAtWork.Plowing
{
    // Références typées vers nos défs XML, remplies par RimWorld au démarrage.
    // Les noms de champs doivent correspondre exactement aux defName.
    [DefOf]
    public static class AAW_DefOf
    {
        public static JobDef AAW_Labourer;
        public static JobDef AAW_Deneiger;
        public static JobDef AAW_Harnacher;
        public static JobDef AAW_Atteler;
        public static JobDef AAW_ChargerCharrette;
        public static JobDef AAW_ViderCharrette;
        public static ThingDef AAW_HarnaisDeTrait;
        public static ThingDef AAW_Charrue;
        public static ThingDef AAW_Charrette;
        public static ThingDef AAW_Grattoir;
        public static TerrainDef AAW_SolLaboure;
        public static ResearchProjectDef AAW_Harnachement;
        public static ResearchProjectDef AAW_Charretterie;

        // Déf vanilla non exposée par TerrainAffordanceDefOf
        public static TerrainAffordanceDef GrowSoil;

        static AAW_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(AAW_DefOf));
        }
    }
}
