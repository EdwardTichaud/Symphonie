using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Composant UI pour afficher la barre de vie d'une unit.
/// </summary>
public class HPBar : MonoBehaviour
{
    [Header("Références Slider")]
    [Tooltip("Slider Unity utilisé pour représenter la barre de vie classiquement (gauche -> droite).")] // Aide les artistes à identifier le champ attendu dans l'inspecteur.
    public Slider slider;

    [Header("Réduction visuelle optionnelle")]
    [Tooltip("RectTransform optionnel à contracter horizontalement pour des barres basées sur l'échelle plutôt que sur un Slider.")]
    [SerializeField] private RectTransform dynamicFillRect;

    /// <summary>Valeur maximale mémorisée pour calculer correctement le ratio d'affichage.</summary>
    private float maxValue = 1f;
    /// <summary>Valeur actuelle retenue pour piloter l'UI.</summary>
    private float currentValue = 1f;

    /// <summary>
    /// Définit la valeur maximale de la barre et réinitialise l'affichage pour garantir
    /// que la jauge est pleine lorsque l'unité est fraîchement créée ou soignée à 100%.
    /// </summary>
    /// <param name="max">Valeur maximale (vie max).</param>
    public virtual void SetMaxValue(float max)
    {
        maxValue = Mathf.Max(0f, max); // Sécurise la valeur afin d'éviter toute division par zéro plus tard.

        // S'assure que la valeur courante reste cohérente avec la nouvelle limite.
        currentValue = Mathf.Clamp(currentValue, 0f, maxValue);

        UpdateSlider(currentValue, forceRefresh: true);
        UpdateDynamicFill();
    }

    /// <summary>
    /// Met à jour la valeur courante de la barre en répercutant immédiatement la perte/gain de PV
    /// sur les éléments visuels associés (Slider, barre qui se contracte, etc.).
    /// </summary>
    /// <param name="current">Valeur actuelle (vie restante).</param>
    public virtual void SetValue(float current)
    {
        // Empêche de dépasser les bornes connues afin de garder un ratio sain.
        currentValue = Mathf.Clamp(current, 0f, maxValue > 0f ? maxValue : current);

        UpdateSlider(currentValue);
        UpdateDynamicFill();
    }

    /// <summary>
    /// Synchronise le Slider Unity si un artiste préfère ce workflow.
    /// </summary>
    /// <param name="value">Valeur à afficher.</param>
    /// <param name="forceRefresh">
    /// Indique si le slider doit être explicitement initialisé (utilisé lors d'un changement de vie max).
    /// </param>
    private void UpdateSlider(float value, bool forceRefresh = false)
    {
        if (slider == null)
            return;

        // On fige la valeur minimale à zéro pour éviter que le handle ne remonte si la maxValue change.
        slider.minValue = 0f;

        if (forceRefresh)
        {
            slider.maxValue = maxValue;
        }

        slider.value = value;
    }

    /// <summary>
    /// Adapte dynamiquement la largeur (via l'échelle locale) d'un RectTransform optionnel.
    /// Cette approche est utile pour les barres stylisées qui ne reposent pas sur le composant Slider.
    /// </summary>
    private void UpdateDynamicFill()
    {
        if (dynamicFillRect == null)
            return;

        float ratio = maxValue > 0f ? currentValue / maxValue : 0f;
        ratio = Mathf.Clamp01(ratio);

        // Met à jour uniquement l'axe X afin de préserver l'échelle verticale et la profondeur éventuelle.
        Vector3 scale = dynamicFillRect.localScale;
        scale.x = ratio;
        dynamicFillRect.localScale = scale;
    }
}