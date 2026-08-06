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
        public static JobDef AAW_ChargerCharrette;
        public static JobDef AAW_ViderCharrette;
        public static JobDef AAW_ColonEquiper;
        public static JobDef AAW_MenerAuTravail;
        public static JobDef AAW_RamenerAEnclos;
        public static ThingDef AAW_HarnaisDeTrait;
        public static ThingDef AAW_Charrue;
        public static ThingDef AAW_Charrette;
        public static ThingDef AAW_Grattoir;
        public static TerrainDef AAW_SolLaboure;
        public static ResearchProjectDef AAW_Harnachement;
        public static ResearchProjectDef AAW_Charretterie;

        // Défs vanilla non exposées par les DefOf du jeu
        public static TerrainAffordanceDef GrowSoil;
        // Cuir ordinaire : matériau de référence du harnais, celui dont la
        // durée de vie est annoncée (voir EquipementUtility.UsureParUsage).
        public static ThingDef Leather_Plain;

        static AAW_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(AAW_DefOf));
        }
    }
}
