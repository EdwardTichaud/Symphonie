using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Affiche une jauge circulaire permettant de passer le tour.
/// </summary>
public class PassTurnUI : MonoBehaviour
{
    public static PassTurnUI Instance { get; private set; }

    [Header("UI Elements")]
    public Image progressImage;
    public TextMeshProUGUI label;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        Hide();
    }

    /// <summary>
    /// Affiche la jauge et réinitialise l'avancement.
    /// </summary>
    public void Show()
    {
        transform.GetChild(0).gameObject.SetActive(true);
        SetProgress(0f);
    }

    /// <summary>
    /// Masque la jauge.
    /// </summary>
    public void Hide()
    {
        transform.GetChild(0).gameObject.SetActive(false);
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
