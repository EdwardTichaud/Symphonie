using UnityEngine;

/// <summary>
/// Empêche le joueur de courir tant qu'il reste dans la zone délimitée par ce collider.
/// Il suffit d'attacher ce script à un GameObject possédant un <see cref="Collider"/> en mode
/// "Is Trigger". Lorsque le joueur entre, la course est désactivée; elle redevient
/// possible à la sortie.
/// </summary>
[RequireComponent(typeof(Collider))]
public class NoRunZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        // Récupère les composants de contrôle du joueur et désactive la course.
        var controller = other.GetComponentInParent<ThirdPersonPlayerController>();
        if (controller != null)
            controller.canRun = false;

        var inputHandler = other.GetComponentInParent<MovementInputHandler>();
        if (inputHandler != null)
            inputHandler.canRun = false;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        // Réactive la possibilité de courir une fois la zone quittée.
        var controller = other.GetComponentInParent<ThirdPersonPlayerController>();
        if (controller != null)
            controller.canRun = true;

        var inputHandler = other.GetComponentInParent<MovementInputHandler>();
        if (inputHandler != null)
            inputHandler.canRun = true;
    }
}
