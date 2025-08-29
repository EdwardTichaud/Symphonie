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

    [Tooltip("Dialogues alternatifs déclenchés selon l'état des GameData.")]
    public ConditionalDialogue[] alternateDialogues;

    [Tooltip("Timeline à lancer avant le dialogue (optionnel).")]
    public TimelineAsset timeline; // ⚙️ Remplacé PlayableDirector par TimelineAsset pour lecture via TimelineLauncher

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
    /// Retourne le dialogue adapté en fonction des succès enregistrés.
    /// </summary>
    public DialogueContainer GetDialogue(GameData data)
    {
        if (data != null && alternateDialogues != null)
        {
            foreach (var alt in alternateDialogues)
            {
                if (alt != null && alt.IsConditionMet(data))
                    return alt.dialogue;
            }
        }

        return dialogue; // Dialogue par défaut si aucune condition n'est remplie
    }
}
