using UnityEngine;
using Verse;

namespace AnimalsAtWork.Plowing
{
    // Icônes des interrupteurs (bête et zone). Les textures doivent être
    // chargées sur le thread principal, au démarrage.
    [StaticConstructorOnStartup]
    public static class TexturesPlowing
    {
        public static readonly Texture2D IconeLabour = ContentFinder<Texture2D>.Get("UI/Icons/Trainables/Haul");
        public static readonly Texture2D IconeCharrue = ContentFinder<Texture2D>.Get("Things/Item/AAW_Charrue");
        public static readonly Texture2D IconeCharrette = ContentFinder<Texture2D>.Get("Things/Item/AAW_Charrette");
        public static readonly Texture2D IconeGrattoir = ContentFinder<Texture2D>.Get("Things/Item/AAW_Grattoir");
    }
}
