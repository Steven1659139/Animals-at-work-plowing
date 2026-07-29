using System;

namespace AnimalsAtWork.Plowing
{
    // Les trois travaux qu'une bête de trait peut prendre. Le joueur les
    // autorise ou les coupe une par une (interrupteurs sur la bête).
    // MapComponent_Labour ne retient que celles qui sont coupées : l'absence
    // vaut « tout permis ». Combinables, d'où [Flags].
    [Flags]
    public enum TacheTrait
    {
        Aucune = 0,
        Labour = 1,
        Charrette = 2,
        Deneigement = 4,
    }
}
