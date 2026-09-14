using RimWorld.Planet;
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
        private const float CartSize = 2.3f;
        private const float PlowSize = 1.7f;
        private const float ScraperSize = 1.7f;
        private const int VisibleCargos = 3;

        // Disposition des piles, en fraction de la charrette : posées sur le
        // plateau, elles la suivent quand elle change de taille.
        private const float CargoSize = 0.37f;
        private const float CargoStep = 0.22f;
        private const float CargoStart = -0.26f;

        // La pièce sort du sprite de la bête : demi-longueur de la bête plus
        // demi-longueur de la pièce, moins un serrage. Dessinée sous la bête,
        // une pièce dont le centre tombe dans le sprite est une pièce enterrée,
        // et d'autant plus que la bête est grosse : l'écart doit suivre les
        // deux tailles, pas seulement l'échelle. Le serrage compense la marge
        // transparente des deux textures, pour que l'attelage reste au cul de
        // la bête.
        private const float Tightening = 0.85f;

        // L'attelage suit le gabarit de la bête qui le tire : la charrue d'un
        // âne n'a pas à faire la taille de celle d'un éléphant. On se règle sur
        // la taille dessinée de la bête, pas sur son bodySize : c'est une
        // texture qu'on accorde à une autre texture, et les deux ne vont pas
        // du tout de pair (l'alpaga se dessine aussi grand que le cheval).
        private const float ReferenceSize = 2.6f; // bovin adulte : les tailles ci-dessus
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
            // Même condition que le rendu vanilla (Map.MapUpdate) : DrawingMap,
            // et non WorldRendered. Ouvrir la planète n'arrête pas
            // MapComponentUpdate, seulement le dessin de la carte, et sans ce
            // test nos DrawMesh passeraient dans la caméra du monde. Quant au
            // monde, il se dessine aussi en fond de carte (vaisseau-gravité,
            // orbite), et là la carte, elle, se dessine bel et bien.
            if (!WorldRendererUtility.DrawingMap || map != Find.CurrentMap)
            {
                return;
            }
            var animals = map.mapPawns.SpawnedColonyAnimals;
            for (int i = 0; i < animals.Count; i++)
            {
                // Une seule recherche par bête : les trois attelages s'excluent,
                // et cette boucle tourne à chaque frame.
                Thing implement = EquipementUtility.CarriedImplement(animals[i]);
                if (implement == null)
                {
                    continue;
                }
                bool cart = implement.def == AAW_DefOf.AAW_Charrette;
                // Le grattoir se pousse, il ne se tire pas : il passe devant la
                // bête, lame en avant, là où charrue et charrette suivent.
                bool front = implement.def == AAW_DefOf.AAW_Grattoir;
                Draw(animals[i], implement, Size(implement.def),
                    withCargo: cart, front: front);
            }
        }

        private static float Size(ThingDef def)
        {
            if (def == AAW_DefOf.AAW_Charrette)
            {
                return CartSize;
            }
            return def == AAW_DefOf.AAW_Charrue ? PlowSize : ScraperSize;
        }

        private static void Draw(Pawn beast, Thing implement, float size, bool withCargo,
            bool front = false)
        {
            float beastSize = DrawnSize(beast);
            size *= Factor(beastSize);
            Rot4 rot = beast.Rotation;
            // Les textures d'attelage sont dessinées timon vers le haut, lame
            // vers le bas : tirées, le timon pointe déjà vers la bête. Poussée,
            // la pièce fait demi-tour pour lui présenter son timon et mettre sa
            // lame en tête.
            Quaternion orientation =
                Quaternion.AngleAxis(rot.AsAngle + (front ? 180f : 0f), Vector3.up);
            float offset = (beastSize + size) * 0.5f * Tightening;
            Vector3 pos = beast.DrawPos
                + rot.FacingCell.ToVector3() * (offset * (front ? 1f : -1f));
            pos.y = AltitudeLayer.Pawn.AltitudeFor() - 0.03f; // juste sous la bête
            Graphics.DrawMesh(MeshPool.plane10,
                Matrix4x4.TRS(pos, orientation, new Vector3(size, 1f, size)),
                implement.Graphic.MatSingle, 0);
            if (!withCargo)
            {
                return;
            }

            ThingOwner contents = beast.inventory.innerContainer;
            int drawn = 0;
            for (int i = 0; i < contents.Count && drawn < VisibleCargos; i++)
            {
                Thing cargo = contents[i];
                if (EquipementUtility.IsEquipment(cargo.def))
                {
                    continue;
                }
                // Tout ne se dessine pas à plat sur un plateau : un cadavre n'a
                // pas de graphicData propre (il est rendu par le pion qu'il
                // contient), et Thing.Graphic renvoie alors BaseContent.BadGraphic,
                // c'est-à-dire le carré magenta barré de rouge. On saute ces
                // pièces-là sans consommer une des trois places visibles.
                //
                // MatSingleFor et non MatSingle : sur un Graphic_Random (les
                // rochers, la ferraille), MatSingle retire une variante au
                // hasard à CHAQUE appel. Appelé une fois par frame, le caillou
                // changeait de forme soixante fois par seconde et gigotait sur
                // le plateau. MatSingleFor fixe la variante sur l'identifiant
                // de la pièce, celle-là même que le jeu lui donne au sol.
                Material stuff = cargo.Graphic?.MatSingleFor(cargo);
                if (stuff.NullOrBad())
                {
                    continue;
                }
                // Piles réparties le long du plateau, de l'arrière vers l'avant.
                // L'orientation ne sert qu'à les répartir : le chargement, lui,
                // reste d'aplomb. Une pile de rochers qui pivote d'un quart de
                // tour parce que la bête tourne à l'est ne ressemble à rien, et
                // le jeu ne fait jamais tourner un objet posé.
                Vector3 cargoPos = pos + orientation
                    * new Vector3(0f, 0f, (drawn * CargoStep + CargoStart) * size);
                cargoPos.y = pos.y + 0.02f; // au-dessus du plateau
                Graphics.DrawMesh(MeshPool.plane10,
                    Matrix4x4.TRS(cargoPos, Quaternion.identity,
                        Vector3.one * (CargoSize * size)),
                    stuff, 0);
                drawn++;
            }
        }

        // Longueur de la bête telle qu'elle est dessinée, en cases. Elle vient
        // de l'étape de vie en cours : un poulain tire une charrue de poulain,
        // et elle grandit avec lui. Le gabarit bovin par défaut, faute de mieux.
        public static float DrawnSize(Pawn beast)
        {
            GraphicData body = beast.ageTracker?.CurKindLifeStage?.bodyGraphicData;
            return body == null ? ReferenceSize : body.drawSize.x;
        }

        // Rapport entre cette longueur et celle du gabarit bovin.
        public static float Factor(float beastSize)
        {
            return Mathf.Clamp(beastSize / ReferenceSize, FacteurMin, FacteurMax);
        }
    }
}
