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

    private Vector3 baseScale;

    // Rendu
    private SpriteRenderer spriteRenderer;
    private CanvasGroup canvasGroup;
    private Renderer genericRenderer;
    private MaterialPropertyBlock mpb;
    private Color baseColor = Color.white;
    private bool hasColorProperty = false;
    private static readonly int ColorID = Shader.PropertyToID("_Color");

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
            // CanvasGroup n’a pas de couleur, seulement alpha
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

        // t va de 0 (min) à 1 (max)
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
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
