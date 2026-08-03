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
                    Ajouter(ref __result, diag);
                }
            }

            Thing harnais = EquipementUtility.Porte(__instance, AAW_DefOf.AAW_HarnaisDeTrait);
            if (harnais != null)
            {
                Ajouter(ref __result, "AAW_InspectHarnais".Translate(Pourcent(harnais)));
            }
            Thing charrue = EquipementUtility.Porte(__instance, AAW_DefOf.AAW_Charrue);
            if (charrue != null)
            {
                Ajouter(ref __result, "AAW_InspectCharrue".Translate(Pourcent(charrue)));
            }
            Thing charrette = EquipementUtility.Porte(__instance, AAW_DefOf.AAW_Charrette);
            if (charrette != null)
            {
                Ajouter(ref __result, "AAW_InspectCharrette".Translate(Pourcent(charrette)));
            }
            Thing grattoir = EquipementUtility.Porte(__instance, AAW_DefOf.AAW_Grattoir);
            if (grattoir != null)
            {
                Ajouter(ref __result, "AAW_InspectGrattoir".Translate(Pourcent(grattoir)));
            }
            float masse = EquipementUtility.MasseCargaison(__instance);
            if (masse > 0f)
            {
                Ajouter(ref __result, "AAW_InspectCargaison".Translate(
                    masse.ToString("F0"), EquipementUtility.CapaciteCharrette.ToString("F0")));
            }
        }

        // État de la bête au moment où le colon devrait l'équiper puis la mener :
        // service, tâche, job en cours (révèle une boucle), équipement porté,
        // et état de la corde. Null si ce n'est pas une bête de trait du joueur.
        private static string Diagnostic(Pawn bete)
        {
            if (!bete.Spawned
                || bete.Faction != Faction.OfPlayer
                || !BeteDeTrait.Est(bete.def))
            {
                return null;
            }
            MapComponent_Labour c = bete.Map.GetComponent<MapComponent_Labour>();
            Thing attelage = EquipementUtility.AttelagePorte(bete);
            TacheTrait tache = ServiceTrait.TacheAServir(bete, c);
            return "[AAW dev] "
                + $"service:{c.EstEnService(bete)} "
                + $"tâche:{tache} "
                + $"jobBête:{bete.CurJob?.def.defName ?? "-"}\n"
                + $"harnais:{EquipementUtility.Porte(bete, AAW_DefOf.AAW_HarnaisDeTrait) != null} "
                + $"attelage:{attelage?.def.defName ?? "-"}\n"
                // Une pièce absente de la carte fait échouer l'étape « équiper »
                // en silence, et donc disparaître le clic droit : on les compte
                // toutes les quatre.
                + $"surCarte harnais:{Compte(bete, AAW_DefOf.AAW_HarnaisDeTrait)} "
                + $"charrue:{Compte(bete, AAW_DefOf.AAW_Charrue)} "
                + $"grattoir:{Compte(bete, AAW_DefOf.AAW_Grattoir)} "
                + $"charrette:{Compte(bete, AAW_DefOf.AAW_Charrette)}\n"
                + $"encordée:{bete.roping.IsRoped} "
                + $"par:{bete.roping.RopedByPawn?.LabelShort ?? "-"}\n"
                + Destination(bete, tache);
        }

        // Où un colon pourrait la mener pour la tâche du moment. « cible:- » avec
        // une tâche non nulle signifie qu'il y a du travail quelque part mais
        // aucune case joignable en menant la bête (clôture sans portail, porte
        // fermée…) : c'est ce cas-là qui fait qu'aucun colon ne vient.
        private static string Destination(Pawn bete, TacheTrait tache)
        {
            if (tache == TacheTrait.Aucune)
            {
                return "cible:aucune tâche";
            }
            List<Pawn> colons = bete.Map.mapPawns.FreeColonistsSpawned;
            if (colons.Count == 0)
            {
                return "cible:aucun colon";
            }
            Pawn colon = colons[0];
            string cible = ServiceTrait.TrouverCelluleTravail(bete, colon, tache, out IntVec3 c)
                ? c.ToString()
                : "-";
            return $"cible:{cible} (menée par {colon.LabelShort})";
        }

        private static int Compte(Pawn bete, ThingDef def)
        {
            return bete.Map.listerThings.ThingsOfDef(def).Count;
        }

        private static string Pourcent(Thing objet)
        {
            return (100f * objet.HitPoints / objet.MaxHitPoints).ToString("F0");
        }

        private static void Ajouter(ref string texte, string ligne)
        {
            texte = texte.NullOrEmpty() ? ligne : texte + "\n" + ligne;
        }
    }
}
