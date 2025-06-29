using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Affiche une jauge circulaire permettant de passer le tour.
/// </summary>
public class PassTurnUI : MonoBehaviour
{
    [Header("UI Elements")]
    public CanvasGroup canvasGroup;
    public Image progressImage;
    public TextMeshProUGUI label;

    private void Awake()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// Affiche la jauge et réinitialise l'avancement.
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
        SetProgress(0f);
    }

    /// <summary>
    /// Masque la jauge.
    /// </summary>
    public void Hide()
    {
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
}
