using UnityEngine;

[ExecuteAlways]
public class Pulse : MonoBehaviour
{
    [Header("Activation")]
    public bool active = true;

    [Header("Taille (multiplicateurs de la scale de base)")]
    [Min(0f)] public float minScaleMul = 0.9f;
    [Min(0f)] public float maxScaleMul = 1.1f;

    [Header("Animation")]
    [Min(0f)] public float pulseSpeed = 2f;
    [Tooltip("Forcer l'utilisation du temps non-scalé (utile en pause).")]
    public bool forceUnscaledTime = false;

    [Header("Transparence selon la taille")]
    public bool adjustTransparency = true;
    [Range(0f, 1f)] public float minAlpha = 0.3f; // à minScale
    [Range(0f, 1f)] public float maxAlpha = 1f;   // à maxScale

    [Header("Confort éditeur")]
    public bool autoCaptureBaseScaleInEditor = true;

    private Vector3 baseScale;
    private bool useUnscaledTime;

    // Rendu générique
    private CanvasGroup canvasGroup;
    private SpriteRenderer spriteRenderer;
    private Renderer genericRenderer;
    private MaterialPropertyBlock mpb;
    private static readonly int ColorID = Shader.PropertyToID("_Color");
    private Color cachedColor = Color.white;
    private bool hasColorProp;

    void OnEnable()
    {
        CacheComponents();
        if (baseScale == Vector3.zero) baseScale = transform.localScale;
    }

    void Awake() => CacheComponents();

    void Start()
    {
        if (baseScale == Vector3.zero) baseScale = transform.localScale;
    }

    void CacheComponents()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        genericRenderer = GetComponent<Renderer>();

        useUnscaledTime = forceUnscaledTime || canvasGroup != null;

        if (spriteRenderer != null)
        {
            cachedColor = spriteRenderer.color;
            hasColorProp = true;
        }
        else if (genericRenderer != null)
        {
            mpb ??= new MaterialPropertyBlock();
            var mat = genericRenderer.sharedMaterial;
            hasColorProp = mat != null && mat.HasProperty(ColorID);
            if (hasColorProp) cachedColor = mat.GetColor(ColorID);
        }
    }

    void Update()
    {
        if (!active)
        {
            // Si désactivé, remettre la scale et l’alpha à la base
            transform.localScale = baseScale;
            if (adjustTransparency)
                ApplyAlpha(maxAlpha);
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying && autoCaptureBaseScaleInEditor && transform.hasChanged)
        {
            baseScale = transform.localScale;
            transform.hasChanged = false;
        }
#endif
        if (baseScale == Vector3.zero)
            baseScale = transform.localScale;

        float minMul = Mathf.Min(minScaleMul, maxScaleMul);
        float maxMul = Mathf.Max(minScaleMul, maxScaleMul);

        float t = (Mathf.Sin((useUnscaledTime ? Time.unscaledTime : Time.time) * pulseSpeed) + 1f) * 0.5f;

        float mul = Mathf.Lerp(minMul, maxMul, t);
        transform.localScale = baseScale * mul;

        if (adjustTransparency)
        {
            float a = Mathf.Lerp(minAlpha, maxAlpha, t);
            ApplyAlpha(a);
        }
    }

    void ApplyAlpha(float a)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = a;
            return;
        }

        if (spriteRenderer != null)
        {
            var c = spriteRenderer.color;
            c.a = a;
            spriteRenderer.color = c;
            return;
        }

        if (genericRenderer != null && hasColorProp)
        {
            genericRenderer.GetPropertyBlock(mpb);
            var c = cachedColor; c.a = a;
            mpb.SetColor(ColorID, c);
            genericRenderer.SetPropertyBlock(mpb);
        }
    }

    [ContextMenu("Capture current scale as base")]
    void CaptureBaseScale() => baseScale = transform.localScale;
}
