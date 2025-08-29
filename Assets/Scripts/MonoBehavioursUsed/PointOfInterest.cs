using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.Playables;

public class PointOfInterest : MonoBehaviour, IInteractable, ILocalInfoBoxTarget
{
    public enum InteractionPlayMode { Repeatable, Once }

    [Header("Interaction")]
    [Tooltip("Repeatable: rejouable. Once: jouable une seule fois.")]
    [SerializeField] private InteractionPlayMode playMode = InteractionPlayMode.Repeatable;

    [Tooltip("Persister l'état (déjà joué) dans PlayerPrefs.")]
    [SerializeField] private bool persistState = true;

    [Tooltip("Identifiant unique pour la persistance. Laisse vide pour autogénérer en Éditeur.")]
    [SerializeField] private string uniqueId = "";

    [Tooltip("Désactiver automatiquement ce composant après une interaction 'Once' consommée.")]
    [SerializeField] private bool autoDisableAfterConsumed = false;

    // État runtime
    [SerializeField] private bool consumed = false;

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

    // --- Interfaces ---
    public GameObject GameObject => gameObject;
    public Vector3 LocalInfoBoxOffset => localInfoBoxOffset;
    public void IncrementDialogueStage() { /* Pas de progression pour l'instant */ }

    // Exposition pratique
    public bool CanInteract => (playMode == InteractionPlayMode.Repeatable) || (playMode == InteractionPlayMode.Once && !consumed);
    public bool IsConsumed => consumed;
    public InteractionPlayMode PlayMode => playMode;

    private void Awake()
    {
        // Charger l'état si nécessaire
        if (persistState && playMode == InteractionPlayMode.Once)
        {
            string key = GetPrefsKey();
            consumed = PlayerPrefs.GetInt(key, 0) == 1;

            if (consumed && autoDisableAfterConsumed)
                enabled = false;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Générer un GUID sérialisé (persistant dans la scène) si vide
        if (string.IsNullOrEmpty(uniqueId))
            uniqueId = System.Guid.NewGuid().ToString("N");
    }
#endif

    public void Interact()
    {
        if (!CanInteract) return; // déjà consommé en mode Once
        if (DialogueManager.Instance.isOpen || EventsManager.Instance.eventInProgress)
            return;

        StartCoroutine(RunInteraction());
    }

    private IEnumerator RunInteraction()
    {
        EventsManager.Instance.eventInProgress = true;

        // 0) Fades optionnels
        var fader = FadeChildrenOpacity.Instance;
        if (fader != null)
        {
            if (blackFade) fader.ChangeOpacity(0, 1f, 1f);
            if (whiteFade) fader.ChangeOpacity(1, 1f, 1f);
        }

        yield return new WaitForSeconds(3f);

        if (fader != null)
        {
            if (blackFade) fader.ChangeOpacity(0, 0f, 2f);
            if (whiteFade) fader.ChangeOpacity(1, 0f, 2f);
        }

        // 0.b) Orbit optionnel
        OrbitAround orbitAroundClass = null;
        if (orbitAround)
        {
            orbitAroundClass = GetComponent<OrbitAround>();
            if (orbitAroundClass != null)
                orbitAroundClass.enabled = true; // ou isActive = true;
        }

        // 1) Dialogue
        if (dialogue != null)
            yield return DialogueManager.Instance.StartDialogue(dialogue);

        // 2) Timeline
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

            while ((TimelineManager.Instance != null && TimelineManager.Instance.IsTimelinePlaying) ||
                   (TimelineManager.Instance == null && directorToPlay != null && directorToPlay.state == PlayState.Playing))
                yield return null;
        }

        // 3) Fin d’orbite
        if (orbitAroundClass != null)
            orbitAroundClass.isActive = false;

        // 4) Marquer consommé si Once
        if (playMode == InteractionPlayMode.Once)
        {
            consumed = true;

            if (persistState)
            {
                PlayerPrefs.SetInt(GetPrefsKey(), 1);
                PlayerPrefs.Save();
            }

            if (autoDisableAfterConsumed)
                enabled = false;
        }

        EventsManager.Instance.eventInProgress = false;
    }

    private string GetPrefsKey()
    {
        // Clé stable : jeu + scène + GUID sérialisé
        string scene = gameObject.scene.IsValid() ? gameObject.scene.path : "unsaved_scene";
        return $"POI_{scene}_{uniqueId}";
    }

    // Réinitialisation manuelle depuis l’éditeur (utile pour tests)
    [ContextMenu("Reset Consumed State")]
    public void ResetConsumedState()
    {
        consumed = false;
        if (persistState)
        {
            PlayerPrefs.DeleteKey(GetPrefsKey());
            PlayerPrefs.Save();
        }
        if (autoDisableAfterConsumed)
            enabled = true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Petit label pour indiquer le mode dans la Scene View
        var col = (playMode == InteractionPlayMode.Once)
            ? (consumed ? new Color(1f, 0.3f, 0.3f, 0.9f) : new Color(1f, 0.6f, 0.2f, 0.9f))
            : new Color(0.2f, 1f, 0.5f, 0.9f);

        Gizmos.color = col;
        Gizmos.DrawSphere(transform.position + Vector3.up * 0.2f, 0.06f);

        UnityEditor.Handles.color = col;
        string label = (playMode == InteractionPlayMode.Once)
            ? (consumed ? "Once (Consumed)" : "Once")
            : "Repeatable";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.3f, label);
    }
#endif
}
