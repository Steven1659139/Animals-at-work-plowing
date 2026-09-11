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
        private readonly List<GlobalTargetInfo> coupables = new List<GlobalTargetInfo>();

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
            coupables.Clear();
            if (!AAW_DefOf.AAW_Harnachement.IsFinished)
            {
                return false;
            }
            foreach (Map map in Find.Maps)
            {
                if (Disponible(map, AAW_DefOf.AAW_HarnaisDeTrait))
                {
                    continue;
                }
                MapComponent_Labour composante = MapComponent_Labour.De(map);
                bool attelageEnAttente = Disponible(map, AAW_DefOf.AAW_Charrue)
                    || Disponible(map, AAW_DefOf.AAW_Charrette)
                    || Disponible(map, AAW_DefOf.AAW_Grattoir);
                foreach (Pawn animal in map.mapPawns.SpawnedColonyAnimals)
                {
                    // Seules les bêtes que le joueur a marquées (opt-in) comptent :
                    // un colon voudra les équiper mais aucun harnais n'est là.
                    if (!BeteDeTrait.Est(animal.def)
                        || !composante.EstBeteDeTrait(animal)
                        || EquipementUtility.Porte(animal, AAW_DefOf.AAW_HarnaisDeTrait) != null)
                    {
                        continue;
                    }
                    if (attelageEnAttente || EquipementUtility.AttelagePorte(animal) != null)
                    {
                        coupables.Add(animal);
                    }
                }
            }
            return AlertReport.CulpritsAre(coupables);
        }

        private static bool Disponible(Map map, ThingDef def)
        {
            List<Thing> objets = map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < objets.Count; i++)
            {
                if (!objets[i].IsForbidden(Faction.OfPlayer))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
