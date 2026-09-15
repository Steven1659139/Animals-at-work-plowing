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
            Toil search = ToilMaker.MakeToil("ChercherRangement");
            search.initAction = delegate
            {
                if (EquipementUtility.FirstCargo(pawn) == null)
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                // On livre la pile dont le rangement est le plus proche d'ici,
                // et pas la première venue dans l'inventaire : sinon chaque
                // dépôt peut renvoyer la bête à l'autre bout de la colonie.
                if (NextDelivery(out Thing cargo, out IntVec3 cell))
                {
                    job.SetTarget(TargetIndex.B, cargo);
                    job.SetTarget(TargetIndex.A, cell);
                }
                else
                {
                    // Plus une seule pile ne trouve de rangement : on déverse
                    // sur place plutôt que de promener la cargaison sans fin.
                    DumpAll();
                    // Et on pose un répit : sans lui, la tournée suivante
                    // reprendrait ce qu'on vient de poser, et la bête
                    // tournerait en boucle jusqu'à détruire sa charrette.
                    MapComponent_Labour.Of(Map)?.NoteDump(pawn);
                    EndJobWith(JobCondition.Succeeded);
                }
            };
            search.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return search;

            // ClosestTouch, comme les porteurs vanilla : déposer ne demande pas
            // d'être sur la case, et un rangement qu'on ne peut que toucher
            // (meuble infranchissable) reste un rangement.
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.ClosestTouch);

            // La case a pu se remplir pendant le trajet (un colon y a rangé
            // autre chose) : on repart chercher un rangement plutôt que de
            // poser la pile à côté, hors stock, où la tournée suivante la
            // reprendrait.
            yield return Toils_Jump.JumpIf(search, () => job.targetB.Thing != null
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

            yield return Toils_Jump.Jump(search);
        }

        // La pile à bord dont le meilleur rangement est le plus proche de la
        // position actuelle de la bête. Faux s'il n'y a plus rien à ranger.
        private bool NextDelivery(out Thing cargo, out IntVec3 cell)
        {
            cargo = null;
            cell = IntVec3.Invalid;
            float best = float.MaxValue;
            ThingOwner contents = pawn.inventory.innerContainer;
            for (int i = 0; i < contents.Count; i++)
            {
                Thing t = contents[i];
                if (EquipementUtility.IsEquipment(t.def))
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
                if (dist < best)
                {
                    best = dist;
                    cargo = t;
                    cell = c;
                }
            }
            return cargo != null;
        }

        private void DumpAll()
        {
            ThingOwner contents = pawn.inventory.innerContainer;
            // Un déversement veut dire qu'une pile est entrée dans la charrette
            // alors qu'elle ne pouvait plus en sortir : le ramassage et la
            // livraison n'ont pas répondu pareil sur la même pile. On trace
            // laquelle, faute de quoi la cause reste invisible en jeu. Le répit
            // qui suit borne la trace à une par heure et par bête.
            if (Prefs.DevMode)
            {
                Thing first = EquipementUtility.FirstCargo(pawn);
                Log.Warning($"[AAW] {pawn.LabelShort} déverse en {pawn.Position} : "
                    + $"aucun rangement pour {contents.Count} pile(s), "
                    + $"dont {first?.LabelCap ?? "-"}.");
            }
            for (int i = contents.Count - 1; i >= 0; i--)
            {
                Thing t = contents[i];
                if (!EquipementUtility.IsEquipment(t.def))
                {
                    contents.TryDrop(t, pawn.Position, Map, ThingPlaceMode.Near, out _);
                }
            }
        }
    }
}
