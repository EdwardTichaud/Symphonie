using UnityEngine;

[RequireComponent(typeof(Light))]
[ExecuteAlways]
public class FireLightFlicker : MonoBehaviour
{
    [Header("Intensity Settings")]
    [Tooltip("Minimum light intensity during flicker")] public float minIntensity = 0.8f;
    [Tooltip("Maximum light intensity during flicker")] public float maxIntensity = 1.2f;
    [Tooltip("Speed of flickering")] public float flickerSpeed = 1.0f;

    [Header("Color Variation")]
    [Tooltip("Maximum deviation from base color")] public float colorVariation = 0.1f;

    private Light fireLight;
    private float baseIntensity;
    private Color baseColor;
    private float noiseTime;

    void Awake()
    {
        // R�cup�re la Light attach�e
        fireLight = GetComponent<Light>();
        // Sauvegarde les valeurs de base
        baseIntensity = fireLight.intensity;
        baseColor = fireLight.color;
        // Initialise le temps de Perlin al�atoirement pour �viter la synchro
        noiseTime = Random.Range(0f, 100f);
    }

    void Update()
    {
        // Incr�mente le temps pour le Perlin Noise
        noiseTime += Time.deltaTime * flickerSpeed;
        // G�n�re une valeur de bruit entre 0 et 1
        float noise = Mathf.PerlinNoise(noiseTime, 0f);

        // Interpole l'intensit� entre min et max
        fireLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);

        // L�g�re variation de couleur bas�e sur le bruit
        float offset = (noise - 0.5f) * 2f * colorVariation;
        fireLight.color = baseColor * (1f + offset);
    }
}
