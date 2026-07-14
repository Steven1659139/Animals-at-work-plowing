using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Habille un toil de travail de l'effet visuel et du son vanilla du
    // métier correspondant (référencés par nom : absents, rien ne casse).
    public static class Ambiance
    {
        public static void Habiller(Toil toil, TargetIndex cible, string effet, string son)
        {
            EffecterDef defEffet = DefDatabase<EffecterDef>.GetNamedSilentFail(effet);
            if (defEffet != null)
            {
                toil.WithEffect(defEffet, cible);
            }
            SoundDef defSon = DefDatabase<SoundDef>.GetNamedSilentFail(son);
            if (defSon != null)
            {
                toil.PlaySustainerOrSound(defSon);
            }
        }
    }
}
