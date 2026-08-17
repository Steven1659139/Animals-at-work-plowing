using UnityEngine;
using Verse;

namespace AnimalsAtWork.Plowing
{
    // Dessine sa pièce à chaque bête équipée, orientée avec elle : la charrue
    // ou la charrette derrière, celle-ci avec jusqu'à trois piles de cargaison
    // visibles sur le plateau, et le grattoir devant, qui se pousse.
    // MapComponentUpdate tourne à chaque frame : la boucle reste courte
    // (animaux de la colonie seulement) et ne dessine que la carte visible.
    public class MapComponent_Charrettes : MapComponent
    {
        // Tailles pour le gabarit bovin ; tout le reste s'en déduit. Une pièce
        // qui fait la moitié de la bête se lit de loin, le tiers ne se lit pas.
        private const float TailleCharrette = 2.3f;
        private const float TailleCharrue = 1.7f;
        private const float TailleGrattoir = 1.7f;
        private const int CargosVisibles = 3;

        // Disposition des piles, en fraction de la charrette : posées sur le
        // plateau, elles la suivent quand elle change de taille.
        private const float CargoTaille = 0.37f;
        private const float CargoPas = 0.22f;
        private const float CargoDepart = -0.26f;

        // La pièce sort du sprite de la bête : demi-longueur de la bête plus
        // demi-longueur de la pièce. Un écart fixe mis à l'échelle ne suffit
        // pas — dessinée SOUS la bête, une pièce dont le centre tombe dans le
        // sprite est une pièce enterrée, et plus la bête est grosse mieux elle
        // l'enterre. C'est ce qui la faisait disparaître de profil, où le corps
        // remplit toute la largeur, et qui masquait l'agrandissement.
        // Serrage : les deux textures ont de la marge transparente, on les
        // rapproche d'autant pour que l'attelage reste au cul de la bête.
        private const float Serrage = 0.85f;

        // L'attelage suit le gabarit de la bête qui le tire : la charrue d'un
        // âne n'a pas à faire la taille de celle d'un éléphant. On se règle sur
        // la taille dessinée de la bête, pas sur son bodySize — c'est une
        // texture qu'on accorde à une autre texture, et les deux ne vont pas
        // du tout de pair (l'alpaga se dessine aussi grand que le cheval).
        private const float GabaritReference = 2.6f; // bovin adulte : les tailles ci-dessus
        // Le plancher se règle en taille dessinée, pas en proportion : il vaut
        // ce qu'il faut pour que la charrue du poulet reste lisible (~0,6 case),
        // et se redescend donc quand les tailles ci-dessus grandissent.
        private const float FacteurMin = 0.35f;
        private const float FacteurMax = 1.5f;  // le thrumbo n'en tire pas une de deux cases

        public MapComponent_Charrettes(Map map) : base(map)
        {
        }

        public override void MapComponentUpdate()
        {
            if (map != Find.CurrentMap)
            {
                return;
            }
            var animaux = map.mapPawns.SpawnedColonyAnimals;
            for (int i = 0; i < animaux.Count; i++)
            {
                // Une seule recherche par bête : les trois attelages s'excluent,
                // et cette boucle tourne à chaque frame.
                Thing attelage = EquipementUtility.AttelagePorte(animaux[i]);
                if (attelage == null)
                {
                    continue;
                }
                bool charrette = attelage.def == AAW_DefOf.AAW_Charrette;
                // Le grattoir se pousse, il ne se tire pas : il passe devant la
                // bête, lame en avant, là où charrue et charrette suivent.
                bool devant = attelage.def == AAW_DefOf.AAW_Grattoir;
                Dessiner(animaux[i], attelage, Taille(attelage.def),
                    avecCargo: charrette, devant: devant);
            }
        }

        private static float Taille(ThingDef def)
        {
            if (def == AAW_DefOf.AAW_Charrette)
            {
                return TailleCharrette;
            }
            return def == AAW_DefOf.AAW_Charrue ? TailleCharrue : TailleGrattoir;
        }

        private static void Dessiner(Pawn bete, Thing attelage, float taille, bool avecCargo,
            bool devant = false)
        {
            float gabarit = TailleDessinee(bete);
            taille *= Facteur(gabarit);
            Rot4 rot = bete.Rotation;
            // Les textures d'attelage sont dessinées timon vers le haut, lame
            // vers le bas : tirées, le timon pointe déjà vers la bête. Poussée,
            // la pièce fait demi-tour pour lui présenter son timon et mettre sa
            // lame en tête.
            Quaternion orientation =
                Quaternion.AngleAxis(rot.AsAngle + (devant ? 180f : 0f), Vector3.up);
            float ecart = (gabarit + taille) * 0.5f * Serrage;
            Vector3 pos = bete.DrawPos
                + rot.FacingCell.ToVector3() * (ecart * (devant ? 1f : -1f));
            pos.y = AltitudeLayer.Pawn.AltitudeFor() - 0.03f; // juste sous la bête
            Graphics.DrawMesh(MeshPool.plane10,
                Matrix4x4.TRS(pos, orientation, new Vector3(taille, 1f, taille)),
                attelage.Graphic.MatSingle, 0);
            if (!avecCargo)
            {
                return;
            }

            ThingOwner contenu = bete.inventory.innerContainer;
            int dessines = 0;
            for (int i = 0; i < contenu.Count && dessines < CargosVisibles; i++)
            {
                Thing cargo = contenu[i];
                if (EquipementUtility.EstEquipement(cargo.def))
                {
                    continue;
                }
                // Tout ne se dessine pas à plat sur un plateau : un cadavre n'a
                // pas de graphicData propre (il est rendu par le pion qu'il
                // contient), et Thing.Graphic renvoie alors BaseContent.BadGraphic,
                // c'est-à-dire le carré magenta barré de rouge. On saute ces
                // pièces-là sans consommer une des trois places visibles.
                Material materiau = cargo.Graphic?.MatSingle;
                if (materiau.NullOrBad())
                {
                    continue;
                }
                // Piles réparties le long du plateau, de l'arrière vers l'avant.
                Vector3 posCargo = pos + orientation
                    * new Vector3(0f, 0f, (dessines * CargoPas + CargoDepart) * taille);
                posCargo.y = pos.y + 0.02f; // au-dessus du plateau
                Graphics.DrawMesh(MeshPool.plane10,
                    Matrix4x4.TRS(posCargo, orientation, Vector3.one * (CargoTaille * taille)),
                    materiau, 0);
                dessines++;
            }
        }

        // Longueur de la bête telle qu'elle est dessinée, en cases. Elle vient
        // de l'étape de vie en cours : un poulain tire une charrue de poulain,
        // et elle grandit avec lui. Le gabarit bovin par défaut, faute de mieux.
        public static float TailleDessinee(Pawn bete)
        {
            GraphicData corps = bete.ageTracker?.CurKindLifeStage?.bodyGraphicData;
            return corps == null ? GabaritReference : corps.drawSize.x;
        }

        // Rapport entre cette longueur et celle du gabarit bovin.
        public static float Facteur(float gabarit)
        {
            return Mathf.Clamp(gabarit / GabaritReference, FacteurMin, FacteurMax);
        }
    }
}
