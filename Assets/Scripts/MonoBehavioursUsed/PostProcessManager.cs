using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[ExecuteAlways]
/// <summary>
/// Gère les effets de post-traitement globaux.
/// Actuellement utilisé pour appliquer une distorsion lors des transitions.
/// </summary>
public class PostProcessManager : MonoBehaviour
{
    /// <summary>
    /// Instance unique du gestionnaire de post-traitement.
    /// </summary>
    public static PostProcessManager Instance { get; private set; }

    [Tooltip("Volume global contenant les effets de post-processing")]
    [SerializeField] private Volume volume;

    private LensDistortion lensDistortion;         // effet de distorsion utilisé lors des transitions
    private float baseLensIntensity;               // intensité de base de la distorsion

    // Réglages de couleur
    private ColorAdjustments colorAdjustments;     // composant pour gérer contraste/saturation/etc.
    private float baseContrast;                   // contraste de base pour revenir à l'état initial
    private float basePostExposure;               // exposition de base pour pouvoir revenir à un niveau neutre
    private Color baseColorFilter;                // couleur de base appliquée au rendu

    // Jeux de lumière
    private Bloom bloom;                          // effet de bloom pour accentuer les sources lumineuses
    private float baseBloomIntensity;             // intensité de bloom initiale

    // Ambiance sombre
    private Vignette vignette;                    // vignette pour assombrir les bords de l'écran
    private float baseVignetteIntensity;          // intensité de vignette initiale

    // Effets supplémentaires pour un style "clair-obscur"
    private ScreenSpaceAmbientOcclusion ambientOcclusion;    // renforce les ombres locales
    private float baseAmbientOcclusionIntensity;  // intensité de base pour l'occlusion ambiante
    private FilmGrain filmGrain;                  // grain cinématographique subtil
    private SplitToning splitToning;              // teintes séparées ombres/lumières

    // ---- Cycle jour/nuit ----
    [Header("Cycle jour/nuit")]

    [Tooltip("Durée en secondes d'un cycle complet (jour + nuit). Par défaut 24 min = journée accélérée x60.")]
    [SerializeField] private float cycleDuration = 24f * 60f; // 1440 secondes

    [Tooltip("Moment de la journée au lancement du jeu (0 = minuit, 0.5 = midi)." )]
    [Range(0f, 1f)]
    [SerializeField] private float startTime = 0.5f; // démarrage à midi pour une scène lumineuse

    [Tooltip("Courbe d'exposition au fil de la journée.")]
    [SerializeField] private AnimationCurve exposureCurve;   // modifie la luminosité générale

    [Tooltip("Dégradé de couleur appliqué selon l'heure.")]
    [SerializeField] private Gradient colorGradient;         // teinte chaude/froide selon la journée

    [Tooltip("Intensité du bloom pour simuler les étoiles la nuit.")]
    [SerializeField] private AnimationCurve bloomCurve;      // halo lumineux nocturne

    [Tooltip("Intensité de la vignette pour accentuer la nuit.")]
    [SerializeField] private AnimationCurve vignetteCurve;   // bordures plus sombres la nuit

    private float cycleTimer;                                // chronomètre interne du cycle

