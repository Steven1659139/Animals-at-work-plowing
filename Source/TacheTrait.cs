using System;

namespace AnimalsAtWork.Plowing
{
    // Les trois travaux qu'une bête de trait peut prendre. Le joueur les
    // active une par une (interrupteurs sur la bête). MapComponent_Labour ne
    // retient que celles qui sont activées : l'absence vaut « ne travaille
    // pas », et une bête sans aucune tâche n'est pas une bête de trait.
    // Combinables, d'où [Flags].
    [Flags]
    public enum TacheTrait
    {
        Aucune = 0,
        Labour = 1,
        Charrette = 2,
        Deneigement = 4,
    }
}
