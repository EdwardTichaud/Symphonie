using UnityEngine;

/// <summary>
/// Gère l'affichage de la LocalInfoBox en World Space et la fait suivre
/// l'objet <see cref="InteractionManager.currentInteractable"/>.
/// L'UI reste en permanence orientée vers la <c>WorldCamera</c> et prend
/// en compte un décalage configurable pour chaque objet interactif.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class LocalInfoBox : MonoBehaviour
{
    [Tooltip("Décalage utilisé si l'objet interactif n'en fournit pas." )]
    public Vector3 defaultOffset; // Valeur de repli

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

        // Récupère un éventuel offset spécifique sur l'objet ciblé.
        Vector3 offset = defaultOffset;
        var target = interactable.GetComponent<ILocalInfoBoxTarget>();
        if (target != null)
        {
            offset = target.LocalInfoBoxOffset;
        }

        // Positionne la boîte en tenant compte de l'offset.
        transform.position = interactable.transform.position + offset;

        // Oriente la boîte vers la WorldCamera pour rester lisible.
        if (worldCamera != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - worldCamera.transform.position);
        }
    }
}