    private void Awake()
    {
        // Mise en place du singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Récupération des effets pour sauvegarder leurs valeurs de base
        if (volume != null && volume.profile != null)
        {
            // --- Lens Distortion ---
            volume.profile.TryGet(out lensDistortion);
            if (lensDistortion != null)
                baseLensIntensity = lensDistortion.intensity.value;

            // --- Color Adjustments ---
            volume.profile.TryGet(out colorAdjustments);
            if (colorAdjustments != null)
            {
                baseContrast = colorAdjustments.contrast.value;
                basePostExposure = colorAdjustments.postExposure.value;
                baseColorFilter = colorAdjustments.colorFilter.value;
            }

            // --- Bloom ---
            if (!volume.profile.TryGet(out bloom))
                bloom = volume.profile.Add<Bloom>(false); // ajoute l'effet s'il est absent
            if (bloom != null)
            {
                baseBloomIntensity = bloom.intensity.value;
                bloom.threshold.value = 0.8f;   // seuil pour ne pas brûler l'image
            }

            // --- Vignette ---
            if (!volume.profile.TryGet(out vignette))
                vignette = volume.profile.Add<Vignette>(false);
            if (vignette != null)
            {
                baseVignetteIntensity = vignette.intensity.value;
                vignette.smoothness.value = 0.8f;      // transition douce
                vignette.color.value = Color.black;    // couleur sombre
            }

            // --- Ambient Occlusion ---
            if (!volume.profile.TryGet(out ambientOcclusion))
                ambientOcclusion = volume.profile.Add<ScreenSpaceAmbientOcclusion>(false);
            if (ambientOcclusion != null)
                baseAmbientOcclusionIntensity = ambientOcclusion.intensity.value;

            // --- Film Grain ---
            if (!volume.profile.TryGet(out filmGrain))
                filmGrain = volume.profile.Add<FilmGrain>(false);

            // --- Split Toning ---
            if (!volume.profile.TryGet(out splitToning))
                splitToning = volume.profile.Add<SplitToning>(false);

            // Application du rendu clair-obscur par défaut
            ApplyClairObscurStyle();
        }
    }

