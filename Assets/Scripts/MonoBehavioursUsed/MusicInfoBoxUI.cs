using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Affiche une vignette indiquant le titre et le compositeur de la musique
/// actuellement lancée via un <see cref="AudioClipSO"/>.
/// L'élément est automatiquement créé au premier usage et persiste entre les scènes.
/// </summary>
public class MusicInfoBoxUI : MonoBehaviour
{
    private static MusicInfoBoxUI instance;
    public static MusicInfoBoxUI Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("MusicInfoBoxUI");
                instance = go.AddComponent<MusicInfoBoxUI>();
            }
            return instance;
        }
    }

    [Header("Layout")]
    [SerializeField] private Vector2 anchoredPosition = new Vector2(30f, 30f);
    [SerializeField] private Vector2 boxSize = new Vector2(360f, 96f);

    [Header("Animation")]
    [SerializeField, Range(0.1f, 3f)] private float fadeDuration = 0.35f;
    [SerializeField, Range(1f, 10f)] private float displayDuration = 5f;

    private CanvasGroup canvasGroup;
    private RectTransform panelRect;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI composerLabel;
    private Coroutine displayRoutine;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUIIfNeeded();
        HideImmediate();
    }

    /// <summary>
    /// Affiche la boîte avec les informations fournies par le ScriptableObject.
    /// </summary>
    public void Show(AudioClipSO clip)
    {
        if (clip == null)
            return;

        BuildUIIfNeeded();

        string title = string.IsNullOrWhiteSpace(clip.title)
            ? clip.Clip != null ? clip.Clip.name : clip.name
            : clip.title;

        string composer = string.IsNullOrWhiteSpace(clip.compositor)
            ? string.Empty
            : clip.compositor;

        titleLabel.text = title;
        composerLabel.text = composer;
        composerLabel.gameObject.SetActive(!string.IsNullOrEmpty(composer));

        if (displayRoutine != null)
            StopCoroutine(displayRoutine);

        displayRoutine = StartCoroutine(DisplaySequence());
    }

    private void BuildUIIfNeeded()
    {
        if (canvasGroup != null)
            return;

        var canvasGO = new GameObject("MusicInfoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var panelGO = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelGO.transform.SetParent(canvasGO.transform, false);

        panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.zero;
        panelRect.pivot = Vector2.zero;
        ApplyLayoutSettings();

        var bg = panelGO.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);

        canvasGroup = panelGO.GetComponent<CanvasGroup>();

        titleLabel = CreateText("Title", panelRect, 24f, FontStyles.Bold);
        titleLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleLabel.rectTransform.pivot = new Vector2(0f, 1f);
        titleLabel.rectTransform.offsetMin = new Vector2(16f, -48f);
        titleLabel.rectTransform.offsetMax = new Vector2(-16f, -16f);
        titleLabel.alignment = TextAlignmentOptions.Left;

        composerLabel = CreateText("Composer", panelRect, 18f, FontStyles.Italic);
        composerLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        composerLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
        composerLabel.rectTransform.pivot = new Vector2(0f, 0f);
        composerLabel.rectTransform.offsetMin = new Vector2(16f, 16f);
        composerLabel.rectTransform.offsetMax = new Vector2(-16f, 48f);
        composerLabel.alignment = TextAlignmentOptions.Left;
    }

    private void ApplyLayoutSettings()
    {
        if (panelRect == null)
            return;

        panelRect.anchoredPosition = anchoredPosition;
        panelRect.sizeDelta = boxSize;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, float size, FontStyles style)
    {
        var textGO = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(parent, false);
        var tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.enableAutoSizing = false;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.text = string.Empty;
        return tmp;
    }

    private IEnumerator DisplaySequence()
    {
        yield return FadeCanvas(1f);
        yield return new WaitForSecondsRealtime(displayDuration);
        yield return FadeCanvas(0f);
        displayRoutine = null;
    }

    private IEnumerator FadeCanvas(float targetAlpha)
    {
        float startAlpha = canvasGroup.alpha;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        canvasGroup.blocksRaycasts = targetAlpha > 0f;
        canvasGroup.interactable = targetAlpha > 0f;
    }

    private void HideImmediate()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            ApplyLayoutSettings();
        }
    }
#endif
}
