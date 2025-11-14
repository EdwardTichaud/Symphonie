using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Affiche une vignette indiquant le titre et le compositeur de la musique
/// actuellement lancée via un <see cref="AudioClipSO"/>.
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
                instance = FindObjectOfType<MusicInfoBoxUI>(true);
                if (instance == null)
                {
                    var go = new GameObject("MusicInfoBoxUI");
                    instance = go.AddComponent<MusicInfoBoxUI>();
                }
            }
            return instance;
        }
    }

    [Header("References")]
    [SerializeField] private RectTransform infoContainer;
    [SerializeField] private CanvasGroup infoGroup;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI composerLabel;

    [Header("Animation")]
    [SerializeField, Range(0.1f, 3f)] private float fadeDuration = 0.35f;
    [SerializeField, Range(1f, 10f)] private float displayDuration = 5f;

    private Coroutine displayRoutine;
    private bool runtimeGenerated;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeUI();
        HideImmediate();
    }

    private void InitializeUI()
    {
        if (TryBindExistingHierarchy())
            return;

        BuildRuntimeUI();
        runtimeGenerated = true;
    }

    /// <summary>
    /// Affiche la boîte avec les informations fournies par le ScriptableObject.
    /// </summary>
    public void Show(AudioClipSO clip)
    {
        if (clip == null)
            return;

        EnsureUIReady();

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

    private void EnsureUIReady()
    {
        if (infoContainer != null && infoGroup != null && titleLabel != null && composerLabel != null)
            return;

        if (!TryBindExistingHierarchy())
        {
            if (!runtimeGenerated)
                BuildRuntimeUI();
        }
    }

    private bool TryBindExistingHierarchy()
    {
        RectTransform rootRect = infoContainer;

        if (rootRect == null)
        {
            var explicitChild = transform.Find("MusicInfoBox");
            if (explicitChild != null)
            {
                rootRect = explicitChild as RectTransform;
            }
            else
            {
                foreach (var child in GetComponentsInChildren<RectTransform>(true))
                {
                    if (child.gameObject == gameObject)
                        continue;

                    if (child.name.Contains("MusicInfoBox"))
                    {
                        rootRect = child;
                        break;
                    }
                }
            }
        }

        if (rootRect == null)
            return false;

        infoContainer = rootRect;
        infoGroup = infoGroup ?? rootRect.GetComponent<CanvasGroup>() ?? rootRect.gameObject.AddComponent<CanvasGroup>();
        titleLabel = titleLabel ?? rootRect.Find("MusicInfoBox_Title")?.GetComponent<TextMeshProUGUI>();
        composerLabel = composerLabel ?? rootRect.Find("MusicInfoBox_Compositor")?.GetComponent<TextMeshProUGUI>();

        if (titleLabel == null || composerLabel == null)
            return false;

        return true;
    }

    private void BuildRuntimeUI()
    {
        var canvasGO = new GameObject("MusicInfoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var panelGO = new GameObject("MusicInfoBox", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelGO.transform.SetParent(canvasGO.transform, false);

        infoContainer = panelGO.GetComponent<RectTransform>();
        infoContainer.anchorMin = Vector2.zero;
        infoContainer.anchorMax = Vector2.zero;
        infoContainer.pivot = Vector2.zero;
        infoContainer.anchoredPosition = new Vector2(30f, 30f);
        infoContainer.sizeDelta = new Vector2(360f, 96f);

        var bg = panelGO.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);

        infoGroup = panelGO.GetComponent<CanvasGroup>();

        titleLabel = CreateText("MusicInfoBox_Title", infoContainer, 24f, FontStyles.Bold);
        titleLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleLabel.rectTransform.pivot = new Vector2(0f, 1f);
        titleLabel.rectTransform.offsetMin = new Vector2(16f, -48f);
        titleLabel.rectTransform.offsetMax = new Vector2(-16f, -16f);
        titleLabel.alignment = TextAlignmentOptions.Left;

        composerLabel = CreateText("MusicInfoBox_Compositor", infoContainer, 18f, FontStyles.Italic);
        composerLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        composerLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
        composerLabel.rectTransform.pivot = new Vector2(0f, 0f);
        composerLabel.rectTransform.offsetMin = new Vector2(16f, 16f);
        composerLabel.rectTransform.offsetMax = new Vector2(-16f, 48f);
        composerLabel.alignment = TextAlignmentOptions.Left;
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
        float startAlpha = infoGroup.alpha;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            infoGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / fadeDuration);
            yield return null;
        }

        infoGroup.alpha = targetAlpha;
        infoGroup.blocksRaycasts = targetAlpha > 0f;
        infoGroup.interactable = targetAlpha > 0f;
    }

    private void HideImmediate()
    {
        if (infoGroup == null)
            return;

        infoGroup.alpha = 0f;
        infoGroup.blocksRaycasts = false;
        infoGroup.interactable = false;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
