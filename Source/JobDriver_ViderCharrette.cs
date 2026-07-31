using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Livraison : la bête dépose sa cargaison pile par pile dans les
    // meilleurs stocks disponibles, en boucle jusqu'à ce que la charrette
    // soit vide. Sans rangement pour une pile, tout est déversé sur place
    // plutôt que de promener la cargaison indéfiniment.
    public class JobDriver_ViderCharrette : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil chercher = ToilMaker.MakeToil("ChercherRangement");
            chercher.initAction = delegate
            {
                if (EquipementUtility.PremierCargo(pawn) == null)
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                // On livre la pile dont le rangement est le plus proche d'ici,
                // et pas la première venue dans l'inventaire : sinon chaque
                // dépôt peut renvoyer la bête à l'autre bout de la colonie.
                if (ProchaineLivraison(out Thing cargo, out IntVec3 cellule))
                {
                    job.SetTarget(TargetIndex.B, cargo);
                    job.SetTarget(TargetIndex.A, cellule);
                }
                else
                {
                    // Plus une seule pile ne trouve de rangement : on déverse
                    // sur place plutôt que de promener la cargaison sans fin.
                    ToutDeverser();
                    EndJobWith(JobCondition.Succeeded);
                }
            };
            chercher.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return chercher;

            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            yield return Toils_General.Do(delegate
            {
                Thing cargo = job.targetB.Thing;
                if (cargo != null
                    && !pawn.inventory.innerContainer.TryDrop(cargo, job.targetA.Cell,
                        Map, ThingPlaceMode.Direct, out _))
                {
                    pawn.inventory.innerContainer.TryDrop(cargo, job.targetA.Cell,
                        Map, ThingPlaceMode.Near, out _);
                }
            });

            yield return Toils_Jump.Jump(chercher);
        }

        // La pile à bord dont le meilleur rangement est le plus proche de la
        // position actuelle de la bête. Faux s'il n'y a plus rien à ranger.
        private bool ProchaineLivraison(out Thing cargo, out IntVec3 cellule)
        {
            cargo = null;
            cellule = IntVec3.Invalid;
            float meilleure = float.MaxValue;
            ThingOwner contenu = pawn.inventory.innerContainer;
            for (int i = 0; i < contenu.Count; i++)
            {
                Thing t = contenu[i];
                if (EquipementUtility.EstEquipement(t.def))
                {
                    continue;
                }
                if (!StoreUtility.TryFindBestBetterStoreCellFor(t, pawn, Map,
                        StoragePriority.Unstored, pawn.Faction, out IntVec3 c))
                {
                    continue;
                }
                float dist = c.DistanceToSquared(pawn.Position);
                if (dist < meilleure)
                {
                    meilleure = dist;
                    cargo = t;
                    cellule = c;
                }
            }
            return cargo != null;
        }

        private void ToutDeverser()
        {
            ThingOwner contenu = pawn.inventory.innerContainer;
            for (int i = contenu.Count - 1; i >= 0; i--)
            {
                Thing t = contenu[i];
                if (!EquipementUtility.EstEquipement(t.def))
                {
                    contenu.TryDrop(t, pawn.Position, Map, ThingPlaceMode.Near, out _);
                }
            }
        }
    }
}
