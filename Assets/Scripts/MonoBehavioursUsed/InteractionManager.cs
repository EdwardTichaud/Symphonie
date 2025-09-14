using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables; // 👈 nécessaire pour PlayableDirector et PlayState

public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance { get; private set; }

    [Header("Interaction Settings")]
    [Tooltip("Maximum distance from player to interactable objects")]
    public float interactionRange = 3f;
    [Tooltip("Layer mask for interactable objects")]
    public LayerMask interactableLayer;

    // --- Gestion dynamique des couches interactables ---
    private LayerMask baseInteractableLayer;              // Masque de base : uniquement "World_Interactable".
    private int revealInteractableLayer;                  // Index de la couche "World_ReveLink_Interactable".
    private LayerMask revealCombinedLayer;                // Masque combiné lorsque Munin est lié.
    private ThirdPersonPlayerController playerController; // Référence pour connaître l'état du lien.

    [Header("References")]
    private Camera mainCamera;
    private Transform playerTransform;
    private GameObject localInfoBox;
    public GameObject currentInteractable;

    [Header("References")]
    [Tooltip("Director controlling cutscenes")]
    public PlayableDirector director; // 👈 nouveau champ

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        SetInputs();

        // Find the player
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            // Récupère le contrôleur pour connaître l'état du lien avec Munin
            playerController = playerTransform.GetComponent<ThirdPersonPlayerController>();
        }
        else
        {
            Debug.LogError("[InteractionManager3D] Lucian3D not found.");
        }

        // Initialise les masques de couches utilisables pour la détection
        baseInteractableLayer = interactableLayer; // Masque configuré dans l'inspecteur
        revealInteractableLayer = LayerMask.NameToLayer("World_ReveLink_Interactable");
        if (revealInteractableLayer != -1)
            revealCombinedLayer = baseInteractableLayer | (1 << revealInteractableLayer);
        else
            revealCombinedLayer = baseInteractableLayer;

        // Applique immédiatement le masque correspondant à l'état actuel du lien
        UpdateInteractableLayer();

        if (DialogueManager.Instance == null)
            Debug.LogError("[InteractionManager3D] DialogueManager not found.");
        if (InfoBoxManager.Instance == null)
            Debug.LogError("[InteractionManager3D] InfoBoxManager not found.");

        // Find and hide local info box UI
        localInfoBox = GameObject.Find("LocalInfoBoxCanvas");
        if (localInfoBox == null)
            Debug.LogError("[InteractionManager3D] LocalInfoBoxCanvas not found.");
        else
            localInfoBox.SetActive(false);
    }

    void OnDisable()
    {
        ResetBattleInputs();
    }

    /// <summary>
    /// Abonne l'action d'interaction du monde à la confirmation pour
    /// permettre au joueur de déclencher les <see cref="PointOfInterest"/>.
    /// </summary>
    public void SetInputs()
    {
        // L'action "Interact" fait partie du mapping d'entrée "World".
        // En l'utilisant directement, on garantit une cohérence des contrôles
        // sur l'ensemble des PointsOfInterest et dans tout le jeu.
        var world = InputsManager.Instance.playerInputs.World;
        world.Interact.performed += OnConfirm;
    }

    /// <summary>
    /// Retire l'abonnement à l'action d'interaction afin d'éviter
    /// les appels lorsque l'objet est désactivé.
    /// </summary>
    public void ResetBattleInputs()
    {
        var world = InputsManager.Instance.playerInputs.World;
        world.Interact.performed -= OnConfirm;
    }

    void OnConfirm(InputAction.CallbackContext ctx)
    {
        if (currentInteractable != null)
        {
            // Masquer immédiatement l'invite locale pour éviter qu'elle reste affichée
            if (localInfoBox != null)
            {
                localInfoBox.SetActive(false);
                InputsManager.Instance.ActivateOnly(InputsManager.Instance.playerInputs.World.Get());
            }
            currentInteractable.GetComponent<IInteractable>().Interact();
        }
    }

    void Update()
    {
        HandleInteractableDetection();
    }

    /// <summary>
    /// Met à jour le masque <see cref="interactableLayer"/> selon l'état du lien avec Munin.
    /// Lorsque Lucian et Munin sont liés, on ajoute les objets de la couche
    /// "World_ReveLink_Interactable" afin qu'ils deviennent interactables.
    /// </summary>
    private void UpdateInteractableLayer()
    {
        if (playerController != null && playerController.linkToMunin)
            interactableLayer = revealCombinedLayer;  // Inclut toutes les couches
        else
            interactableLayer = baseInteractableLayer; // Reste sur les couches visibles par défaut
    }

    private void HandleInteractableDetection()
    {
        // Assure que le masque est toujours cohérent avec l'état du lien
        UpdateInteractableLayer();

        if (DialogueManager.Instance.isOpen || EventsManager.Instance.eventInProgress)
        {
            // Si une interaction est en cours, on s'assure que la LocalInfoBox reste cachée
            if (currentInteractable != null)
            {
                currentInteractable = null;
            }

            if (localInfoBox != null && localInfoBox.activeSelf)
            {
                localInfoBox.SetActive(false);
                InputsManager.Instance.ActivateOnly(InputsManager.Instance.playerInputs.World.Get());
            }

            return;
        }

        // ❗️ Ne pas détecter pendant une cinématique Timeline
        if (TimelineManager.Instance != null && TimelineManager.Instance.IsTimelinePlaying)
        {
            // Si une Timeline joue, désactive la UI si elle était active
            if (currentInteractable != null)
            {
                currentInteractable = null;
                if (localInfoBox != null)
                {
                    localInfoBox.SetActive(false);
                    InputsManager.Instance.ActivateOnly(InputsManager.Instance.playerInputs.World.Get());
                }
            }
            return;
        }

        Collider[] hits = Physics.OverlapSphere(playerTransform.position, interactionRange, interactableLayer);

        // Recherche du point interactif valide le plus proche.
        GameObject nearest = null;
        float minDistance = float.MaxValue;
        foreach (Collider hit in hits)
        {
            GameObject obj = hit.gameObject;

            // Ignore les Points of Interest déjà consommés afin de ne plus afficher leur LocalInfoBox
            PointOfInterest poi = obj.GetComponent<PointOfInterest>();
            if (poi != null && !poi.CanInteract)
                continue;

            float dist = Vector3.Distance(playerTransform.position, obj.transform.position);
            if (dist < minDistance)
            {
                nearest = obj;
                minDistance = dist;
            }
        }

        if (nearest != null)
        {
            // Met à jour l'interactable courant uniquement s'il change.
            if (currentInteractable != nearest)
            {
                currentInteractable = nearest;

                if (localInfoBox != null)
                {
                    // Affiche l'invite locale d'interaction.
                    localInfoBox.SetActive(true);

                    // Seule la map "World" est nécessaire pour récupérer l'action Interact.
                    InputsManager.Instance.ActivateOnly(
                        InputsManager.Instance.playerInputs.World.Get());
                }
            }
        }
        else if (currentInteractable != null)
        {
            // Aucun objet valide détecté : on cache l'invite.
            currentInteractable = null;

            if (localInfoBox != null)
            {
                localInfoBox.SetActive(false);
                InputsManager.Instance.ActivateOnly(InputsManager.Instance.playerInputs.World.Get());
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (playerTransform == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(playerTransform.position, interactionRange);
    }
}