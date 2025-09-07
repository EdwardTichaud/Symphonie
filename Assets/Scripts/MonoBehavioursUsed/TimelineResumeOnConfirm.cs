using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

 [DisallowMultipleComponent]
 [RequireComponent(typeof(PlayableDirector))]
 [ExecuteAlways]
 public class TimelinePauseResumeOnInteract : MonoBehaviour
{
    [Tooltip("Instance de PlayerInputs. Si vide, on prendra celle de InputsManager, sinon on en créera une locale.")]
    [SerializeField] private PlayerInputs playerInputs;

    [Header("Conditions à valider pour reprendre la Timeline")]
    [Tooltip("Liste ordonnée des conditions à remplir après chaque pause déclenchée par un Signal.")]
    [SerializeField] private List<ResumeCondition> resumeConditions = new();

    private PlayableDirector director;
    private bool ownsLocalInputs = false;
    private int currentConditionIndex = -1;
    private bool waitingForCondition = false;

    // Utilisé uniquement pour la condition DialogueClosed afin d'éviter une reprise immédiate si aucun dialogue n'était ouvert au moment de la pause.
    private bool waitingForDialogueToClose = false;

    [Serializable]
    private class ResumeCondition
    {
        public enum ConditionType
        {
            InteractInput,     // Attend un input "Interact"
            GameObjectInactive, // Attend que le GameObject soit désactivé
            DialogueClosed     // Attend la fermeture du DialogueManager (isOpen == false)
        }

        [Tooltip("Type de la condition à vérifier avant de reprendre la timeline.")]
        public ConditionType type = ConditionType.InteractInput;

        [Tooltip("GameObject à surveiller si le type est GameObjectInactive.")]
        public GameObject targetObject; // utilisé uniquement pour GameObjectInactive
    }

    private void Awake()
    {
        director = GetComponent<PlayableDirector>();

        if (playerInputs == null)
        {
            if (InputsManager.Instance != null && InputsManager.Instance.playerInputs != null)
            {
                playerInputs = InputsManager.Instance.playerInputs;
                ownsLocalInputs = false;
            }
            else
            {
                playerInputs = new PlayerInputs();
                ownsLocalInputs = true;
            }
        }
        else
        {
            ownsLocalInputs = true;
        }
    }

    private void OnEnable()
    {
        // En mode Éditeur, on n'active pas les entrées pour éviter les erreurs
        if (!Application.isPlaying)
            return;

        if (playerInputs == null)
        {
            Debug.LogWarning("[TimelinePauseResumeOnInteract] PlayerInputs introuvable.");
            return;
        }

        playerInputs.World.Enable();
        playerInputs.World.Interact.performed += OnInteract;
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        if (playerInputs != null)
        {
            playerInputs.World.Interact.performed -= OnInteract;

            if (ownsLocalInputs)
                playerInputs.World.Disable();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (!waitingForCondition || TimelineManager.Instance == null)
            return;

        if (currentConditionIndex < 0 || currentConditionIndex >= resumeConditions.Count)
            return;

        var cond = resumeConditions[currentConditionIndex];

        switch (cond.type)
        {
            case ResumeCondition.ConditionType.GameObjectInactive:
                if (cond.targetObject != null && !cond.targetObject.activeInHierarchy)
                    ResumeTimeline();
                break;

            case ResumeCondition.ConditionType.DialogueClosed:
                // On ne reprend que si on attend réellement que le dialogue se ferme
                if (!waitingForDialogueToClose) break;

                if (DialogueManager.Instance != null && !DialogueManager.Instance.isOpen)
                    ResumeTimeline();
                break;

                // InteractInput est géré par l'event OnInteract
        }
    }

    /// <summary>
    /// Appelée par un Signal (ou du code) pour mettre en pause et armer la prochaine condition.
    /// </summary>
    public void PauseForNextCondition()
    {
        if (!Application.isPlaying)
        {
            // Prévisualisation : pause simple du PlayableDirector sans gestion de conditions
            director?.Pause();
            return;
        }

        if (TimelineManager.Instance == null)
            return;

        currentConditionIndex++;

        if (currentConditionIndex >= resumeConditions.Count)
        {
            Debug.LogWarning("[TimelinePauseResumeOnInteract] Aucune condition restante, reprise immédiate.");
            ResumeTimeline();
            return;
        }

        var cond = resumeConditions[currentConditionIndex];

        // On arme la condition "DialogueClosed" intelligemment :
        // - si un dialogue est ouvert au moment de la pause -> on attend sa fermeture
        // - sinon -> on ne bloque pas (on reprendra via d'autres conditions de la liste, ou immédiatement s'il n'y en a pas)
        waitingForDialogueToClose = false;
        if (cond.type == ResumeCondition.ConditionType.DialogueClosed)
        {
            if (DialogueManager.Instance != null && DialogueManager.Instance.isOpen)
            {
                waitingForDialogueToClose = true;
            }
            else
            {
                // Aucun dialogue ouvert maintenant : pas la peine d'attendre
                // Si la seule condition est DialogueClosed, on peut reprendre de suite
                // Mais on suit la même logique que les autres : on met en pause puis Update reprendra si besoin.
            }
        }

        waitingForCondition = true;
        TimelineManager.Instance.PauseCurrentTimeline();

        // Si la condition en cours est DialogueClosed et qu'aucun dialogue n'est actuellement ouvert,
        // alors la condition est déjà satisfaite -> reprise immédiate pour ne pas bloquer inutilement.
        if (cond.type == ResumeCondition.ConditionType.DialogueClosed && !waitingForDialogueToClose)
        {
            ResumeTimeline();
        }
    }

    /// <summary>
    /// Input "Interact" -> ne reprend que si la condition attendue est InteractInput.
    /// </summary>
    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (!Application.isPlaying)
            return;

        if (!waitingForCondition || TimelineManager.Instance == null)
            return;

        if (currentConditionIndex < 0 || currentConditionIndex >= resumeConditions.Count)
            return;

        var cond = resumeConditions[currentConditionIndex];
        if (cond.type == ResumeCondition.ConditionType.InteractInput)
            ResumeTimeline();
    }

    private void ResumeTimeline()
    {
        if (!Application.isPlaying)
        {
            // En prévisualisation, on relance simplement la Timeline locale
            director?.Play();
            return;
        }

        waitingForCondition = false;
        waitingForDialogueToClose = false;
        TimelineManager.Instance.ResumeCurrentTimeline();
    }
}