    private void Start()
    {
        // Initialisation du timer en fonction de l'heure de départ souhaitée
        cycleTimer = startTime * cycleDuration;

        // Si aucune courbe n'est assignée dans l'inspecteur, on crée un jeu de courbes par défaut
        if (exposureCurve == null || exposureCurve.length == 0)
        {
            exposureCurve = new AnimationCurve(
                new Keyframe(0f, -2f),
                new Keyframe(0.25f, -0.2f),
                new Keyframe(0.5f, 0.3f),
                new Keyframe(0.75f, -0.2f),
                new Keyframe(1f, -2f)
            );
        }

        if (colorGradient == null || colorGradient.colorKeys == null || colorGradient.colorKeys.Length == 0)
        {
            colorGradient = new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(new Color(0.05f, 0.1f, 0.2f), 0f),   // nuit profonde
                    new GradientColorKey(new Color(0.8f, 0.7f, 0.6f), 0.25f), // aube
                    new GradientColorKey(Color.white, 0.5f),                 // midi éclatant
                    new GradientColorKey(new Color(0.8f, 0.5f, 0.4f), 0.75f),// crépuscule
                    new GradientColorKey(new Color(0.05f, 0.1f, 0.2f), 1f)    // retour à la nuit
                }
            };
        }

        if (bloomCurve == null || bloomCurve.length == 0)
        {
            bloomCurve = new AnimationCurve(
                new Keyframe(0f, 1.8f),
                new Keyframe(0.25f, 1f),
                new Keyframe(0.5f, 0.5f),
                new Keyframe(0.75f, 1f),
                new Keyframe(1f, 1.8f)
            );
        }

        if (vignetteCurve == null || vignetteCurve.length == 0)
        {
            vignetteCurve = new AnimationCurve(
                new Keyframe(0f, 0.45f),
                new Keyframe(0.25f, 0.3f),
                new Keyframe(0.5f, 0.2f),
                new Keyframe(0.75f, 0.3f),
                new Keyframe(1f, 0.45f)
            );
        }
    }

    private void Update()
    {
        // Avancement du cycle jour/nuit. La durée totale est de 24 minutes pour une journée complète.
        cycleTimer = (cycleTimer + Time.deltaTime) % cycleDuration;
        float t = cycleTimer / cycleDuration; // 0 à 1

        // Réglages de lumière et de couleur en fonction de l'heure
        if (colorAdjustments != null)
        {
            colorAdjustments.postExposure.value = basePostExposure + exposureCurve.Evaluate(t);
            colorAdjustments.colorFilter.value = colorGradient.Evaluate(t);
        }

        // Intensité du bloom : forte la nuit pour des étoiles brillantes
        if (bloom != null)
            bloom.intensity.value = bloomCurve.Evaluate(t);

        // Vignette plus marquée la nuit pour renforcer l'ambiance
        if (vignette != null)
            vignette.intensity.value = vignetteCurve.Evaluate(t);
    }

    /// <summary>
    /// Configure le volume pour un rendu "clair-obscur" inspiré d'Expedition 33.
    /// Des contrastes élevés et une exposition réduite mettent en valeur l'opposition
    /// entre lumière et obscurité, renforçant l'atmosphère dramatique de l'histoire.
    /// </summary>
    public void ApplyClairObscurStyle()
    {
        // Réglages de l'exposition et du contraste
        if (colorAdjustments != null)
        {
            colorAdjustments.postExposure.value = -0.5f;   // image globale assombrie
            colorAdjustments.contrast.value = 60f;          // contraste accentué
            colorAdjustments.saturation.value = -20f;       // légère désaturation

            // Mise à jour des valeurs de base pour le cycle jour/nuit
            basePostExposure = colorAdjustments.postExposure.value;
            baseContrast = colorAdjustments.contrast.value;
        }

        // Mise en avant des sources lumineuses
        if (bloom != null)
        {
            bloom.threshold.value = 1f;        // seuil bas pour capturer les zones lumineuses
            bloom.intensity.value = 1.2f;      // intensité modérée mais visible
            baseBloomIntensity = bloom.intensity.value;
        }

        // Assombrissement des bords de l'écran
        if (vignette != null)
        {
            vignette.intensity.value = 0.6f;   // vignette marquée
            baseVignetteIntensity = vignette.intensity.value;
        }

        // Renforcement des ombres locales
        if (ambientOcclusion != null)
        {
            ambientOcclusion.intensity.value = 2f; // ombres prononcées
            ambientOcclusion.radius.value = 0.3f;  // rayon réduit pour un effet concentré
            baseAmbientOcclusionIntensity = ambientOcclusion.intensity.value;
        }

        // Teinte chaude des lumières et froide des ombres
        if (splitToning != null)
        {
            splitToning.active = true;
            splitToning.shadowColor.value = new Color(0.2f, 0.2f, 0.25f);
            splitToning.highlightColor.value = new Color(1f, 0.95f, 0.8f);
            splitToning.balance.value = -20f; // ombres légèrement privilégiées
        }

        // Grain cinématographique léger pour le style
        if (filmGrain != null)
        {
            filmGrain.active = true;
            filmGrain.intensity.value = 0.1f;
        }
    }

    /// <summary>
    /// Fait varier l'intensité du LensDistortion jusqu'à <paramref name="peakIntensity"/>
    /// puis la ramène à son niveau de base sur la même durée.
    /// </summary>
    /// <param name="peakIntensity">Intensité maximale à atteindre.</param>
    /// <param name="duration">Durée de montée puis de descente en secondes.</param>
    public IEnumerator PulseLensDistortion(float peakIntensity, float duration)
    {
        // Si aucun effet n'est disponible, on quitte la coroutine
        if (lensDistortion == null && colorAdjustments == null)
            yield break;

        // Montée progressive jusqu'à l'intensité souhaitée
        float timer = 0f;
        while (timer < duration)
        {
            // Interpolation de l'intensité de la distorsion
            if (lensDistortion != null)
                lensDistortion.intensity.value = Mathf.Lerp(baseLensIntensity, peakIntensity, timer / duration);

            // Interpolation du contraste jusqu'à 100
            if (colorAdjustments != null)
                colorAdjustments.contrast.value = Mathf.Lerp(baseContrast, 100f, timer / duration);

            timer += Time.deltaTime;
            yield return null;
        }
        if (lensDistortion != null)
            lensDistortion.intensity.value = peakIntensity;
        if (colorAdjustments != null)
            colorAdjustments.contrast.value = 100f;

        // Retour à l'intensité d'origine
        timer = 0f;
        while (timer < duration)
        {
            if (lensDistortion != null)
                lensDistortion.intensity.value = Mathf.Lerp(peakIntensity, baseLensIntensity, timer / duration);
            if (colorAdjustments != null)
                colorAdjustments.contrast.value = Mathf.Lerp(100f, baseContrast, timer / duration);
            timer += Time.deltaTime;
            yield return null;
        }
        if (lensDistortion != null)
            lensDistortion.intensity.value = baseLensIntensity;
        if (colorAdjustments != null)
            colorAdjustments.contrast.value = baseContrast;
    }
}
