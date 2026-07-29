using RimWorld;
using UnityEngine;
using Verse;

namespace AnimalsAtWork.Plowing
{
    // Le harnais, la charrue et la charrette sont des objets portés dans
    // l'inventaire de la bête (onglet Équipement) : visibles, usés à
    // l'ouvrage via leurs points de vie, lâchés au sol à sa mort. Tout le
    // reste de l'inventaire est de la cargaison de charrette.
    public static class EquipementUtility
    {
        public const float CapaciteCharrette = 300f; // kg de cargaison par tournée

        // L'objet de ce type que la bête porte sur elle, s'il y en a un.
        public static Thing Porte(Pawn pawn, ThingDef def)
        {
            if (pawn.inventory == null)
            {
                return null;
            }
            ThingOwner contenu = pawn.inventory.innerContainer;
            for (int i = 0; i < contenu.Count; i++)
            {
                if (contenu[i].def == def)
                {
                    return contenu[i];
                }
            }
            return null;
        }

        public static bool EstEquipement(ThingDef def)
        {
            return def == AAW_DefOf.AAW_HarnaisDeTrait || EstAttelage(def);
        }

        // Les attelages s'excluent mutuellement : une bête n'en tire qu'un seul.
        public static bool EstAttelage(ThingDef def)
        {
            return def == AAW_DefOf.AAW_Charrue
                || def == AAW_DefOf.AAW_Charrette
                || def == AAW_DefOf.AAW_Grattoir;
        }

        // L'attelage que la bête tire (charrue, charrette ou grattoir), s'il y en a un.
        public static Thing AttelagePorte(Pawn pawn)
        {
            if (pawn.inventory == null)
            {
                return null;
            }
            ThingOwner contenu = pawn.inventory.innerContainer;
            for (int i = 0; i < contenu.Count; i++)
            {
                if (EstAttelage(contenu[i].def))
                {
                    return contenu[i];
                }
            }
            return null;
        }

        // La bête pose son attelage au sol : la saison de cet outil est finie,
        // elle se rend disponible pour un autre. Les colons rangeront la pièce.
        public static void DeposerAttelage(Pawn pawn, ThingDef def)
        {
            Thing porte = Porte(pawn, def);
            if (porte != null)
            {
                pawn.inventory.innerContainer.TryDrop(
                    porte, pawn.Position, pawn.Map, ThingPlaceMode.Near, out _);
            }
        }

        // Retire tout l'équipement de trait (harnais et attelage) au sol : les
        // colons le rangeront au râtelier. Sert quand une bête cesse d'être bête
        // de trait alors qu'elle était encore équipée.
        public static void ToutDeposer(Pawn pawn)
        {
            DeposerAttelage(pawn, AAW_DefOf.AAW_Charrue);
            DeposerAttelage(pawn, AAW_DefOf.AAW_Charrette);
            DeposerAttelage(pawn, AAW_DefOf.AAW_Grattoir);
            DeposerAttelage(pawn, AAW_DefOf.AAW_HarnaisDeTrait);
        }

        // Première pile de cargaison à bord (hors harnais et charrette).
        public static Thing PremierCargo(Pawn pawn)
        {
            if (pawn.inventory == null)
            {
                return null;
            }
            ThingOwner contenu = pawn.inventory.innerContainer;
            for (int i = 0; i < contenu.Count; i++)
            {
                if (!EstEquipement(contenu[i].def))
                {
                    return contenu[i];
                }
            }
            return null;
        }

        public static float MasseCargaison(Pawn pawn)
        {
            if (pawn.inventory == null)
            {
                return 0f;
            }
            float total = 0f;
            ThingOwner contenu = pawn.inventory.innerContainer;
            for (int i = 0; i < contenu.Count; i++)
            {
                if (!EstEquipement(contenu[i].def))
                {
                    total += contenu[i].GetStatValue(StatDefOf.Mass) * contenu[i].stackCount;
                }
            }
            return total;
        }

        // L'équipement porté s'use ; détruit, la bête ira s'en procurer un neuf.
        // vieUtile : nombre d'usages qu'un exemplaire neuf encaisse.
        public static void User(Pawn pawn, ThingDef def, int vieUtile, string cleMessage)
        {
            Thing porte = Porte(pawn, def);
            if (porte == null)
            {
                return;
            }
            porte.HitPoints -= Mathf.Max(1, porte.MaxHitPoints / vieUtile);
            if (porte.HitPoints <= 0)
            {
                porte.Destroy();
                Messages.Message(cleMessage.Translate(pawn.LabelShortCap),
                    pawn, MessageTypeDefOf.NegativeEvent);
            }
        }
    }
}
