using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Composant UI pour afficher la barre de vie d'une unit�.
/// </summary>
public class HPBar : MonoBehaviour
{
    [Header("R�f�rence Slider")]
    public Slider slider;

    /// <summary>
    /// D�finit la valeur et la limite maximale de la barre.
    /// </summary>
    /// <param name="max">Valeur maximale (vie max)</param>
    public void SetMaxValue(float max)
    {
        if (slider != null)
        {
            slider.maxValue = max;
            slider.value = max;
        }
    }

    /// <summary>
    /// Met � jour la valeur courante de la barre.
    /// </summary>
    /// <param name="current">Valeur actuelle (vie restante)</param>
    public void SetValue(float current)
    {
        if (slider != null)
        {
            slider.value = current;
        }
    }
}