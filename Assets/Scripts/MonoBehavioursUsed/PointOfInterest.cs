using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Playables;

public class PointOfInterest : MonoBehaviour, IInteractable, ILocalInfoBoxTarget
{
    public enum InteractionPlayMode { Repeatable, Once }
    public enum StartMode { OnConfirm, OnPlayerEnter }

    [Header("Déclenchement")]
    [Tooltip("OnConfirm: attend l'appui 'Confirm' (appel de Interact()). OnPlayerEnter: démarre dès l'entrée du joueur dans le trigger.")]
    [SerializeField] private StartMode startMode = StartMode.OnConfirm;

    [Tooltip("Tag à détecter pour OnPlayerEnter.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Délai avant démarrage auto après détection (0 = immédiat).")]
    [Min(0f)]
    [SerializeField] private float autoStartDelay = 0f;

    [Header("Interaction")]
    [SerializeField] private InteractionPlayMode playMode = InteractionPlayMode.Repeatable;
    [SerializeField] private bool persistState = true;
    [SerializeField] private string uniqueId = "";
    [SerializeField] private bool autoDisableAfterConsumed = false;
    [SerializeField] private bool consumed = false;

    [Header("Fades")]
    public bool blackFade;
    public bool whiteFade;

    [Header("Orbit")]
    public bool orbitAround = false;

    [Header("Dialogue")]
    public DialogueContainer dialogue;
    public ConditionalDialogue[] alternateDialogues;
    private bool mainDialoguePlayed = false;

    [Header("Timeline (direct)")]
    public bool launchTimeline = false;
    public PlayableDirector directorToPlay;
    public bool useTimelineFade = true;

    [Header("Pick Up d'objet")]
    [SerializeField] private List<ItemData> itemsToPickUp = new();

    [Header("Désactivation d'objets")]
    [SerializeField] private List<GameObject> objectsToDisable = new();

    [Header("Particules")]
    [Tooltip("Particle Systems dont l'émission doit passer à 0 à la fin de l'interaction (ou immédiatement si déjà consommé).")]
    [SerializeField] private List<ParticleSystem> particleSystemsToZeroEmission = new();

    [Header("Succès")]
    [SerializeField] private AchievementSO achievementToUnlock;

    [Header("Local InfoBox")]
    public Vector3 localInfoBoxOffset;

    // --- Interfaces ---
    public GameObject GameObject => gameObject;
    public Vector3 LocalInfoBoxOffset => localInfoBoxOffset;
    public void IncrementDialogueStage() { }

    // Exposition pratique
    public bool CanInteract => (playMode == InteractionPlayMode.Repeatable) || (playMode == InteractionPlayMode.Once && !consumed);
    public bool IsConsumed => consumed;
    public InteractionPlayMode PlayMode => playMode;

    // Garde-fous locaux
    private bool _isRunning = false;
    private bool _playerInside = false;
    private Coroutine _autoStartRoutine;

    private void Awake()
    {
        if (persistState && playMode == InteractionPlayMode.Once)
        {
            string key = GetPrefsKey();
            consumed = PlayerPrefs.GetInt(key, 0) == 1;

            if (consumed)
            {
                if (autoDisableAfterConsumed)
                    enabled = false;

                // Maintenir les objets désactivés si déjà consommé
                if (objectsToDisable != null && objectsToDisable.Count > 0)
                {
                    foreach (var obj in objectsToDisable)
                    {
                        if (obj != null)
                            obj.SetActive(false);
                    }
                }

                // Couper l'émission des particules si consommé au chargement
                if (particleSystemsToZeroEmission != null && particleSystemsToZeroEmission.Count > 0)
                {
                    foreach (var ps in particleSystemsToZeroEmission)
                        SetEmissionToZero(ps);
                }
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(uniqueId))
            uniqueId = System.Guid.NewGuid().ToString("N");

        // Petit rappel utile pour OnPlayerEnter
        if (startMode == StartMode.OnPlayerEnter)
        {
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
                Debug.LogWarning($"[PointOfInterest] Le Collider de '{name}' devrait être 'isTrigger' pour OnPlayerEnter.");
        }
    }
#endif

    // === Mode OnConfirm ===
    public void Interact()
    {
        TryStartInteraction();
    }

    private void TryStartInteraction()
    {
        if (_isRunning) return;
        if (!CanInteract) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.isOpen) return;
        if (EventsManager.Instance != null && EventsManager.Instance.eventInProgress) return;

        StartCoroutine(RunInteraction());
    }

    // === Mode OnPlayerEnter (Trigger 3D) ===
    private void OnTriggerEnter(Collider other)
    {
        if (startMode != StartMode.OnPlayerEnter) return;
        if (!other || (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag))) return;
        if (!CanInteract) return;

        _playerInside = true;

