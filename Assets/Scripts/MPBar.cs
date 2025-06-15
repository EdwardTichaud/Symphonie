using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Composant UI pour afficher la barre de mana d'une unité.
/// </summary>
public class MPBar : MonoBehaviour
{
    [Header("Référence Slider")]
    public Slider slider;

    /// <summary>
    /// Définit la valeur et la limite maximale de la barre.
    /// </summary>
    /// <param name="max">Valeur maximale (mana max)</param>
    public void SetMaxValue(int max)
    {
        if (slider != null)
        {
            slider.maxValue = max;
            slider.value = max;
        }
    }

    /// <summary>
    /// Met à jour la valeur courante de la barre.
    /// </summary>
    /// <param name="current">Valeur actuelle (mana restant)</param>
    public void SetValue(int current)
    {
        if (slider != null)
        {
            slider.value = current;
        }
    }
}