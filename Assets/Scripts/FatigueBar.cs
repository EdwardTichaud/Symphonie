using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Composant UI pour afficher la barre de fatigue d'une unité.
/// </summary>
public class FatigueBar : MonoBehaviour
{
    [Header("Référence Slider")] 
    public Slider slider;

    public void SetMaxValue(int max)
    {
        if (slider != null)
        {
            slider.maxValue = max;
            slider.value = 0;
        }
    }

    public void SetValue(int value)
    {
        if (slider != null)
            slider.value = value;
    }
}
