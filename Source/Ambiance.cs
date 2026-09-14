using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Habille un toil de travail de l'effet visuel et du son vanilla du
    // métier correspondant (référencés par nom : absents, rien ne casse).
    public static class Ambiance
    {
        public static void Dress(Toil toil, TargetIndex target, string effect, string sound)
        {
            EffecterDef effectDef = DefDatabase<EffecterDef>.GetNamedSilentFail(effect);
            if (effectDef != null)
            {
                toil.WithEffect(effectDef, target);
            }
            SoundDef soundDef = DefDatabase<SoundDef>.GetNamedSilentFail(sound);
            if (soundDef != null)
            {
                toil.PlaySustainerOrSound(soundDef);
            }
        }
    }
}
