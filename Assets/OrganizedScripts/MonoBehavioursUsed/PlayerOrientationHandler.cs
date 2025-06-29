using UnityEngine;

[RequireComponent(typeof(MovementInputHandler))]
public class PlayerOrientationHandler : MonoBehaviour
{
    private MovementInputHandler movementInputHandler;
    public Vector2 orientation;
    private Vector2 lastOrientation;

    void Start()
    {
        movementInputHandler = GetComponent<MovementInputHandler>();
    }

    void Update()
    {
        HandleOrientation();
    }

    public void HandleOrientation()
    {
        if (movementInputHandler.moveInput.x != 0)
        {
            orientation.x = Mathf.Sign(movementInputHandler.moveInput.x);
            lastOrientation.x = orientation.x;
        }

        if (movementInputHandler.moveInput.y != 0)
        {
            orientation.y = Mathf.Clamp(movementInputHandler.moveInput.y, -1, 1);
            lastOrientation.y = orientation.y;
        }
    }

    public void LookAtTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f; // On ignore la hauteur pour une rotation horizontale seulement

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }
    }
}
