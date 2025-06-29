using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Affiche une jauge circulaire permettant de passer le tour.
/// </summary>
public class PassTurnUI : MonoBehaviour
{
    [Header("UI Elements")]
    public CanvasGroup canvasGroup;
    public Image progressImage;
    public TextMeshProUGUI label;

    [Header("Fade Settings")]
    public float fadeDuration = 1f;

    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// Affiche la jauge et réinitialise l'avancement, avec un fondu progressif.
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeIn());
        SetProgress(0f);
    }

    /// <summary>
    /// Masque la jauge.
    /// </summary>
    public void Hide()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Met à jour la progression entre 0 et 1.
    /// </summary>
    public void SetProgress(float t)
    {
        if (progressImage != null)
            progressImage.fillAmount = Mathf.Clamp01(t);
    }

    /// <summary>
    /// Fait apparaître progressivement la jauge.
    /// </summary>
    private IEnumerator FadeIn()
    {
        if (canvasGroup == null)
            yield break;

        canvasGroup.alpha = 0f;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            // Utilise unscaledDeltaTime pour ignorer le timeScale
            // et assurer l'affichage même si le jeu est en pause
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
    }
}
