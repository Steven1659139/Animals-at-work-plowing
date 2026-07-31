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

        // La cargaison vit dans la charrette : quand la charrette s'en va, elle
        // s'en va aussi. Sans ça, une bête dé-marquée gardait son chargement
        // prisonnier de son inventaire pour toujours — et vanilla lui dessinait
        // des sacoches sur le dos tant qu'il restait quoi que ce soit dedans
        // (PawnRenderNodeWorker_AnimalPack teste innerContainer.Count > 0).
        public static void DeposerCargaison(Pawn pawn)
        {
            if (pawn.inventory == null)
            {
                return;
            }
            ThingOwner contenu = pawn.inventory.innerContainer;
            for (int i = contenu.Count - 1; i >= 0; i--)
            {
                if (!EstEquipement(contenu[i].def))
                {
                    contenu.TryDrop(contenu[i], pawn.Position, pawn.Map, ThingPlaceMode.Near, out _);
                }
            }
        }

        // Retire tout ce que la bête porte — cargaison, attelage et harnais — au
        // sol : les colons rangeront l'équipement au râtelier et le chargement en
        // stock. Sert quand une bête cesse d'être bête de trait alors qu'elle
        // était encore équipée.
        public static void ToutDeposer(Pawn pawn)
        {
            DeposerCargaison(pawn);
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

        // Ce que la bête peut encore charger, au plus petit des deux plafonds :
        // la place restante dans la charrette, et ce qu'elle peut porter sans
        // être surchargée. Les deux comptent — un âne plafonne vers 305 kg
        // (bonus de charrette compris), soit moins que les 300 kg de cargaison
        // une fois le harnais et la charrette déduits. Sans le second plafond,
        // elle repartirait au ralenti sous le poids.
        public static float MasseLibre(Pawn pawn)
        {
            return Mathf.Min(CapaciteCharrette - MasseCargaison(pawn), MassUtility.FreeSpace(pawn));
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
