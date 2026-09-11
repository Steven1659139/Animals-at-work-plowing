using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Tronc commun du labour et du déneigement : aller sur la case, y
    // travailler le temps que dicte le gabarit de la bête (barre de
    // progression, effet et son vanilla), abandonner si la case ne s'y prête
    // plus, puis agir sur la case et user l'outil et le harnais. Les
    // sous-classes ne disent que ce qui change d'un travail à l'autre.
    public abstract class JobDriver_TravailDeCase : JobDriver
    {
        // Durée d'une case pour le gabarit bovin (bodySize 2.4) ; la courbe des
        // gabarits vit dans BeteDeTrait.FacteurDuree.
        protected abstract int DureeBaseTicks { get; }

        // Effet visuel et son vanilla du travail (voir Ambiance).
        protected abstract string Effet { get; }
        protected abstract string Son { get; }

        // L'outil que le travail use, le nombre de cases qu'en tire un
        // exemplaire du matériau ordinaire, et le message quand il casse.
        protected abstract ThingDef Outil { get; }
        protected abstract int CasesParOutil { get; }
        protected abstract string CleOutilRompu { get; }

        protected IntVec3 Cellule => job.targetA.Cell;

        // La case se prête-t-elle encore au travail ? Vérifié à chaque tick
        // pendant l'ouvrage.
        protected abstract bool CelluleValide(MapComponent_Labour composante);

        // Ce que le travail fait de la case, une fois le temps écoulé.
        protected abstract void Travailler(MapComponent_Labour composante);

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            int duree = Mathf.RoundToInt(DureeBaseTicks * BeteDeTrait.FacteurDuree(pawn));
            Toil ouvrage = Toils_General.Wait(duree);
            ouvrage.WithProgressBarToilDelay(TargetIndex.A);
            Ambiance.Habiller(ouvrage, TargetIndex.A, Effet, Son);
            ouvrage.FailOn(() => !CelluleValide(MapComponent_Labour.De(pawn.Map)));
            yield return ouvrage;

            yield return Toils_General.Do(delegate
            {
                Travailler(MapComponent_Labour.De(pawn.Map));
                // Chaque case use l'outil, et un peu le harnais ; brisés, un
                // colon rééquipera la bête.
                EquipementUtility.User(pawn, Outil, CasesParOutil, CleOutilRompu);
                EquipementUtility.User(pawn, AAW_DefOf.AAW_HarnaisDeTrait,
                    EquipementUtility.UsagesParHarnais, "AAW_HarnaisRompu");
            });
        }
    }
}
