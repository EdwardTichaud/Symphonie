using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Composant UI pour afficher la barre de rage d'une unité.
/// </summary>
public class RageBar : MonoBehaviour
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
            slider.value = max;
        }
    }

    public void SetValue(int current)
    {
        if (slider != null)
        {
            slider.value = current;

            if (current >= slider.maxValue)
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
