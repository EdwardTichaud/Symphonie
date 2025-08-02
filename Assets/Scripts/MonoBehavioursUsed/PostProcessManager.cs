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

    private LensDistortion lensDistortion;
    private float baseLensIntensity;

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

        // Récupération du LensDistortion pour sauvegarder son intensité de base
        if (volume != null && volume.profile != null)
        {
            volume.profile.TryGet(out lensDistortion);
            if (lensDistortion != null)
                baseLensIntensity = lensDistortion.intensity.value;
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
        if (lensDistortion == null)
            yield break;

        // Montée progressive jusqu'à l'intensité souhaitée
        float timer = 0f;
        while (timer < duration)
        {
            lensDistortion.intensity.value = Mathf.Lerp(baseLensIntensity, peakIntensity, timer / duration);
            timer += Time.deltaTime;
            yield return null;
        }
        lensDistortion.intensity.value = peakIntensity;

        // Retour à l'intensité d'origine
        timer = 0f;
        while (timer < duration)
        {
            lensDistortion.intensity.value = Mathf.Lerp(peakIntensity, baseLensIntensity, timer / duration);
            timer += Time.deltaTime;
            yield return null;
        }
        lensDistortion.intensity.value = baseLensIntensity;
    }
}
