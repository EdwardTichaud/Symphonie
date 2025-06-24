using UnityEngine;

public class MovementInputHandler : MonoBehaviour
{
    public Vector2 orientation;
    private Vector2 lastOrientation;

    public Vector3 moveInput;
    public bool isRunning;
    public bool isWalking;

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
        moveInput = new Vector3(input2D.x, 0f, input2D.y).normalized;

        isRunning = inputs.World.Run.IsPressed();
        isWalking = !isRunning;
    }
}
