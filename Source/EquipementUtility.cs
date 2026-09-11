using System.Collections.Generic;
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

        // Le harnais sert aux trois tâches : sa vie se compte en ouvrages toutes
        // tâches confondues, d'où sa place ici plutôt que dans chaque JobDriver,
        // qui en tenaient chacun leur copie. Valeur pour du cuir ordinaire ; les
        // cuirs plus résistants durent d'autant plus.
        //
        // Les durées de vie visent une saison de champ par exemplaire : le jeu
        // de base n'use rien à l'usage (ni arme qui frappe, ni vêtement porté),
        // et une pièce qui casse tous les deux jours n'a pas d'équivalent
        // vanilla. Elle doit rester un événement, pas une corvée d'atelier.
        public const int UsagesParHarnais = 1200;

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

        // Pose au sol l'une des pièces que la bête porte, si elle la porte ;
        // un colon la rangera au râtelier.
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
        // prisonnier de son inventaire pour toujours, et vanilla lui dessinait
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

        // Retire tout ce que la bête porte (cargaison, attelage et harnais) au
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
        // être surchargée. Les deux comptent : un âne plafonne vers 305 kg
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

        // L'équipement porté s'use ; détruit, un colon en apportera un neuf à
        // la bête (ServiceTrait.PieceManquante).
        // vieUtile : nombre d'usages qu'encaisse un exemplaire fait du matériau
        // ordinaire (bois pour les attelages, cuir simple pour le harnais).
        public static void User(Pawn pawn, ThingDef def, int vieUtile, string cleMessage)
        {
            Thing porte = Porte(pawn, def);
            if (porte == null)
            {
                return;
            }
            // Arrondi aléatoire : l'usure d'un usage tombe rarement sur un
            // nombre entier de points de vie, et une pièce n'en perd que des
            // entiers. Sur la vie de l'outil, la moyenne tombe juste.
            porte.HitPoints -= GenMath.RoundRandom(UsureParUsage(def, vieUtile));
            if (porte.HitPoints <= 0)
            {
                porte.Destroy();
                Messages.Message(cleMessage.Translate(pawn.LabelShortCap),
                    pawn, MessageTypeDefOf.NegativeEvent);
            }
        }

        // Points de vie perdus à chaque usage. La valeur est absolue : elle ne
        // dépend que de la déf, jamais du matériau de l'exemplaire, et c'est ce
        // qui fait qu'un soc d'acier dure plus longtemps qu'un soc de bois. Les
        // points de vie portent déjà le facteur du matériau : rapporter l'usure
        // au maximum de l'exemplaire l'annulerait, et un soc de plasteel
        // s'userait au même rythme qu'un soc de bois. On rapporte donc la durée
        // au matériau ordinaire (le bois, le cuir simple) : la durée annoncée
        // est celle de l'exemplaire que tout le monde fabrique, et tout ce qui
        // est plus solide dure davantage.
        private static float UsureParUsage(ThingDef def, int vieUtile)
        {
            return def.BaseMaxHitPoints * FacteurOrdinaire(def) / vieUtile;
        }

        private static float FacteurOrdinaire(ThingDef def)
        {
            ThingDef ordinaire = def == AAW_DefOf.AAW_HarnaisDeTrait
                ? AAW_DefOf.Leather_Plain
                : ThingDefOf.WoodLog;
            List<StatModifier> facteurs = ordinaire?.stuffProps?.statFactors;
            return facteurs == null
                ? 1f
                : facteurs.GetStatFactorFromList(StatDefOf.MaxHitPoints);
        }
    }
}
