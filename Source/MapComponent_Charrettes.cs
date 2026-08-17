using UnityEngine;
using Verse;

namespace AnimalsAtWork.Plowing
{
    // Dessine l'attelage derrière chaque bête équipée, orienté avec elle :
    // la charrue, le grattoir, ou la charrette avec jusqu'à trois piles de
    // cargaison visibles sur le plateau. MapComponentUpdate tourne à chaque
    // frame : la boucle reste courte (animaux de la colonie seulement) et ne
    // dessine que la carte visible.
    public class MapComponent_Charrettes : MapComponent
    {
        private const float TailleCharrette = 1.35f;
        private const float TailleCharrue = 1.0f;
        private const float TailleGrattoir = 1.0f;
        private const float TailleCargo = 0.5f;
        private const float Recul = 0.95f; // distance derrière le centre de la bête
        private const int CargosVisibles = 3;

        // L'attelage suit le gabarit de la bête qui le tire : la charrue d'un
        // âne n'a pas à faire la taille de celle d'un éléphant. On se règle sur
        // la taille dessinée de la bête, pas sur son bodySize — c'est une
        // texture qu'on accorde à une autre texture, et les deux ne vont pas
        // du tout de pair (l'alpaga se dessine aussi grand que le cheval).
        private const float GabaritReference = 2.6f; // bovin adulte : les tailles ci-dessus
        private const float FacteurMin = 0.6f;  // le poulet garde une charrue lisible
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
                Dessiner(animaux[i], attelage, Taille(attelage.def), avecCargo: charrette);
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

        private static void Dessiner(Pawn bete, Thing attelage, float taille, bool avecCargo)
        {
            float facteur = Facteur(bete);
            taille *= facteur;
            Rot4 rot = bete.Rotation;
            Quaternion orientation = Quaternion.AngleAxis(rot.AsAngle, Vector3.up);
            // Le recul suit le gabarit lui aussi : sans ça, l'attelage d'une
            // grosse bête lui rentrerait dans le corps et celui d'une petite
            // traînerait une case derrière elle, détaché.
            Vector3 pos = bete.DrawPos - rot.FacingCell.ToVector3() * (Recul * facteur);
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
                // Elles sont posées dessus : elles suivent la charrette.
                Vector3 posCargo = pos + orientation
                    * new Vector3(0f, 0f, (dessines * 0.30f - 0.35f) * facteur);
                posCargo.y = pos.y + 0.02f; // au-dessus du plateau
                Graphics.DrawMesh(MeshPool.plane10,
                    Matrix4x4.TRS(posCargo, orientation, Vector3.one * (TailleCargo * facteur)),
                    materiau, 0);
                dessines++;
            }
        }

        // Rapport entre la bête telle qu'elle est dessinée et le gabarit bovin.
        // La taille vient de l'étape de vie en cours : un poulain tire une
        // charrue de poulain, et elle grandit avec lui.
        private static float Facteur(Pawn bete)
        {
            GraphicData corps = bete.ageTracker?.CurKindLifeStage?.bodyGraphicData;
            if (corps == null)
            {
                return 1f;
            }
            return Mathf.Clamp(corps.drawSize.x / GabaritReference, FacteurMin, FacteurMax);
        }
    }
}