        // Une seule routine d'auto-start à la fois
        if (_autoStartRoutine == null)
            _autoStartRoutine = StartCoroutine(AutoStartWhenPlayerEnters());
    }

    private void OnTriggerExit(Collider other)
    {
        if (startMode != StartMode.OnPlayerEnter) return;
        if (!other || (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag))) return;

        _playerInside = false;

        // Si on avait prévu un démarrage différé, on l'annule à la sortie
        if (_autoStartRoutine != null)
        {
            StopCoroutine(_autoStartRoutine);
            _autoStartRoutine = null;
        }
    }

    private IEnumerator AutoStartWhenPlayerEnters()
    {
        // Délai optionnel
        if (autoStartDelay > 0f)
            yield return new WaitForSeconds(autoStartDelay);

        _autoStartRoutine = null;

        // Toujours vérifier que le joueur est encore là et que l'on peut démarrer
        if (_playerInside)
            TryStartInteraction();
    }

    private IEnumerator RunInteraction()
    {
        _isRunning = true;
        if (EventsManager.Instance != null) EventsManager.Instance.eventInProgress = true;

        // 0) Fades
        var fader = FadeChildrenOpacity.Instance;
        bool fadeTriggered = false;

        if (fader != null)
        {
            if (blackFade) { fader.ChangeOpacity(0, 1f, 1f); fadeTriggered = true; }
            if (whiteFade) { fader.ChangeOpacity(1, 1f, 1f); fadeTriggered = true; }
            if (fadeTriggered) yield return new WaitForSeconds(3f);
            if (blackFade) fader.ChangeOpacity(0, 0f, 2f);
            if (whiteFade) fader.ChangeOpacity(1, 0f, 2f);
        }

        // 0.c) Orbit optionnel
        OrbitAround orbitAroundClass = null;
        if (orbitAround)
        {
            orbitAroundClass = GetComponent<OrbitAround>();
            if (orbitAroundClass != null) orbitAroundClass.enabled = true;
        }

        // 1) Dialogue
        DialogueContainer container = GetDialogue();
        if (container != null)
        {
            yield return DialogueManager.Instance.StartDialogue(container);
            if (container == dialogue) mainDialoguePlayed = true;
        }

        // 2) Timeline
        if (launchTimeline && directorToPlay != null)
        {
            if (directorToPlay.state == PlayState.Playing) directorToPlay.Stop();
            directorToPlay.time = 0; directorToPlay.Evaluate();

            if (TimelineManager.Instance != null)
                TimelineManager.Instance.PlayTimeline(directorToPlay, useTimelineFade);
            else
                directorToPlay.Play();

            while ((TimelineManager.Instance != null && TimelineManager.Instance.IsTimelinePlaying) ||
                   (TimelineManager.Instance == null && directorToPlay != null && directorToPlay.state == PlayState.Playing))
                yield return null;
        }

        // 2.b) Ramassage d'objet
        if (itemsToPickUp != null && itemsToPickUp.Count > 0)
        {
            foreach (var item in itemsToPickUp)
                if (item != null) GameManager.Instance?.AddItemToInventory(item);
        }

        // 2.c) Succès
        if (achievementToUnlock != null)
        {
            if (AchievementManager.Instance != null)
                AchievementManager.Instance.Unlock(achievementToUnlock);
            else
                Debug.LogWarning("[PointOfInterest] Aucun AchievementManager pour débloquer le succès.");
        }

        // 3) Fin d’orbite
        if (orbitAroundClass != null) orbitAroundClass.isActive = false;

        // 4) Marquer consommé si Once (sans tout couper tout de suite)
        bool shouldAutoDisableComponent = false;
        if (playMode == InteractionPlayMode.Once)
        {
            consumed = true;

            if (persistState)
            {
                PlayerPrefs.SetInt(GetPrefsKey(), 1);
                PlayerPrefs.Save();
            }

            shouldAutoDisableComponent = autoDisableAfterConsumed;
        }

        // 5) Libérer l'état d'événement AVANT désactivations
        if (EventsManager.Instance != null) EventsManager.Instance.eventInProgress = false;
        _isRunning = false;

        // 6) Particules → émission = 0 (fin de séquence)
        if (particleSystemsToZeroEmission != null && particleSystemsToZeroEmission.Count > 0)
        {
            foreach (var ps in particleSystemsToZeroEmission)
                SetEmissionToZero(ps);
        }

        // 7) Désactivation d'objets — À LA FIN
        if (objectsToDisable != null && objectsToDisable.Count > 0)
        {
            foreach (var obj in objectsToDisable)
            {
                if (obj == null || obj == this.gameObject) continue;
                obj.SetActive(false);
            }

            if (objectsToDisable.Contains(this.gameObject))
            {
                // Laisse un frame pour s'assurer que tout est terminé
                yield return null;
                this.gameObject.SetActive(false);
                yield break;
            }
        }

        // 8) Désactivation éventuelle du composant à la toute fin
        if (shouldAutoDisableComponent)
            enabled = false;
    }

    private void SetEmissionToZero(ParticleSystem ps)
    {
        if (ps == null) return;
        var em = ps.emission;
        em.enabled = true;
        em.rateOverTimeMultiplier = 0f;
        em.rateOverDistanceMultiplier = 0f;
        ps.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
        // ps.Clear(withChildren: true); // si tu veux vider immédiatement
    }

    private string GetPrefsKey()
    {
        string scene = gameObject.scene.IsValid() ? gameObject.scene.path : "unsaved_scene";
        return $"POI_{scene}_{uniqueId}";
    }

    private DialogueContainer GetDialogue()
    {
        if (!mainDialoguePlayed) return dialogue;

        if (alternateDialogues != null)
        {
            foreach (var alt in alternateDialogues)
                if (alt != null && alt.IsConditionMet(mainDialoguePlayed))
                    return alt.dialogue;
        }

        return dialogue;
    }

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

        // Réactive les objets désactivés
        if (objectsToDisable != null && objectsToDisable.Count > 0)
        {
            foreach (var obj in objectsToDisable)
                if (obj != null) obj.SetActive(true);
        }

        // (On ne relance pas automatiquement les particules ici)
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
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
