using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Composant UI pour afficher la barre de rage d'une unité.
/// </summary>
public class RageBar : MonoBehaviour
{
    [Header("Référence Slider")]
    public Slider slider;

    public void SetMaxValue(int max)
    {
        if (slider != null)
        {
            slider.maxValue = max;
            slider.value = max;
        }
    }

    public void SetValue(int current)
    {
        if (slider != null)
        {
            slider.value = current;
        }
    }
}
