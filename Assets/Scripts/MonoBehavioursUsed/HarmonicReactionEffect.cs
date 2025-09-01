using UnityEngine;

/// <summary>
/// Affiche un effet électrique sur le personnage lorsque l'harmonique de la zone
/// correspond à l'harmonique intrinsèque du personnage. Cet effet visuel permet au
/// joueur de repérer immédiatement les alliés en résonance avec la musique,
/// à la manière de l'électricité parcourant Thor dans le MCU.
/// </summary>
[RequireComponent(typeof(CharacterUnit))]
public class HarmonicReactionEffect : MonoBehaviour
{
    [Tooltip("Prefab de l'électricité parcourant le corps en cas de résonance.")]
    public GameObject electricityPrefab;

    private GameObject electricityInstance;
    private CharacterUnit unit;

    void Awake()
    {
        unit = GetComponent<CharacterUnit>();
    }

    void OnEnable()
    {
        // Abonnement à l'évènement de changement d'harmonique prédominante
        if (ZoneManager.Instance != null)
            ZoneManager.Instance.OnPredominantHarmonicChanged += OnHarmonicChanged;
        // Application initiale selon l'harmonique courante
        if (ZoneManager.Instance != null)
            OnHarmonicChanged(ZoneManager.Instance.currentPredominantHarmonic);
    }

    void OnDisable()
    {
        if (ZoneManager.Instance != null)
            ZoneManager.Instance.OnPredominantHarmonicChanged -= OnHarmonicChanged;
        DisableEffect();
    }

    /// <summary>
    /// Active ou désactive l'effet selon la concordance des harmoniques.
    /// </summary>
    private void OnHarmonicChanged(HarmonicType newHarmonic)
    {
        if (unit != null && unit.Data != null && unit.Data.harmonicType == newHarmonic)
            EnableEffect();
        else
            DisableEffect();
    }

    /// <summary>
    /// Instancie l'effet d'électricité s'il n'est pas déjà présent.
    /// </summary>
    private void EnableEffect()
    {
        if (electricityPrefab == null || electricityInstance != null)
            return;
        electricityInstance = Instantiate(electricityPrefab, transform.position, Quaternion.identity, transform);
    }

    /// <summary>
    /// Supprime l'effet d'électricité si présent.
    /// </summary>
    private void DisableEffect()
    {
        if (electricityInstance != null)
        {
            Destroy(electricityInstance);
            electricityInstance = null;
        }
    }
}
