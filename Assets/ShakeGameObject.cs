using UnityEngine;
using System.Collections;

public class ShakeGameObject : MonoBehaviour
{
    [Header("Target")]
    public GameObject target; // L'objet à secouer

    [Header("Shake Settings")]
    public float shakeStrength = 0.1f;  // Amplitude du tremblement
    public float shakeFrequency = 25f;  // Vitesse du tremblement

    private Vector3 originalPos;
    private Coroutine shakeCoroutine;

    /// <summary>
    /// Lance le tremblement sur l'objet assigné (continu jusqu'à Stop).
    /// </summary>
    public void StartShaking()
    {
        if (target == null) return;

        originalPos = target.transform.localPosition;

        if (shakeCoroutine != null)
            StopCoroutine(shakeCoroutine);

        shakeCoroutine = StartCoroutine(ShakeRoutine());
    }

    /// <summary>
    /// Arrête immédiatement le tremblement et remet l'objet à sa position initiale.
    /// </summary>
    public void StopShaking()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }

        if (target != null)
            target.transform.localPosition = originalPos;
    }

    /// <summary>
    /// Arrête le tremblement de manière progressive.
    /// </summary>
    public void StopShakingSmooth(float fadeDuration = 0.5f)
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }

        if (target != null)
            shakeCoroutine = StartCoroutine(SmoothStopRoutine(fadeDuration));
    }

    private IEnumerator ShakeRoutine()
    {
        while (true) // Boucle infinie tant que non stoppé
        {
            Vector3 offset = Random.insideUnitSphere * shakeStrength;
            target.transform.localPosition = originalPos + offset;

            yield return new WaitForSeconds(1f / shakeFrequency);
        }
    }

    private IEnumerator SmoothStopRoutine(float duration)
    {
        float elapsed = 0f;
        float startStrength = shakeStrength;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - (elapsed / duration);

            Vector3 offset = Random.insideUnitSphere * (startStrength * t);
            target.transform.localPosition = originalPos + offset;

            yield return new WaitForSeconds(1f / shakeFrequency);
        }

        target.transform.localPosition = originalPos;
        shakeCoroutine = null;
    }
}
