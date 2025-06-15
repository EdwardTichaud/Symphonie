using UnityEngine;
using UnityEngine.InputSystem;

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

        mainCamera = Camera.main;
        if (mainCamera == null)
            Debug.LogError("[InteractionManager3D] Main Camera not found.");

        // Find the player
        var player = FindFirstObjectByType<Lucian3D>();
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

        Collider[] hits = Physics.OverlapSphere(playerTransform.position, interactionRange, interactableLayer);
            if (hits.Length > 0)
            {
                // Determine l'IInteractable le plus proche
                GameObject nearestObj = hits[0].gameObject;
                float minDist = Vector3.Distance(playerTransform.position, nearestObj.transform.position);
                foreach (var col in hits)
                {
                    float d = Vector3.Distance(playerTransform.position, col.transform.position);
                    if (d < minDist)
                    {
                        minDist = d;
                        nearestObj = col.gameObject;
                    }
                }
                // Update highlight
                if (nearestObj != currentInteractable)
                {
                    currentInteractable = nearestObj;
                    if (localInfoBox != null)
                    {
                        localInfoBox.SetActive(true);
                        InputsManager.Instance.ActivateOnly(InputsManager.Instance.playerInputs.InfoBox.Get(), InputsManager.Instance.playerInputs.Player.Get());
                    }
                    RectTransform rt = localInfoBox.GetComponent<RectTransform>();
                        Vector3 worldPos = currentInteractable.transform.position + Vector3.up * 2f;
                        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
                        rt.position = screenPos;
                        // TODO: update icon/text on localInfoBox as needed
                }
            }
            else if (currentInteractable != null) // Si aucun IInteractable détecté et qu'il y en avait un
            {
                currentInteractable = null;

                if (localInfoBox != null)
                {
                    localInfoBox.SetActive(false);
                    InputsManager.Instance.ActivateOnly(InputsManager.Instance.playerInputs.Player.Get());
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