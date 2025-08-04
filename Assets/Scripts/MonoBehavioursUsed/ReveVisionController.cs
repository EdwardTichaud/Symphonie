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
    [Tooltip("Action Input System utilisée pour activer la vision révélée.")]
    public string actionName = "ReveVisionToggle"; // Nom logique de l'action.

    [Tooltip("Action d'entrée réellement écoutée. Si null, une action par défaut sera créée.")]
    public InputAction toggleAction;

    // État actuel de la vision révélée.
    private bool reveActive = false;

    private void Awake()
    {
        // Crée une action par défaut si rien n'est assigné dans l'inspecteur.
        if (toggleAction == null)
        {
            // Par défaut on associe la touche R du clavier.
            toggleAction = new InputAction(actionName, binding: "<Keyboard>/r");
        }

        // Abonnement au déclenchement de l'action.
        toggleAction.performed += OnTogglePerformed;
        toggleAction.Enable();
    }

    private void OnDestroy()
    {
        // Nettoie proprement l'action pour éviter les fuites mémoire.
        if (toggleAction != null)
        {
            toggleAction.performed -= OnTogglePerformed;
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
    /// </summary>
    private void ToggleVision()
    {
        reveActive = !reveActive;
        // Informe le shader de l'état courant via une propriété globale.
        Shader.SetGlobalFloat("_ReveVision", reveActive ? 1f : 0f);
    }
}
