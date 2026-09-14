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
                    // Et on pose un répit : sans lui, la tournée suivante
                    // reprendrait ce qu'on vient de poser, et la bête
                    // tournerait en boucle jusqu'à détruire sa charrette.
                    MapComponent_Labour.De(Map)?.NoterDeversement(pawn);
                    EndJobWith(JobCondition.Succeeded);
                }
            };
            chercher.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return chercher;

            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            // La case a pu se remplir pendant le trajet (un colon y a rangé
            // autre chose) : on repart chercher un rangement plutôt que de
            // poser la pile à côté, hors stock, où la tournée suivante la
            // reprendrait.
            yield return Toils_Jump.JumpIf(chercher, () => job.targetB.Thing != null
                && !job.targetA.Cell.IsValidStorageFor(Map, job.targetB.Thing));

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
                // Même test qu'au chargement (JobGiver_Charretier.Destination),
                // accessibilité comprise : ce qui est monté dans la charrette
                // doit pouvoir en descendre quelque part, sinon la bête repose
                // sa pile là où elle l'a prise et recommence.
                if (!JobGiver_Charretier.Destination(pawn, Map, t,
                        StoragePriority.Unstored, out IntVec3 c))
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
            // Un déversement veut dire qu'une pile est entrée dans la charrette
            // alors qu'elle ne pouvait plus en sortir : le ramassage et la
            // livraison n'ont pas répondu pareil sur la même pile. On trace
            // laquelle, faute de quoi la cause reste invisible en jeu. Le répit
            // qui suit borne la trace à une par heure et par bête.
            if (Prefs.DevMode)
            {
                Thing premier = EquipementUtility.PremierCargo(pawn);
                Log.Warning($"[AAW] {pawn.LabelShort} déverse en {pawn.Position} : "
                    + $"aucun rangement pour {contenu.Count} pile(s), "
                    + $"dont {premier?.LabelCap ?? "-"}.");
            }
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
