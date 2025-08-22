using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayableDirector))]
public class TimelinePauseResumeOnInteract : MonoBehaviour
{
    [Tooltip("Instance de PlayerInputs. Si vide, on prendra celle de InputsManager, sinon on en créera une locale.")]
    [SerializeField] private PlayerInputs playerInputs;

    [Header("Conditions à valider pour reprendre la Timeline")] 
    [Tooltip("Liste ordonnée des conditions qui devront être remplies pour que la timeline reprenne après chaque pause déclenchée par un Signal.")]
    [SerializeField] private List<ResumeCondition> resumeConditions = new();

    private PlayableDirector director; // Référence à la Timeline contrôlée
    private bool ownsLocalInputs = false; // vrai si on a créé notre propre instance locale
    private int currentConditionIndex = -1; // index de la condition en cours dans la liste
    private bool waitingForCondition = false; // indique si l'on attend une condition avant de reprendre

    /// <summary>
    /// Représente une condition pouvant être utilisée pour reprendre la timeline.
    /// On peut facilement étendre ce système en ajoutant de nouveaux types.
    /// </summary>
    [Serializable]
    private class ResumeCondition
    {
        public enum ConditionType
        {
            InteractInput,      // Attend un input "Interact" du joueur
            GameObjectInactive  // Attend qu'un GameObject donné soit désactivé
        }

        [Tooltip("Type de la condition à vérifier avant de reprendre la timeline.")]
        public ConditionType type = ConditionType.InteractInput;

        [Tooltip("GameObject à surveiller si le type est GameObjectInactive.")]
        public GameObject targetObject; // utilisé uniquement pour GameObjectInactive
    }

    private void Awake()
    {
        director = GetComponent<PlayableDirector>();

        // Si rien n'est référencé dans l'inspecteur, on tente d'utiliser l'instance globale
        if (playerInputs == null)
        {
            if (InputsManager.Instance != null && InputsManager.Instance.playerInputs != null)
            {
                playerInputs = InputsManager.Instance.playerInputs; // on réutilise celle du jeu
                ownsLocalInputs = false;
            }
            else
            {
                playerInputs = new PlayerInputs(); // on crée une instance locale
                ownsLocalInputs = true;
            }
        }
        else
        {
            // Une instance a été assignée manuellement dans l'inspecteur
            // On considère qu'elle est "locale" à ce composant.
            ownsLocalInputs = true;
        }
    }

    private void OnEnable()
    {
        if (playerInputs == null)
        {
            Debug.LogWarning("[TimelinePauseResumeOnInteract] PlayerInputs introuvable.");
            return;
        }

        // On s'assure que la map World est active pour recevoir l'input Interact
        playerInputs.World.Enable();

        // Abonnement à l'input Interact
        playerInputs.World.Interact.performed += OnInteract;
    }

    private void OnDisable()
    {
        if (playerInputs != null)
        {
            // Désabonnement propre
            playerInputs.World.Interact.performed -= OnInteract;

            // Si on possède une instance locale, on peut la désactiver proprement
            if (ownsLocalInputs)
            {
                playerInputs.World.Disable();
            }
            // Si l'instance vient d'InputsManager, on ne touche pas à son lifecycle
        }
    }

    private void Update()
    {
        if (!waitingForCondition || TimelineManager.Instance == null)
            return;

        // Récupère la condition en cours
        if (currentConditionIndex < 0 || currentConditionIndex >= resumeConditions.Count)
            return;

        var cond = resumeConditions[currentConditionIndex];

        // On vérifie les conditions qui doivent être testées en continu
        switch (cond.type)
        {
            case ResumeCondition.ConditionType.GameObjectInactive:
                if (cond.targetObject != null && !cond.targetObject.activeInHierarchy)
                {
                    ResumeTimeline();
                }
                break;
            // Les autres types sont gérés par évènement, donc rien à faire ici
        }
    }

    /// <summary>
    /// Méthode appelée par un Signal de la Timeline pour marquer une pause et définir la prochaine condition à attendre.
    /// </summary>
    public void PauseForNextCondition()
    {
        if (TimelineManager.Instance == null)
            return;

        // On prépare la prochaine condition
        currentConditionIndex++;

        if (currentConditionIndex >= resumeConditions.Count)
        {
            Debug.LogWarning("[TimelinePauseResumeOnInteract] Aucune condition restante, reprise immédiate.");
            TimelineManager.Instance.SetCurentTimelineSpeed(1);
            return;
        }

        waitingForCondition = true; // indique qu'on attend quelque chose
        TimelineManager.Instance.SetCurentTimelineSpeed(0); // met la timeline en pause (speed 0)
    }

    /// <summary>
    /// Callback exécuté lors de l'appui sur l'action "Interact".
    /// Ne reprend la timeline que si la condition attendue est de type InteractInput.
    /// </summary>
    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (!waitingForCondition || TimelineManager.Instance == null)
            return;

        if (currentConditionIndex < 0 || currentConditionIndex >= resumeConditions.Count)
            return;

        var cond = resumeConditions[currentConditionIndex];
        if (cond.type == ResumeCondition.ConditionType.InteractInput)
        {
            ResumeTimeline();
        }
    }

    /// <summary>
    /// Relance la Timeline en remettant sa vitesse à 1.
    /// </summary>
    private void ResumeTimeline()
    {
        waitingForCondition = false;
        TimelineManager.Instance.SetCurentTimelineSpeed(1);
    }
}
