using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class TimeManager : MonoBehaviour
{
    public float normalTimeScale = 1.0f;
    public float slowMotionScale = 0.5f;
    public float fixedDeltaTimeNormal = 0.02f;
    public float transitionDuration = 0.5f;

    [Header("Inputs")]
    [SerializeField] private InputAction pauseAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/p");
    [SerializeField] private InputAction logTimeScaleAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/t");
    [SerializeField] private InputAction slowMotionAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/o");

    private void Awake()
    {
        fixedDeltaTimeNormal = Time.fixedDeltaTime;
    }

    private void OnEnable()
    {
        HookAction(pauseAction, OnPausePerformed);
        HookAction(logTimeScaleAction, OnLogTimeScalePerformed);
        HookAction(slowMotionAction, OnSlowMotionPerformed);
    }

    private void OnDisable()
    {
        UnhookAction(pauseAction, OnPausePerformed);
        UnhookAction(logTimeScaleAction, OnLogTimeScalePerformed);
        UnhookAction(slowMotionAction, OnSlowMotionPerformed);

        StopAllCoroutines();
        ResetTimeScale();
    }

    public void SetTimeScale(float newTimeScale)
    {
        Time.timeScale = newTimeScale;
        Time.fixedDeltaTime = fixedDeltaTimeNormal * Time.timeScale;
    }

    public void ResetTimeScale()
    {
        SetTimeScale(normalTimeScale);
    }

    public void ToggleSlowMotion()
    {
        if (Mathf.Approximately(Time.timeScale, normalTimeScale))
        {
            StartCoroutine(SmoothTransitionToSlowMotion(slowMotionScale));
        }
        else
        {
            StartCoroutine(SmoothTransitionToNormalTime(normalTimeScale));
        }
    }

    private IEnumerator SmoothTransitionToSlowMotion(float targetTimeScale)
    {
        float elapsedTime = 0f;
        float startTimeScale = Time.timeScale;

        AudioManager.Instance?.PlaySfx(10);
        AudioManager.Instance?.PlaySfx(11);

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float newTimeScale = Mathf.Lerp(startTimeScale, targetTimeScale, elapsedTime / transitionDuration);
            SetTimeScale(newTimeScale);
            yield return null;
        }

        SetTimeScale(targetTimeScale);
    }

    private IEnumerator SmoothTransitionToNormalTime(float targetTimeScale)
    {
        float elapsedTime = 0f;
        float startTimeScale = Time.timeScale;

        AudioManager.Instance?.PlaySfx(12);

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float newTimeScale = Mathf.Lerp(startTimeScale, targetTimeScale, elapsedTime / transitionDuration);
            SetTimeScale(newTimeScale);
            yield return null;
        }

        SetTimeScale(targetTimeScale);
    }

    public void PauseGame()
    {
        SetTimeScale(0f);
    }

    public void UnpauseGame()
    {
        ResetTimeScale();
    }

    public void TogglePause()
    {
        if (Mathf.Approximately(Time.timeScale, 0f))
        {
            UnpauseGame();
        }
        else
        {
            PauseGame();
        }
    }

    private void HookAction(InputAction action, System.Action<InputAction.CallbackContext> handler)
    {
        if (action == null)
            return;

        action.performed += handler;
        if (!action.enabled)
            action.Enable();
    }

    private void UnhookAction(InputAction action, System.Action<InputAction.CallbackContext> handler)
    {
        if (action == null)
            return;

        action.performed -= handler;
        if (action.enabled)
            action.Disable();
    }

    private void OnPausePerformed(InputAction.CallbackContext ctx) => TogglePause();

    private void OnSlowMotionPerformed(InputAction.CallbackContext ctx) => ToggleSlowMotion();

    private void OnLogTimeScalePerformed(InputAction.CallbackContext ctx)
    {
        Debug.Log($"Current TimeScale: {Time.timeScale:F2}");
    }
}
