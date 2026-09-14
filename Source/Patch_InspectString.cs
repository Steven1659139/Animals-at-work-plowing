using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AnimalsAtWork.Plowing
{
    // Affiche l'état du harnais, de la charrette et de la cargaison dans le
    // panneau d'inspection de l'animal (là où le joueur regarde) plutôt
    // que d'enfouir l'information dans l'onglet Équipement.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
    public static class Patch_InspectString
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            // Diagnostic (mode Dév uniquement) : pourquoi le colon (n')agit(-il)
            // (pas) sur cette bête. Sélectionne la bête et lis la ligne [AAW dev].
            if (Prefs.DevMode)
            {
                string diag = Diagnostic(__instance);
                if (diag != null)
                {
                    Add(ref __result, diag);
                }
            }

            Thing harness = EquipementUtility.Carries(__instance, AAW_DefOf.AAW_HarnaisDeTrait);
            if (harness != null)
            {
                Add(ref __result, "AAW_InspectHarnais".Translate(Percent(harness)));
            }
            Thing plow = EquipementUtility.Carries(__instance, AAW_DefOf.AAW_Charrue);
            if (plow != null)
            {
                Add(ref __result, "AAW_InspectCharrue".Translate(Percent(plow)));
            }
            Thing cart = EquipementUtility.Carries(__instance, AAW_DefOf.AAW_Charrette);
            if (cart != null)
            {
                Add(ref __result, "AAW_InspectCharrette".Translate(Percent(cart)));
            }
            Thing scraper = EquipementUtility.Carries(__instance, AAW_DefOf.AAW_Grattoir);
            if (scraper != null)
            {
                Add(ref __result, "AAW_InspectGrattoir".Translate(Percent(scraper)));
            }
            float mass = EquipementUtility.CargoMass(__instance);
            if (mass > 0f)
            {
                Add(ref __result, "AAW_InspectCargaison".Translate(
                    mass.ToString("F0"), EquipementUtility.CartCapacity.ToString("F0")));
            }
        }

        // État de la bête au moment où le colon devrait l'équiper puis la mener :
        // service, tâche, job en cours (révèle une boucle), équipement porté,
        // et état de la corde. Null si ce n'est pas une bête de trait du joueur.
        private static string Diagnostic(Pawn beast)
        {
            if (!beast.Spawned
                || beast.Faction != Faction.OfPlayer
                || !BeteDeTrait.Is(beast.def))
            {
                return null;
            }
            MapComponent_Labour c = beast.Map.GetComponent<MapComponent_Labour>();
            Thing implement = EquipementUtility.CarriedImplement(beast);
            TacheTrait task = ServiceTrait.TaskToServe(beast, c);
            float beastSize = MapComponent_Charrettes.DrawnSize(beast);
            return "[AAW dev] "
                + $"service:{c.IsOnDuty(beast)} "
                + $"tâche:{task} "
                + $"jobBête:{beast.CurJob?.def.defName ?? "-"}\n"
                + $"harnais:{EquipementUtility.Carries(beast, AAW_DefOf.AAW_HarnaisDeTrait) != null} "
                + $"attelage:{implement?.def.defName ?? "-"}\n"
                // Taille de l'attelage dessiné : « facteur:1,00 » sur un
                // éléphant dirait que la lecture du gabarit échoue, et non que
                // la pièce est cachée sous le sprite.
                + $"gabarit:{beastSize:F2} "
                + $"facteur:{MapComponent_Charrettes.Factor(beastSize):F2}\n"
                // Pourquoi la bête ne sort pas pour la charrette. « piles:0/2 »
                // avec des tas partout dit que rien n'a de stock où aller ;
                // « répit » dit qu'elle vient de déverser ou de rentrer.
                + $"piles:{ServiceTrait.CountStacks(beast.Map, beast)}/2 "
                + $"répitRetour:{c.InReturnGrace(beast)} "
                + $"répitDéversement:{c.InDumpGrace(beast)}\n"
                // Une pièce absente de la carte fait échouer l'étape « équiper »
                // en silence, et donc disparaître le clic droit : on les compte
                // toutes les quatre.
                + $"surCarte harnais:{Count(beast, AAW_DefOf.AAW_HarnaisDeTrait)} "
                + $"charrue:{Count(beast, AAW_DefOf.AAW_Charrue)} "
                + $"grattoir:{Count(beast, AAW_DefOf.AAW_Grattoir)} "
                + $"charrette:{Count(beast, AAW_DefOf.AAW_Charrette)}\n"
                + $"encordée:{beast.roping.IsRoped} "
                + $"par:{beast.roping.RopedByPawn?.LabelShort ?? "-"}\n"
                + Destination(beast, task);
        }

        // Où un colon pourrait la mener pour la tâche du moment. « cible:- » avec
        // une tâche non nulle signifie qu'il y a du travail quelque part mais
        // aucune case joignable en menant la bête (clôture sans portail, porte
        // fermée…) : c'est ce cas-là qui fait qu'aucun colon ne vient.
        private static string Destination(Pawn beast, TacheTrait task)
        {
            if (task == TacheTrait.Aucune)
            {
                return "cible:aucune tâche";
            }
            List<Pawn> colonists = beast.Map.mapPawns.FreeColonistsSpawned;
            if (colonists.Count == 0)
            {
                return "cible:aucun colon";
            }
            Pawn colonist = colonists[0];
            string target = ServiceTrait.FindWorkCell(beast, colonist, task, out IntVec3 c)
                ? c.ToString()
                : "-";
            return $"cible:{target} (menée par {colonist.LabelShort})";
        }

        private static int Count(Pawn beast, ThingDef def)
        {
            return beast.Map.listerThings.ThingsOfDef(def).Count;
        }

        private static string Percent(Thing item)
        {
            return (100f * item.HitPoints / item.MaxHitPoints).ToString("F0");
        }

        private static void Add(ref string text, string line)
        {
            text = text.NullOrEmpty() ? line : text + "\n" + line;
        }
    }
}
