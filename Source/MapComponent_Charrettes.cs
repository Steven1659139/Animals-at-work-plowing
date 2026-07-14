using UnityEngine;
using Verse;

namespace AnimalsAtWork.Plowing
{
    // Dessine l'attelage derrière chaque bête équipée, orienté avec elle :
    // la charrette avec jusqu'à trois piles de cargaison visibles sur le
    // plateau, ou la charrue. MapComponentUpdate tourne à chaque frame : la
    // boucle reste courte (animaux de la colonie seulement) et ne dessine
    // que la carte visible.
    public class MapComponent_Charrettes : MapComponent
    {
        private const float TailleCharrette = 1.35f;
        private const float TailleCharrue = 1.0f;
        private const float TailleCargo = 0.5f;
        private const int CargosVisibles = 3;

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
                Thing charrette = EquipementUtility.Porte(animaux[i], AAW_DefOf.AAW_Charrette);
                if (charrette != null)
                {
                    Dessiner(animaux[i], charrette, TailleCharrette, avecCargo: true);
                    continue;
                }
                Thing charrue = EquipementUtility.Porte(animaux[i], AAW_DefOf.AAW_Charrue);
                if (charrue != null)
                {
                    Dessiner(animaux[i], charrue, TailleCharrue, avecCargo: false);
                }
            }
        }

        private static void Dessiner(Pawn bete, Thing attelage, float taille, bool avecCargo)
        {
            Rot4 rot = bete.Rotation;
            Quaternion orientation = Quaternion.AngleAxis(rot.AsAngle, Vector3.up);
            Vector3 pos = bete.DrawPos - rot.FacingCell.ToVector3() * 0.95f;
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
                // Piles réparties le long du plateau, de l'arrière vers l'avant.
                Vector3 posCargo = pos + orientation * new Vector3(0f, 0f, dessines * 0.30f - 0.35f);
                posCargo.y = pos.y + 0.02f; // au-dessus du plateau
                Graphics.DrawMesh(MeshPool.plane10,
                    Matrix4x4.TRS(posCargo, orientation, Vector3.one * TailleCargo),
                    cargo.Graphic.MatSingle, 0);
                dessines++;
            }
        }
    }
}
