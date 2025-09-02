using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Events; // Permet d'ajouter des callbacks via l'inspecteur

/// <summary>
/// Représente une phase complète de dialogue pour un PNJ.
/// Chaque phase peut jouer une Timeline spécifique et enchaîner automatiquement.
/// </summary>
[System.Serializable]
public class NPCDialoguePhase
{
    [Tooltip("Dialogue principal associé à cette phase.")]
    public DialogueContainer dialogue;

    // Variantes déclenchées après le premier passage et en fonction des succès.
    [Tooltip("Dialogues alternatifs joués après le dialogue principal si un succès est débloqué.")]
    public ConditionalDialogue[] alternateDialogues;

    // Indique si le dialogue principal a déjà été joué au moins une fois.
    // Non sérialisé pour éviter d'encombrer l'inspecteur.
    [System.NonSerialized] public bool mainDialoguePlayed;

    [Tooltip("Timeline à lancer avant le dialogue (optionnel).")]
    public TimelineAsset timeline; // ⚙️ Remplacé PlayableDirector par TimelineAsset pour lecture via TimelineManager

    [Tooltip("Si activé, enchaîne automatiquement avec la phase suivante.")]
    public bool autoProceed;

    [Header("Confirmation")]
    [Tooltip("Ouvre une boîte de confirmation à la fin de cette phase.")]
    public bool askConfirmation;

    [Tooltip("Texte affiché dans la boîte de confirmation.")]
    [TextArea]
    public string confirmationText;

    [Tooltip("Événement déclenché si le joueur répond Oui.")]
    public UnityEvent onYes;

    [Tooltip("Événement déclenché si le joueur répond Non.")]
    public UnityEvent onNo;

    /// <summary>
    /// Retourne le dialogue approprié.
    /// Le dialogue principal est joué une première fois, puis les variantes
    /// sont évaluées en fonction des succès débloqués.
    /// </summary>
    public DialogueContainer GetDialogue()
    {
        // Si le dialogue principal n'a jamais été joué, on le renvoie
        if (!mainDialoguePlayed)
        {
            mainDialoguePlayed = true; // Marque le passage
            return dialogue;
        }

        // Ensuite, on vérifie les dialogues alternatifs
        if (alternateDialogues != null)
        {
            foreach (var alt in alternateDialogues)
            {
                if (alt != null && alt.IsConditionMet(mainDialoguePlayed))
                    return alt.dialogue;
            }
        }

        // Par défaut, on rejoue le dialogue principal
        return dialogue;
    }
}
