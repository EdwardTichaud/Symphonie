using UnityEngine;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// Point d'intérêt interactif : lance un dialogue puis éventuellement
/// une confirmation Oui/Non pour que le joueur prenne une décision.
/// </summary>
public class PointOfInterest : MonoBehaviour, IInteractable
{
    [Header("Dialogue")]
    [Tooltip("Dialogue joué lorsque le joueur interagit avec ce point d'intérêt.")]
    public DialogueContainer dialogue;

    [Header("Confirmation")]
    [Tooltip("Ouvre une boîte de confirmation à la fin du dialogue.")]
    public bool askConfirmation;
    [Tooltip("Texte affiché dans la boîte de confirmation.")]
    [TextArea]
    public string confirmationText;
    [Tooltip("Événement déclenché si le joueur répond Oui.")]
    public UnityEvent onYes;
    [Tooltip("Événement déclenché si le joueur répond Non.")]
    public UnityEvent onNo;

    // --- Implémentation de l'interface IInteractable ---
    public GameObject GameObject => gameObject;

    public void Interact()
    {
        if (DialogueManager.Instance.isOpen || EventsManager.Instance.eventInProgress)
            return; // Empêche les interactions multiples

        StartCoroutine(RunInteraction());
    }

    public void IncrementDialogueStage() { /* Aucun dialogue progressif pour l'instant */ }

    /// <summary>
    /// Coroutine gérant l'enchaînement dialogue puis confirmation.
    /// </summary>
    private IEnumerator RunInteraction()
    {
        EventsManager.Instance.eventInProgress = true; // Bloque les autres interactions

        if (dialogue != null)
            yield return DialogueManager.Instance.StartDialogue(dialogue);

        if (askConfirmation)
        {
            bool done = false;
            ConfirmationBox.Instance.Show(
                confirmationText,
                () => { onYes?.Invoke(); done = true; },
                () => { onNo?.Invoke(); done = true; }
            );
            while (!done)
                yield return null; // Attend la décision du joueur
        }

        EventsManager.Instance.eventInProgress = false; // Libère les interactions
    }
}
