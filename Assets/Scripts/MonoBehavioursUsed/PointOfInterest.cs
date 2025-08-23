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

    [Header("Orbit")]
    [Tooltip("Cochez pour activer l'orbite autour de la cible avant le dialogue.")]
    public bool orbitAround = false;

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
    public Vector3 LocalInfoBoxOffset => localInfoBoxOffset;
    public void IncrementDialogueStage() { /* Aucun dialogue progressif pour l'instant */ }

    public void Interact()
    {
        if (DialogueManager.Instance.isOpen || EventsManager.Instance.eventInProgress)
            return; // Empêche les interactions multiples

        StartCoroutine(RunInteraction());
    }

    private IEnumerator RunInteraction()
    {
        EventsManager.Instance.eventInProgress = true;

        // 0) Fades optionnels
        var fader = FadeChildrenOpacity.Instance;
        if (fader != null)
        {
            if (blackFade) fader.ChangeOpacity(0, 1f, 1f); // enfant index 0 -> opaque en 1s
            if (whiteFade) fader.ChangeOpacity(1, 1f, 1f); // enfant index 1 -> opaque en 1s
        }

        yield return new WaitForSeconds(3f); // Attendre la fin du fade (ajuste selon tes durées)

        if (fader != null)
        {
            if (blackFade) fader.ChangeOpacity(0, 0f, 2f); // re-fade vers 0
            if (whiteFade) fader.ChangeOpacity(1, 0f, 2f);
        }

        // 0.b) Orbit optionnel : on récupère d'abord la référence PUIS on l'utilise
        OrbitAround orbitAroundClass = null;
        if (orbitAround)
        {
            orbitAroundClass = GetComponent<OrbitAround>();
            if (orbitAroundClass != null)
            {
                // Si ton OrbitAround expose un bool 'isActive', on l'active ici
                orbitAroundClass.enabled = true; // ou orbitAroundClass.isActive = true;
            }
        }

        // 1) Dialogue
        if (dialogue != null)
            yield return DialogueManager.Instance.StartDialogue(dialogue);

        // 2) Lancer directement le PlayableDirector, si demandé
        if (launchTimeline && directorToPlay != null)
        {
            if (directorToPlay.state == PlayState.Playing)
                directorToPlay.Stop();

            directorToPlay.time = 0;
            directorToPlay.Evaluate();

            if (TimelineManager.Instance != null)
                TimelineManager.Instance.PlayTimeline(directorToPlay);
            else
                directorToPlay.Play();

            // Option : attendre la fin du director
            while ((TimelineManager.Instance != null && TimelineManager.Instance.IsTimelinePlaying) ||
                   (TimelineManager.Instance == null && directorToPlay != null && directorToPlay.state == PlayState.Playing))
                yield return null;
        }

        // 3) Désactiver l’orbite si on l’avait activée
        if (orbitAroundClass != null)
        {
            orbitAroundClass.isActive = false;
        }

        EventsManager.Instance.eventInProgress = false;
    }
}
