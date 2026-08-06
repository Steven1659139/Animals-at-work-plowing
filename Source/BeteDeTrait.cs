using UnityEngine;
using Verse;

namespace AnimalsAtWork.Plowing
{
    // Le seul endroit qui décide si une espèce est une bête de trait, en
    // couches, de la plus forte à la plus faible :
    //
    //   1. retirée par le joueur          → non, quoi qu'en disent les autres ;
    //   2. ajoutée par le joueur          → oui ;
    //   3. extension de def               → oui (auteurs de mods, et les
    //      exceptions vanilla de Patch_EspecesDeTrait.xml) ;
    //   4. bête de somme au sens vanilla  → oui.
    //
    // La quatrième couche est celle qui fait le gros du travail sans que
    // personne n'ait rien à faire. packAnimal est le drapeau vanilla « bête qui
    // porte des charges » : il couvre cheval, muffalo, bison, yak, dromadaire,
    // éléphant et mastodonte, et un mod qui ajoute une grosse bête utile la
    // marque presque toujours pour qu'elle serve en caravane. Il n'attrape en
    // revanche ni mécanoïde, ni entité d'Anomaly, ni thrumbo, ni prédateur —
    // là où un simple seuil de gabarit aurait collé des interrupteurs de labour
    // sur des horreurs. La vache et l'âne n'étant pas des bêtes de somme, ils
    // restent marqués par l'extension.
    //
    // Le risque d'être large est faible : l'éligibilité ne fait qu'afficher les
    // interrupteurs sur la bête. MapComponent_Labour reste en opt-in strict,
    // rien ne travaille tant que le joueur n'a pas allumé une tâche.
    public static class BeteDeTrait
    {
        public static bool Est(ThingDef def)
        {
            if (def == null)
            {
                return false;
            }
            if (PlowingMod.EspeceRetiree(def))
            {
                return false;
            }
            return PlowingMod.EspeceAjoutee(def) || EstDorigine(def);
        }

        // Éligible sans que le joueur ait rien touché. Sert à la fenêtre de
        // réglages : elle ne mémorise un choix que s'il diffère de celui-ci.
        public static bool EstDorigine(ThingDef def)
        {
            return ParExtension(def) || ParBat(def);
        }

        public static bool ParExtension(ThingDef def)
        {
            return def != null && def.GetModExtension<ModExtension_BeteDeTrait>() != null;
        }

        public static bool ParBat(ThingDef def)
        {
            return def?.race != null && def.race.packAnimal;
        }

        // Rendement au travail, partagé par le labour et le déneigement : une
        // bête plus grosse tire plus vite. Facteur multiplicateur de la durée
        // d'une case, 1 pour le gabarit de référence.
        private const float GabaritReference = 2.4f; // vache, cheval, muffalo, bison
        private const float FacteurPlafond = 0.6f;   // atteint par l'éléphant (4.0)
        private const float GabaritAlpaga = 1.0f;    // plus petite bête de bât vanilla
        private const float FacteurAlpaga = 1.8f;    // ce qu'elle a toujours valu
        // Un poulet (0.3) est déjà à ce maximum. Plus bas, on ne distingue plus :
        // une case interrompue en chemin est reprise de zéro, donc l'allonger
        // sans fin ne punirait plus, elle empêcherait simplement d'aboutir.
        private const float FacteurMax = 6f;

        public static float FacteurDuree(Pawn bete)
        {
            float gabarit = bete.BodySize;
            if (gabarit >= GabaritAlpaga)
            {
                return Mathf.Clamp(GabaritReference / gabarit, FacteurPlafond, FacteurAlpaga);
            }
            // Sous l'alpaga, le palier disparaît : rien ne justifiait qu'un
            // écureuil laboure à sa vitesse. La courbe repart de son facteur.
            return Mathf.Clamp(FacteurAlpaga / gabarit, FacteurAlpaga, FacteurMax);
        }
    }
}
