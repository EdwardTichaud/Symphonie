using UnityEngine;
using System.Collections;

/// <summary>
/// PNJ "Le Vieux" : gère plusieurs phases de dialogue en fonction
/// du nombre d'interactions du joueur.
/// </summary>
public class NPC_LeVieux : MonoBehaviour, IInteractable
{
    [Header("Dialogues du PNJ")]
    [Tooltip("Liste des phases de dialogue (0 = première interaction, etc.).")]
    public DialogueContainer[] dialoguePhases; // Références vers les dialogues successifs

    // Indice de la prochaine phase à jouer
    private int dialogueStage = 0;

    // --- Implémentation de IInteractable ---
    public GameObject GameObject => gameObject;

    /// <summary>
    /// Déclenche le dialogue correspondant à la phase en cours
    /// si aucune autre interaction n'est active.
    /// </summary>
    public void Interact()
    {
        if (DialogueManager.Instance.isOpen || EventsManager.Instance.eventInProgress)
        {
            // Empêche les dialogues simultanés ou les cinématiques concurrentes
            return;
        }

        if (dialogueStage >= dialoguePhases.Length)
        {
            // Toutes les phases ont été jouées : aucune interaction supplémentaire
            return;
        }

        // Lance le dialogue approprié
        StartCoroutine(PlayDialogue());
    }

    /// <summary>
    /// Lance le dialogue de la phase courante, puis incrémente la phase.
    /// </summary>
    private IEnumerator PlayDialogue()
    {
        // Signale qu'un événement est en cours pour bloquer d'autres interactions
        EventsManager.Instance.eventInProgress = true;

        DialogueContainer container = dialoguePhases[dialogueStage];
        if (container != null)
        {
            // Démarre le dialogue et attend sa fin en prenant en compte la position de la bulle
            yield return DialogueManager.Instance.StartDialogue(container);
        }

        // Prépare la phase suivante
        IncrementDialogueStage();

        // Libère le verrou d'événement
        EventsManager.Instance.eventInProgress = false;
    }

    /// <summary>
    /// Avance à la prochaine phase de dialogue.
    /// </summary>
    public void IncrementDialogueStage()
    {
        // Évite de dépasser la taille du tableau
        dialogueStage = Mathf.Min(dialogueStage + 1, dialoguePhases.Length);
    }
}
