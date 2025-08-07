using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

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

                // Réglages par défaut pour une ambiance sombre et poétique
                colorAdjustments.postExposure.value = -1f;                       // assombrir légèrement la scène
                colorAdjustments.colorFilter.value = new Color(0.9f, 0.9f, 1f);  // légère teinte bleutée
            }

            // --- Bloom ---
            if (!volume.profile.TryGet(out bloom))
                bloom = volume.profile.Add<Bloom>(false); // ajoute l'effet s'il est absent
            if (bloom != null)
            {
                baseBloomIntensity = bloom.intensity.value;
                bloom.intensity.value = 1.5f;   // intensité modérée pour un halo doux
                bloom.threshold.value = 0.8f;   // seuil pour ne pas brûler l'image
            }

            // --- Vignette ---
            if (!volume.profile.TryGet(out vignette))
                vignette = volume.profile.Add<Vignette>(false);
            if (vignette != null)
            {
                baseVignetteIntensity = vignette.intensity.value;
                vignette.intensity.value = 0.35f;      // assombrit les bords pour un rendu plus intime
                vignette.smoothness.value = 0.8f;      // transition douce
                vignette.color.value = Color.black;    // couleur sombre
            }
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
