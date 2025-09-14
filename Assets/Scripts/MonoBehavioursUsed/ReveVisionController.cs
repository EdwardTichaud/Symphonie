using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gère la vision révélée à la manière d'un viseur spécial.
// Cette fonction s'intègre au récit de l'Histoire de Symphonie en permettant de dévoiler des secrets cachés.
/// Lors de l'appui sur l'action "ReveVisionToggle", seules les textures
/// prévues pour cette vision sont affichées.
/// </summary>
public class ReveVisionController : MonoBehaviour
{
    [Tooltip("Action d'entrée utilisée pour activer la vision révélée.")]
    public InputAction toggleAction; // Référence vers l'action écoutée.

    // Indique si nous avons créé l'action localement (pour éviter de désactiver l'action globale).
    private bool ownsLocalAction = false;

    // État actuel de la vision révélée.
    private bool reveActive = false;

    private void OnEnable()
    {
        // Essaie d'utiliser le système d'inputs centralisé.
        if (toggleAction == null && InputsManager.Instance != null)
        {
            // Utilise l'action "World.Action" définie dans l'asset d'inputs.
            toggleAction = InputsManager.Instance.playerInputs.World.Action;
            ownsLocalAction = false;
        }

        // Si aucune action n'a pu être récupérée (jeu lancé sans InputsManager), on crée un fallback local.
        if (toggleAction == null)
        {
            toggleAction = new InputAction("ReveVisionToggle", binding: "<Keyboard>/r");
            toggleAction.Enable();
            ownsLocalAction = true;
        }

        // Abonnement au déclenchement.
        toggleAction.performed += OnTogglePerformed;
    }

    private void OnDisable()
    {
        if (toggleAction != null)
        {
            toggleAction.performed -= OnTogglePerformed;

            // On ne désactive l'action que si elle a été créée localement.
            if (ownsLocalAction)
                toggleAction.Disable();
        }
    }

    // Callback interne appelé lors de l'appui sur l'action.
    private void OnTogglePerformed(InputAction.CallbackContext ctx)
    {
        ToggleVision();
    }

    /// <summary>
    /// Bascule l'état de la vision révélée et met à jour le shader global.
    /// Cette mécanique fait directement écho à l'Histoire de Symphonie en révélant les secrets cachés.
    /// </summary>
    private void ToggleVision()
    {
        reveActive = !reveActive;
        // Informe le shader de l'état courant via une propriété globale.
        Shader.SetGlobalFloat("_ReveVision", reveActive ? 1f : 0f);
    }
}
