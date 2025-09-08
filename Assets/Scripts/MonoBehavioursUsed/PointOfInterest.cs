using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic; // Nécessaire pour manipuler des listes d'objets ramassables
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
    [Tooltip("Dialogue par défaut lorsque le joueur interagit avec ce point d'intérêt.")]
    public DialogueContainer dialogue;

    [Tooltip("Dialogues alternatifs joués après le premier passage si un succès est débloqué.")]
    public ConditionalDialogue[] alternateDialogues;

    // Indique si le dialogue principal a déjà été joué au moins une fois
    private bool mainDialoguePlayed = false;

    [Header("Timeline (direct)")]
    [Tooltip("Cochez pour lancer un PlayableDirector à la fin du dialogue.")]
    public bool launchTimeline = false;
    [Tooltip("PlayableDirector à lancer directement (director.Play()).")]
    public PlayableDirector directorToPlay;
    [Tooltip("Activer pour entourer la timeline d'un fondu noir via le TimelineManager.")]
    public bool useTimelineFade = true;

    [Header("Pick Up d'objet")]
    [Tooltip("Liste d'objets (ScriptableObject) ajoutés à l'inventaire lors de l'interaction.")]
    [SerializeField] private List<ItemData> itemsToPickUp = new();

    [Header("Désactivation d'objets")]
    [Tooltip("GameObjects désactivés lorsque ce point d'intérêt est déclenché. Utile pour faire disparaître des éléments de la scène après l'interaction.")]
    [SerializeField] private List<GameObject> objectsToDisable = new();

    [Header("Succès")]
    [Tooltip("Succès à débloquer lors du déclenchement de ce point d'intérêt.")]
    [SerializeField] private AchievementSO achievementToUnlock; // Référence vers le succès optionnel à déverrouiller

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

            if (consumed)
            {
                if (autoDisableAfterConsumed)
                    enabled = false;

                // Si déjà consommé, on s'assure que les objets à désactiver restent inactifs.
                if (objectsToDisable != null && objectsToDisable.Count > 0)
                {
                    foreach (var obj in objectsToDisable)
                    {
                        if (obj != null)
                            obj.SetActive(false);
                    }
                }
            }
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

        // 0.b) Désactivation optionnelle d'objets externes
        if (objectsToDisable != null && objectsToDisable.Count > 0)
        {
            foreach (var obj in objectsToDisable)
            {
                if (obj != null)
                    obj.SetActive(false); // Désactive l'objet pour refléter l'évolution de la scène.
            }
        }

        // 0.c) Orbit optionnel
        OrbitAround orbitAroundClass = null;
        if (orbitAround)
        {
            orbitAroundClass = GetComponent<OrbitAround>();
            if (orbitAroundClass != null)
                orbitAroundClass.enabled = true; // ou isActive = true;
        }

        // 1) Dialogue
        DialogueContainer container = GetDialogue();
        if (container != null)
        {
            yield return DialogueManager.Instance.StartDialogue(container);

            // Marque le dialogue principal comme joué après sa première exécution
            if (container == dialogue)
                mainDialoguePlayed = true;
        }

        // 2) Timeline
        if (launchTimeline && directorToPlay != null)
        {
            if (directorToPlay.state == PlayState.Playing)
                directorToPlay.Stop();

            directorToPlay.time = 0;
            directorToPlay.Evaluate();

            if (TimelineManager.Instance != null)
                // Lance la timeline via le gestionnaire global en précisant si un fondu est souhaité.
                TimelineManager.Instance.PlayTimeline(directorToPlay, useTimelineFade);
            else
                // Sans TimelineManager, lecture directe (aucun fondu global disponible).
                directorToPlay.Play();

            while ((TimelineManager.Instance != null && TimelineManager.Instance.IsTimelinePlaying) ||
                   (TimelineManager.Instance == null && directorToPlay != null && directorToPlay.state == PlayState.Playing))
                yield return null;
        }

        // 2.b) Ramassage d'objet
        if (itemsToPickUp != null && itemsToPickUp.Count > 0)
        {
            // Ajoute chaque item à l'inventaire du joueur
            foreach (var item in itemsToPickUp)
            {
                if (item != null)
                    GameManager.Instance?.AddItemToInventory(item);
            }
        }

        // 2.c) Déblocage éventuel d'un succès
        if (achievementToUnlock != null)
        {
            // Vérifie la présence d'un AchievementManager avant de tenter le déblocage
            if (AchievementManager.Instance != null)
                AchievementManager.Instance.Unlock(achievementToUnlock);
            else
                Debug.LogWarning("[PointOfInterest] Aucun AchievementManager dans la scène pour débloquer le succès.");
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

    /// <summary>
    /// Sélectionne le dialogue approprié en fonction des conditions définies.
    /// </summary>
    private DialogueContainer GetDialogue()
    {
        // Si le dialogue principal n'a jamais été joué, on le retourne directement
        if (!mainDialoguePlayed)
            return dialogue;

        // Ensuite, on vérifie les dialogues alternatifs liés à des succès
        if (alternateDialogues != null)
        {
            foreach (var alt in alternateDialogues)
            {
                if (alt != null && alt.IsConditionMet(mainDialoguePlayed))
                    return alt.dialogue;
            }
        }

        // Aucun dialogue alternatif valide : on rejoue le dialogue principal
        return dialogue;
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

        // Réactive les objets potentiellement désactivés lors de l'interaction
        if (objectsToDisable != null && objectsToDisable.Count > 0)
        {
            foreach (var obj in objectsToDisable)
            {
                if (obj != null)
                    obj.SetActive(true);
            }
        }
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
