using UnityEngine;

public class MovementInputHandler : MonoBehaviour
{
    public Vector2 orientation;
    private Vector2 lastOrientation;

    public Vector3 moveInput;
    public bool isRunning;
    public bool isWalking;

    /// <summary>
    /// Indique si la course est autorisée. Permet à d'autres scripts (zones, cinématiques...) 
    /// de désactiver temporairement le sprint du joueur.
    /// </summary>
    [Tooltip("Si désactivé, l'input de course est ignoré et le joueur marche uniquement.")]
    public bool canRun = true;

    [Tooltip("Transform de la caméra utilisée pour orienter le déplacement.")]
    public Transform cameraTransform;

    void Awake()
    {
        // Assigne la caméra principale si aucune n'est définie.
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    void LateStart()
    {
        if (InputsManager.Instance == null)
        {
            Debug.LogError("InputsManager is not assigned in ScriptAccesManager.");
            return;
        }

        if (InputsManager.Instance.playerInputs == null)
        {
            Debug.LogError("PlayerInputs is not assigned in InputsManager.");
            return;
        }

        InputsManager.Instance.playerInputs.Enable();
    }

    void Update()
    {
        HandleMovementInput();
    }

    public void HandleMovementInput()
    {
        if (InputsManager.Instance == null || InputsManager.Instance.playerInputs == null)
            return;

        var inputs = InputsManager.Instance.playerInputs;
        Vector2 input2D = inputs.World.Move.ReadValue<Vector2>();

        // Conversion de l'entrée en vecteur 3D relatif à l'orientation de la caméra.
        Vector3 camForward = Vector3.forward;
        Vector3 camRight = Vector3.right;
        if (cameraTransform != null)
        {
            camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            camRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        }
        moveInput = (camForward * input2D.y + camRight * input2D.x).normalized;

        // Lecture de l'input de course uniquement si celle-ci est autorisée.
        isRunning = canRun && inputs.World.Run.IsPressed();

        // Si la course est désactivée, le personnage est considéré comme marchant.
        isWalking = !isRunning;
    }
}
