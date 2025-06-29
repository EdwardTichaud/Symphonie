using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class UIFadeInOut : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private bool fadeOnEnable = true;
    [SerializeField] private bool fadeOnDisable = true;

    private CanvasGroup canvasGroup;
    private Coroutine currentFadeRoutine;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (fadeOnEnable)
            canvasGroup.alpha = 0f;
    }

    private void OnEnable()
    {
        if (fadeOnEnable)
        {
            StartFade(1f);
        }
        else
        {
            canvasGroup.alpha = 1f;
        }
    }

    private void OnDisable()
    {
        // Si désactivé par code sans passer par Coroutine, on met alpha à 0 directement
        if (fadeOnDisable)
        {
            canvasGroup.alpha = 0f;
        }
    }

    public void FadeOutAndDisable()
    {
        if (fadeOnDisable)
        {
            StartFade(0f, () => gameObject.SetActive(false));
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void StartFade(float targetAlpha, System.Action onComplete = null)
    {
        if (currentFadeRoutine != null)
            StopCoroutine(currentFadeRoutine);
        currentFadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, onComplete));
    }

    private IEnumerator FadeRoutine(float targetAlpha, System.Action onComplete)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            float t = elapsed / fadeDuration;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        currentFadeRoutine = null;
        onComplete?.Invoke();
    }
}
