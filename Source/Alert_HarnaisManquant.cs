using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace AnimalsAtWork.Plowing
{
    // Alerte quand des bêtes marquées par le joueur attendent un harnais
    // qu'aucun colon ne peut leur apporter : plus un seul harnais disponible
    // sur la carte, alors qu'un attelage est déjà porté ou attend au sol.
    // Instanciée automatiquement par AlertsReadout, comme toute sous-classe
    // d'Alert.
    public class Alert_HarnaisManquant : Alert
    {
        private readonly List<GlobalTargetInfo> culprits = new List<GlobalTargetInfo>();

        public override string GetLabel()
        {
            return "AAW_AlerteHarnais".Translate();
        }

        public override TaggedString GetExplanation()
        {
            return "AAW_AlerteHarnaisDesc".Translate();
        }

        public override AlertReport GetReport()
        {
            culprits.Clear();
            if (!AAW_DefOf.AAW_Harnachement.IsFinished)
            {
                return false;
            }
            foreach (Map map in Find.Maps)
            {
                if (Available(map, AAW_DefOf.AAW_HarnaisDeTrait))
                {
                    continue;
                }
                MapComponent_Labour component = MapComponent_Labour.Of(map);
                bool implementPending = Available(map, AAW_DefOf.AAW_Charrue)
                    || Available(map, AAW_DefOf.AAW_Charrette)
                    || Available(map, AAW_DefOf.AAW_Grattoir);
                foreach (Pawn animal in map.mapPawns.SpawnedColonyAnimals)
                {
                    // Seules les bêtes que le joueur a marquées (opt-in) comptent :
                    // un colon voudra les équiper mais aucun harnais n'est là.
                    if (!BeteDeTrait.Is(animal.def)
                        || !component.IsDraftBeast(animal)
                        || EquipementUtility.Carries(animal, AAW_DefOf.AAW_HarnaisDeTrait) != null)
                    {
                        continue;
                    }
                    if (implementPending || EquipementUtility.CarriedImplement(animal) != null)
                    {
                        culprits.Add(animal);
                    }
                }
            }
            return AlertReport.CulpritsAre(culprits);
        }

        private static bool Available(Map map, ThingDef def)
        {
            List<Thing> items = map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < items.Count; i++)
            {
                if (!items[i].IsForbidden(Faction.OfPlayer))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
