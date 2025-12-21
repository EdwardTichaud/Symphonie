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
        // Actualise l'orientation horizontale selon le signe du déplacement X.
        if (movementInputHandler.moveInput.x != 0)
        {
            orientation.x = Mathf.Sign(movementInputHandler.moveInput.x);
            lastOrientation.x = orientation.x;
        }

        // ⚠️ Le déplacement avant/arrière est stocké sur l'axe Z.
        // Utiliser l'axe Y (vertical) empêchait la prise en compte du joystick gauche.
        if (movementInputHandler.moveInput.z != 0)
        {
            orientation.y = Mathf.Clamp(movementInputHandler.moveInput.z, -1, 1);
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
