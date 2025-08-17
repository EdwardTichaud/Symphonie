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
            playerTransform = player.transform;
        else
            Debug.LogError("[InteractionManager3D] Lucian3D not found.");

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

    public void SetInputs()
    {
        var infoBox = InputsManager.Instance.playerInputs.InfoBox;
        infoBox.Confirm.performed += OnConfirm;        
    }

    public void ResetBattleInputs()
    {
        var infoBox = InputsManager.Instance.playerInputs.InfoBox;
        infoBox.Confirm.performed -= OnConfirm;
    }

    void OnConfirm(InputAction.CallbackContext ctx)
    {
        if (currentInteractable != null)
        {
            currentInteractable.GetComponent<IInteractable>().Interact();
        }
    }

    void Update()
    {
        HandleInteractableDetection();
    }

    private void HandleInteractableDetection()
    {
        if (DialogueManager.Instance.isOpen || EventsManager.Instance.eventInProgress)
        {
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

        if (hits.Length > 0)
        {
            // Recherche de l'objet interactif le plus proche du joueur.
            GameObject nearest = hits[0].gameObject;
            float minDistance = Vector3.Distance(playerTransform.position, nearest.transform.position);

            foreach (Collider hit in hits)
            {
                float dist = Vector3.Distance(playerTransform.position, hit.transform.position);
                if (dist < minDistance)
                {
                    // Mémorise l'objet le plus proche trouvé jusqu'à présent.
                    nearest = hit.gameObject;
                    minDistance = dist;
                }
            }

            // Met à jour l'interactable courant uniquement s'il change.
            if (currentInteractable != nearest)
            {
                currentInteractable = nearest;

                if (localInfoBox != null)
                {
                    // Affiche l'invite locale d'interaction.
                    localInfoBox.SetActive(true);

                    // Active également la map d'inputs InfoBox pour capter la touche de validation.
                    InputsManager.Instance.ActivateOnly(
                        InputsManager.Instance.playerInputs.World.Get(),
                        InputsManager.Instance.playerInputs.InfoBox.Get());
                }
            }
        }
        else if (currentInteractable != null)
        {
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