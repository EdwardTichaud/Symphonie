using UnityEngine;
using System.Collections;

public class Shake : MonoBehaviour
{
    [Header("Shake Settings")]
    public float shakeStrength = 0.1f;    // Amplitude du tremblement
    public float shakeFrequency = 25f;    // Vitesse du tremblement

    private Vector3 originalPos;
    private Coroutine shakeCoroutine;

    void Awake()
    {
        originalPos = transform.localPosition;
    }

    /// <summary>
    /// Lance un tremblement (appelé par une signal Timeline ou un autre script).
    /// </summary>
    public void StartShake(float shakeDuration)
    {
        if (shakeCoroutine != null)
            StopCoroutine(shakeCoroutine);

        shakeCoroutine = StartCoroutine(ShakeRoutine(shakeDuration));
    }

    private IEnumerator ShakeRoutine(float shakeDuration)
    {
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float damper = 1f - (elapsed / shakeDuration); // Atténue vers la fin

            Vector3 offset = Random.insideUnitSphere * shakeStrength * damper;
            transform.localPosition = originalPos + offset;

            yield return null;
        }

        transform.localPosition = originalPos;
        shakeCoroutine = null;
    }
}
