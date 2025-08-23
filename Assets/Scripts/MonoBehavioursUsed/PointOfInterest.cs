using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.Playables;

public class PointOfInterest : MonoBehaviour, IInteractable, ILocalInfoBoxTarget
{
    [Header("Fades")]
    [Tooltip("Cochez pour faire un fondu au noir des enfants de ce point d'intérêt avant le dialogue.")]
    public bool blackFade;
    [Tooltip("Cochez pour faire un fondu au blanc des enfants de ce point d'intérêt avant le dialogue.")]
    public bool whiteFade;

    [Header("Dialogue")]
    [Tooltip("Dialogue joué lorsque le joueur interagit avec ce point d'intérêt.")]
    public DialogueContainer dialogue;

    [Header("Timeline (direct)")]
    [Tooltip("Cochez pour lancer un PlayableDirector à la fin du dialogue.")]
    public bool launchTimeline = false;
    [Tooltip("PlayableDirector à lancer directement (director.Play()).")]
    public PlayableDirector directorToPlay;

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

    public Vector3 LocalInfoBoxOffset => localInfoBoxOffset;

    private IEnumerator RunInteraction()
    {
        EventsManager.Instance.eventInProgress = true;

        // 0) Optionnel : 
        if (whiteFade)
            FadeChildrenOpacity.Instance.ChangeOpacity(0, 1f, 2f);

        if (blackFade)
            FadeChildrenOpacity.Instance.ChangeOpacity(1, 1f, 2f);

        // 1) Dialogue
        if (dialogue != null)
            yield return DialogueManager.Instance.StartDialogue(dialogue);

        // 2) Lancer directement le PlayableDirector, si demandé
        if (launchTimeline && directorToPlay != null)
        {
            // Optionnel : stopper une éventuelle lecture en cours pour repartir proprement
            if (directorToPlay.state == PlayState.Playing)
                directorToPlay.Stop();

            directorToPlay.time = 0;     // remet au début
            directorToPlay.Evaluate();   // pose initiale propre

            // Passe par le TimelineManager pour garantir la suspension du CameraController
            // et la gestion centralisée des entrées joueur.
            if (TimelineManager.Instance != null)
                TimelineManager.Instance.PlayTimeline(directorToPlay);
            else
                directorToPlay.Play();


            // (Optionnel) Attendre la fin de la lecture pour séquencer strictement la suite
            while ((TimelineManager.Instance != null && TimelineManager.Instance.IsTimelinePlaying) ||
                   (TimelineManager.Instance == null && directorToPlay != null && directorToPlay.state == PlayState.Playing))
                yield return null;
        }

        EventsManager.Instance.eventInProgress = false;
    }
}
