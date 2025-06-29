using UnityEngine;

public class MovePoint : MonoBehaviour
{
    [Header("Gizmo Settings")]
    public Color gizmoColor = Color.yellow;
    public float diameter = 1f;

    [Header("Movement Settings")]
    [Tooltip("Distance minimale autour de Lucian.")]
    public float minDistance = 1f;

    [Tooltip("Distance maximale autour de Lucian.")]
    public float maxDistance = 1f;

    public float moveSpeed = 5f;

    [Header("References")]
    public Transform lucianTransform; // Drag & drop Lucian ici dans l'Inspector

    private Vector3 currentOffset = Vector3.forward; // Offset par défaut pour éviter 0
    private Vector3 inputDirection = Vector3.zero;

    private void OnDrawGizmos()
    {
        if (lucianTransform == null) return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, diameter * 0.5f);

        // Limite Max
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(lucianTransform.position, maxDistance);

        // Limite Min
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(lucianTransform.position, minDistance);
    }

    void Start()
    {
        if (lucianTransform != null)
            transform.position = lucianTransform.position + currentOffset.normalized * minDistance;
    }

    void Update()
    {
        if (lucianTransform == null) return;

        HandleMovePointDirection();
    }

    void HandleMovePointDirection()
    {
        if (InputsManager.Instance == null) return;

        Vector2 moveInput = InputsManager.Instance.playerInputs.World.Move.ReadValue<Vector2>();

        if (moveInput != Vector2.zero)
        {
            // Nouvelle direction basée sur input
            Vector3 localInput = new Vector3(moveInput.x, 0, moveInput.y).normalized;
            inputDirection = lucianTransform.TransformDirection(localInput);
        }

        // Si jamais aucun input n'a jamais été donné, garder une direction par défaut (avant)
        if (inputDirection == Vector3.zero)
            inputDirection = lucianTransform.forward;

        // Déplacer l'offset avec vitesse
        currentOffset += inputDirection * moveSpeed * Time.deltaTime;

        // Clamp la distance entre min et max
        float currentDistance = currentOffset.magnitude;
        float clampedDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
        currentOffset = currentOffset.normalized * clampedDistance;

        // Applique la position
        transform.position = lucianTransform.position + currentOffset;

        // Oriente vers la direction
        transform.forward = inputDirection;
    }
}
