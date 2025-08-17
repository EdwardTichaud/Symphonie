using UnityEngine;

/// <summary>
/// Gère l'affichage de la LocalInfoBox en World Space et la fait suivre
/// l'objet <see cref="InteractionManager.currentInteractable"/> avec un
/// décalage vertical. L'UI reste en permanence orientée vers la
/// <c>WorldCamera</c>.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class LocalInfoBox : MonoBehaviour
{
    [Tooltip("Décalage vertical appliqué par rapport à l'objet interactif.")]
    public Vector3 offset;

    private Canvas canvas;
    private Camera worldCamera;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();

        // Force un rendu en World Space pour suivre un objet dans la scène.
        canvas.renderMode = RenderMode.WorldSpace;

        // Récupération de la WorldCamera définie par son tag.
        worldCamera = GameObject.FindGameObjectWithTag("WorldCamera")?.GetComponent<Camera>();
        if (worldCamera != null)
        {
            canvas.worldCamera = worldCamera;
        }
        else
        {
            Debug.LogWarning("[LocalInfoBox] WorldCamera introuvable.");
        }
    }

    private void LateUpdate()
    {
        // Récupère l'objet interactif courant à suivre.
        var interactable = InteractionManager.Instance?.currentInteractable;
        if (interactable == null)
            return;

        // Positionne la boîte juste au-dessus de l'objet.
        transform.position = interactable.transform.position + (Vector3)offset;

        // Oriente la boîte vers la WorldCamera pour rester lisible.
        if (worldCamera != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - worldCamera.transform.position);
        }
    }
}
