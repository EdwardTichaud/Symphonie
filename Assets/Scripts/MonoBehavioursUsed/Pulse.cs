using UnityEngine;

public class Pulse : MonoBehaviour
{
    [Header("Pulse (relatif)")]
    public float pulseSpeed = 2f;
    [Range(0f, 1f)] public float pulseAmount = 0.1f; // 0.1 = ±10%

    [Header("Transparence selon la taille")]
    public bool adjustTransparency = true;
    [Range(0f, 1f)] public float minAlpha = 0.3f; // alpha au min scale
    [Range(0f, 1f)] public float maxAlpha = 1f;   // alpha au max scale

    [Header("Options")]
    public bool autoCaptureBaseScaleInEditor = true; // recalcule la base si tu modifies la scale à la main
    [Tooltip("Force l'utilisation du temps non-scalé même en dehors de l'UI.")]
    [SerializeField] private bool forceUnscaledTime = false;

    private Vector3 baseScale;

    // Rendu
    private SpriteRenderer spriteRenderer;
    private CanvasGroup canvasGroup;
    private Renderer genericRenderer;
    private MaterialPropertyBlock mpb;
    private Color baseColor = Color.white;
    private bool hasColorProperty = false;
    private static readonly int ColorID = Shader.PropertyToID("_Color");
    /// <summary>
    /// Indique si le script doit se baser sur Time.unscaledTime. Cette valeur est
    /// forcée à true pour les éléments d'UI afin qu'ils continuent d'animer même
    /// lorsque Time.timeScale tombe à 0 (cas de l'écran de victoire par exemple).
    /// </summary>
    private bool useUnscaledTime = false;

    void Awake()
    {
        CacheRenderers();
    }

    void OnEnable()
    {
        if (baseScale == Vector3.zero) baseScale = transform.localScale;
    }

    void Start()
    {
        if (baseScale == Vector3.zero) baseScale = transform.localScale;
    }

    void CacheRenderers()
    {
        // Références de rendu
        spriteRenderer = GetComponent<SpriteRenderer>();
        canvasGroup = GetComponent<CanvasGroup>();
        genericRenderer = GetComponent<Renderer>();

        // Le choix du temps utilisé est initialisé à partir du paramètre exposé
        // dans l'inspecteur. Cela permet de forcer un comportement spécifique
        // pour des effets hors UI si nécessaire.
        useUnscaledTime = forceUnscaledTime;

        if (genericRenderer != null)
        {
            // Prépare MPB et détecte la propriété _Color sans toucher au matériau partagé
            mpb = new MaterialPropertyBlock();
            hasColorProperty = genericRenderer.sharedMaterial != null && genericRenderer.sharedMaterial.HasProperty(ColorID);

            if (hasColorProperty)
            {
                // Tente de récupérer la couleur de base depuis le matériel
                baseColor = genericRenderer.sharedMaterial.GetColor(ColorID);
            }
        }

        if (spriteRenderer != null)
        {
            baseColor = spriteRenderer.color;
            hasColorProperty = true;
        }
        else if (canvasGroup != null)
        {
            // Les CanvasGroup appartiennent à l'UI : on bascule automatiquement sur
            // Time.unscaledTime pour que l'animation reste active même durant une pause.
            useUnscaledTime = true;
            hasColorProperty = true;
        }
    }

    void Update()
    {
        // Option: recapturer automatiquement la scale de base si elle change dans l’éditeur
#if UNITY_EDITOR
        if (!Application.isPlaying && autoCaptureBaseScaleInEditor)
        {
            if (transform.hasChanged)
            {
                baseScale = transform.localScale;
                transform.hasChanged = false;
            }
        }
#endif

        if (baseScale == Vector3.zero)
            baseScale = transform.localScale;

        // Sélection du temps de référence : non-scalé pour l'UI/pause, sinon temps classique.
        float timeSource = useUnscaledTime ? Time.unscaledTime : Time.time;
        // t va de 0 (min) à 1 (max)
        float t = (Mathf.Sin(timeSource * pulseSpeed) + 1f) * 0.5f;
        float scaleFactor = Mathf.Lerp(1f - pulseAmount, 1f + pulseAmount, t);
        transform.localScale = baseScale * scaleFactor;

        if (adjustTransparency)
        {
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);
            ApplyAlpha(alpha);
        }
    }

    void ApplyAlpha(float alpha)
    {
        // Priorité: CanvasGroup (UI), puis SpriteRenderer, puis Renderer générique via MPB
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
            return;
        }

        if (spriteRenderer != null)
        {
            var c = spriteRenderer.color;
            c.a = alpha;
            spriteRenderer.color = c;
            return;
        }

        if (genericRenderer != null && hasColorProperty)
        {
            genericRenderer.GetPropertyBlock(mpb);
            var c = baseColor;
            c.a = alpha;
            mpb.SetColor(ColorID, c);
            genericRenderer.SetPropertyBlock(mpb);
        }
    }
}
