using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace AnimalsAtWork.Plowing
{
    // Passerelle avec le module Herding Dogs : un chien dressé au troupeau mène
    // les bêtes de trait à leur travail et les ramène à l'enclos, à la place du
    // colon. Il ne harnache pas — apporter le harnais et le boucler demande des
    // mains, le colon reste nécessaire pour ça. Le partage est donc : le colon
    // équipe la bête dans l'enclos, le chien fait l'aller-retour jusqu'au champ.
    //
    // Ce nœud n'est jamais atteint sans le module Herding Dogs : il est inséré
    // dans l'arbre de pensée sous la condition de son dressage AAW_Troupeau, et
    // MayRequire l'efface si le module est absent. C'est bien nous qui posons ce
    // patch (Patches/Patch_ArbrePensee_Meneur.xml) et non Herding Dogs, alors
    // que le dressage lui appartient : la classe doit voyager avec le nœud qui
    // la nomme, sans quoi une version dépareillée des deux mods fait rejeter
    // l'arbre « Animal » vanilla en entier. Voir le commentaire du patch.
    public class JobGiver_Meneur : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn chien)
        {
            Map map = chien.Map;
            if (map == null || chien.Faction != Faction.OfPlayer)
            {
                return null;
            }

            MapComponent_Labour composante = map.GetComponent<MapComponent_Labour>();
            // Un éléphant peut être dressé au troupeau et attelé à une charrue :
            // en service, il travaille, il ne mène pas les autres.
            if (composante.EstEnService(chien))
            {
                return null;
            }
            // Scan throttlé par chien : décider quoi faire d'une bête de trait
            // finit par chercher une case de travail (parcours des zones avec
            // atteignabilité), et rien là-dedans ne demande une réaction
            // immédiate. Même raison que les scans de MapComponent_Bergerie.
            if (!composante.PeutScannerMeneur(chien))
            {
                return null;
            }

            List<Pawn> animaux = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
            for (int i = 0; i < animaux.Count; i++)
            {
                Pawn bete = animaux[i];
                if (bete == chien || !bete.RaceProps.Animal)
                {
                    continue;
                }
                if (!BeteDeTrait.Est(bete.def))
                {
                    continue;
                }
                // Mêmes filtres que le colon (WorkGiver_BeteDeTrait) : ni marquée
                // ni en service, déjà menée par quelqu'un, ou en crise, on passe.
                if (!composante.EstBeteDeTrait(bete) && !composante.EstEnService(bete))
                {
                    continue;
                }
                if (bete.roping.IsRoped || bete.InMentalState)
                {
                    continue;
                }
                if (!chien.CanReserveAndReach(bete, PathEndMode.Touch, Danger.Some))
                {
                    continue;
                }
                Job job = ServiceTrait.JobDeService(chien, bete, composante, false, false);
                if (job != null)
                {
                    return job;
                }
            }
            return null;
        }
    }
}
