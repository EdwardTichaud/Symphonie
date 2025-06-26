using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Composant UI pour afficher la barre de fatigue d'une unité.
/// </summary>
public class FatigueBar : MonoBehaviour
{
    [Header("Référence Slider")]
    public Slider slider;

    [Header("Effet de tremblement")]
    public float shakeDuration = 0.15f;
    public float shakeMagnitude = 2f;

    private Coroutine shakeRoutine;

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
        {
            slider.value = value;

            if (value >= slider.maxValue)
                TriggerShake();
        }
    }

    private void TriggerShake()
    {
        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        shakeRoutine = StartCoroutine(ShakeCoroutine());
    }

    private IEnumerator ShakeCoroutine()
    {
        RectTransform target = slider.GetComponent<RectTransform>();
        if (target == null)
            yield break;

        Vector3 originalPos = target.localPosition;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-shakeMagnitude, shakeMagnitude);
            float y = Random.Range(-shakeMagnitude, shakeMagnitude);
            target.localPosition = originalPos + new Vector3(x, y, 0f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        target.localPosition = originalPos;
        shakeRoutine = null;
    }
}
