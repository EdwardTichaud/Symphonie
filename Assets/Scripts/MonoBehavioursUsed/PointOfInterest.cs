using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.Playables;   // Nécessaire pour contrôler la Timeline
using UnityEngine.Timeline;    // Permet de manipuler les Signaux (SignalAsset)

/// <summary>
/// Point d'intérêt interactif : lance un dialogue puis éventuellement
/// une confirmation Oui/Non pour que le joueur prenne une décision.
/// </summary>
public class PointOfInterest : MonoBehaviour, IInteractable, ILocalInfoBoxTarget
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

    [Header("Timeline")]
    [Tooltip("Timeline à relancer une fois le dialogue terminé.")]
    public PlayableDirector timelineToResume;
    [Tooltip("Signal envoyé pour indiquer à la timeline de reprendre.")]
    public SignalAsset resumeSignal;

    [Tooltip("Timeline à lancer via le TimelineLauncher à la fin du dialogue.")]
    public TimelineAsset timelineToLaunch;
    [Tooltip("Objet utilisé comme 'caster' pour la timeline lancée (par défaut ce point d'intérêt).")]
    public GameObject timelineCaster;
    [Tooltip("Tag de la caméra utilisé pour la timeline lancée.")]
    public string timelineCameraTag = "WorldCamera";

    [Header("Local InfoBox")]
    [Tooltip("Décalage appliqué à la LocalInfoBox pour ce point d'intérêt.")]
    public Vector3 localInfoBoxOffset;

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
    /// Fournit l'offset personnalisé pour positionner la LocalInfoBox.
    /// </summary>
    public Vector3 LocalInfoBoxOffset => localInfoBoxOffset;

    /// <summary>
    /// Coroutine gérant l'enchaînement dialogue puis confirmation.
    /// </summary>
    private IEnumerator RunInteraction()
    {
        EventsManager.Instance.eventInProgress = true; // Bloque les autres interactions

        if (dialogue != null)
            yield return DialogueManager.Instance.StartDialogue(dialogue);

        // À la fin du dialogue, on envoie un signal de reprise à la Timeline si nécessaire
        if (timelineToResume != null)
        {
            // Vérifie si un SignalReceiver est attaché pour traiter le SIG_Resume
            if (resumeSignal != null)
            {
                var receiver = timelineToResume.GetComponent<SignalReceiver>();
                if (receiver != null)
                {
                    // Invoque la réaction associée au signal pour laisser la Timeline gérer la reprise
                    receiver.GetReaction(resumeSignal)?.Invoke();
                }
                else
                {
                    // Pas de SignalReceiver configuré : on reprend la Timeline directement
                    timelineToResume.Resume();
                }
            }
            else
            {
                // Aucun signal défini : reprise immédiate de la Timeline
                timelineToResume.Resume();
            }
        }

        // Lance une nouvelle Timeline via le TimelineLauncher si demandée
        if (timelineToLaunch != null && TimelineLauncher.Instance != null)
        {
            // Détermine le GameObject utilisé pour les tracks "Caster" (par défaut : ce point d'intérêt)
            GameObject caster = timelineCaster != null ? timelineCaster : gameObject;

            // Lance la timeline en utilisant le PlayableDirector centralisé
            TimelineLauncher.Instance.PlayTimeline(timelineToLaunch, caster, timelineCameraTag);

            // Attend la fin de la timeline pour que la suite ne démarre pas trop tôt
            while (TimelineLauncher.Instance.IsTimelineActive)
                yield return null;
        }

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
