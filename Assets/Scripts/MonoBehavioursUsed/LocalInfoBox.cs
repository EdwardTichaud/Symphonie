using UnityEngine;

/// <summary>
/// Affiche la LocalInfoBox en world space mais la positionne
/// systématiquement par rapport au joueur plutôt que sur la cible.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class LocalInfoBox : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private SceneBindings sceneBindings;

    [Header("Placement")]
    [Tooltip("Référence du joueur à suivre. Laisser vide pour recherche automatique via le tag Player.")]
    public Transform playerTransform;

    [Tooltip("Décalage appliqué au-dessus du joueur.")]
    public Vector3 playerOffset = new Vector3(0f, 2f, 0f);

    [Tooltip("Tente de retrouver automatiquement le joueur si la référence est perdue.")]
    public bool autoResolvePlayer = true;

    private Canvas canvas;
    private Camera worldCamera;
    private bool warnedMissingPlayer;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();

        // Force un rendu en World Space pour suivre un objet dans la scène.
        canvas.renderMode = RenderMode.WorldSpace;

        if (sceneBindings == null)
            sceneBindings = ServiceRegistry.GetOrFind<SceneBindings>(FindObjectsInactive.Include);

        // Récupération de la WorldCamera définie par son tag.
        if (worldCamera == null && sceneBindings != null)
            worldCamera = sceneBindings.WorldCamera;
        if (worldCamera == null && Camera.main != null)
            worldCamera = Camera.main;
        if (worldCamera != null)
        {
            canvas.worldCamera = worldCamera;
        }
        else
        {
            Debug.LogWarning("[LocalInfoBox] WorldCamera introuvable.");
        }

        if (playerTransform == null && autoResolvePlayer)
            TryResolvePlayer();
    }

    private void OnEnable()
    {
        if (playerTransform == null && autoResolvePlayer)
            TryResolvePlayer();
    }

    private void LateUpdate()
    {
        // Récupère l'objet interactif courant à suivre.
        var interactable = InteractionManager.Instance?.currentInteractable;
        if (interactable == null)
            return;

        if (playerTransform == null)
        {
            if (!autoResolvePlayer || !TryResolvePlayer())
                return;
        }

        // Positionne la boîte directement sur le joueur avec l'offset défini dans l'inspecteur.
        transform.position = playerTransform.position + playerOffset;

        // Oriente la boîte vers la WorldCamera pour rester lisible.
        if (worldCamera != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - worldCamera.transform.position);
        }
    }

    private bool TryResolvePlayer()
    {
        if (sceneBindings == null)
            sceneBindings = ServiceRegistry.GetOrFind<SceneBindings>(FindObjectsInactive.Include);

        if (sceneBindings != null && sceneBindings.Player != null)
        {
            playerTransform = sceneBindings.Player.transform;
            warnedMissingPlayer = false;
            return true;
        }

        if (!warnedMissingPlayer)
        {
            Debug.LogWarning("[LocalInfoBox] Aucun joueur trouvé pour positionner l'invite.");
            warnedMissingPlayer = true;
        }
        return false;
    }
}
