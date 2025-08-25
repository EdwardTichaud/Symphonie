using UnityEngine;
using System.Collections;

public class Shake : MonoBehaviour
{
    [Header("Shake Settings")]
    [Tooltip("Amplitude du tremblement")]
    public float shakeStrength = 0.1f;

    [Tooltip("Vitesse du tremblement (mouvements par seconde)")]
    public float shakeFrequency = 25f;

    [Header("Inspector Control")]
    [Tooltip("Durée utilisée par le bouton 'Start Shake' de l’inspecteur. <= 0 = shake infini jusqu’à Stop.")]
    public float inspectorDuration = 0.5f;

    private Vector3 originalPos;
    private Coroutine shakeCoroutine;

    void Awake()
    {
        // On ne capture plus ici pour éviter les surprises si l’objet bouge avant le shake.
        // originalPos sera pris au moment du StartShake.
    }

    /// <summary>
    /// Lance un tremblement. Si shakeDuration <= 0, le tremblement continue jusqu'à StopShake().
    /// </summary>
    public void StartShake(float shakeDuration)
    {
        // Capture la position de départ au moment du lancement
        originalPos = transform.localPosition;

        if (shakeCoroutine != null)
            StopCoroutine(shakeCoroutine);

        shakeCoroutine = StartCoroutine(ShakeRoutine(shakeDuration));
    }

    /// <summary>
    /// Arrête immédiatement le tremblement et remet l’objet à sa position d’origine.
    /// </summary>
    public void StopShake()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }

        transform.localPosition = originalPos;
    }

    /// <summary>
    /// Pratique pour Timeline Signals ou Animation Events.
    /// </summary>
    public void StartShakeFromSignal(float duration)
    {
        StartShake(duration);
    }

    private IEnumerator ShakeRoutine(float shakeDuration)
    {
        float elapsed = 0f;
        float invFreq = shakeFrequency > 0f ? 1f / shakeFrequency : 0f;
        float nextStep = 0f;

        // Durée infinie si <= 0
        bool infinite = shakeDuration <= 0f;

        while (infinite || elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;

            // Damper: 1 en infini, sinon atténuation vers la fin
            float damper = infinite ? 1f : (1f - (elapsed / Mathf.Max(0.0001f, shakeDuration)));

            // Avance seulement aux "pas" de fréquence (sinon c’est trop jitter selon FPS)
            if (Time.time >= nextStep)
            {
                Vector3 offset = Random.insideUnitSphere * shakeStrength * damper;
                transform.localPosition = originalPos + offset;
                nextStep = Time.time + invFreq;
            }

            yield return null;
        }

        // Fin : on remet proprement
        transform.localPosition = originalPos;
        shakeCoroutine = null;
    }

    private void OnDisable()
    {
        // Sécurité si l’objet est désactivé en plein shake
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
            transform.localPosition = originalPos;
        }
    }

    // --- Raccourcis de contexte (clic droit sur la barre du composant) ---

    [ContextMenu("Start Shake (Inspector Duration)")]
    private void Ctx_StartShakeInspector()
    {
        StartShake(inspectorDuration);
    }

    [ContextMenu("Stop Shake")]
    private void Ctx_StopShake()
    {
        StopShake();
    }
}
