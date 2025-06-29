using UnityEngine;
using System.Collections;

public class TimeManager : MonoBehaviour
{
    public float normalTimeScale = 1.0f;
    public float slowMotionScale = 0.5f;
    public float fixedDeltaTimeNormal = 0.02f;
    public float transitionDuration = 0.5f; // Durée de la transition en secondes

    private void Awake()
    {
        // Sauvegarder la valeur par défaut de fixedDeltaTime
        fixedDeltaTimeNormal = Time.fixedDeltaTime;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            TogglePause();
        }
    }

    public void SetTimeScale(float newTimeScale)
    {
        Time.timeScale = newTimeScale;
        Time.fixedDeltaTime = fixedDeltaTimeNormal * Time.timeScale;
    }

    public void ResetTimeScale()
    {
        Time.timeScale = normalTimeScale;
        Time.fixedDeltaTime = fixedDeltaTimeNormal;
    }

    public void ToggleSlowMotion()
    {
        if (Time.timeScale == normalTimeScale)
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

        AudioManager.Instance.PlaySfx(10);
        AudioManager.Instance.PlaySfx(11);

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float newTimeScale = Mathf.Lerp(startTimeScale, targetTimeScale, elapsedTime / transitionDuration);
            SetTimeScale(newTimeScale);
            yield return null;
        }

        SetTimeScale(targetTimeScale); // Assurez-vous que la valeur finale est exacte
    }

    private IEnumerator SmoothTransitionToNormalTime(float targetTimeScale)
    {
        float elapsedTime = 0f;
        float startTimeScale = Time.timeScale;

        AudioManager.Instance.PlaySfx(12);

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float newTimeScale = Mathf.Lerp(startTimeScale, targetTimeScale, elapsedTime / transitionDuration);
            SetTimeScale(newTimeScale);
            yield return null;
        }

        SetTimeScale(targetTimeScale); // Assurez-vous que la valeur finale est exacte
    }

    public void PauseGame()
    {
        Time.timeScale = 0;
    }

    public void UnpauseGame()
    {
        ResetTimeScale();
    }

    public void TogglePause()
    {
        if (Time.timeScale == 0)
        {
            UnpauseGame();
        }
        else
        {
            PauseGame();
        }
    }
}
