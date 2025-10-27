using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.VFX;

/// <summary>
///     Gère la séquence complète d'une explosion de décor en combinant shader dissolve,
///     VFX Graph, morceaux physiques et flash lumineux pour donner l'illusion d'une dislocation.
/// </summary>
public class HybridDecorExplosion : MonoBehaviour
{
    [Header("Décor à dissoudre")]
    [Tooltip("Renderer principal du décor qui doit se dissoudre.")]
    [SerializeField] private Renderer decorRenderer;

    [Tooltip("Nom du paramètre float exposé dans le shader pour contrôler la dissolution.")]
    [SerializeField] private string dissolvePropertyName = "_Dissolve";

    [Tooltip("Valeur initiale du paramètre de dissolution (0 = intact, 1 = complètement dissout).")]
    [Range(0f, 1f)]
    [SerializeField] private float initialDissolveValue = 0f;

    [Tooltip("Durée totale de la transition de dissolve.")]
    [SerializeField] private float dissolveDuration = 0.8f;

    [Tooltip("Courbe de progression utilisée pour lisser la montée de la valeur de dissolve.")]
    [SerializeField] private AnimationCurve dissolveCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Tooltip("Désactive le Renderer une fois la dissolution terminée pour économiser le rendu.")]
    [SerializeField] private bool disableRendererWhenComplete = true;

    [Header("Paramètres VFX principal")]
    [Tooltip("VFX Graph à déclencher pour projeter les éclats visuels de l'explosion.")]
    [SerializeField] private VisualEffect mainExplosionVfx;

    [Tooltip("Nom de l'évènement du VFX Graph déclenchant le burst principal.")]
    [SerializeField] private string mainBurstEventName = "OnBurst";

    [Tooltip("Nom du paramètre Vector3 donnant le centre du burst.")]
    [SerializeField] private string burstCenterProperty = "burstCenter";

    [Tooltip("Nom du paramètre float représentant le rayon de l'explosion dans le VFX Graph.")]
    [SerializeField] private string burstRadiusProperty = "burstRadius";

    [Tooltip("Nom du paramètre entier déterminant le nombre de particules émises.")]
    [SerializeField] private string burstCountProperty = "spawnCount";

    [Tooltip("Rayon transmis au VFX pour contrôler l'étendue du burst.")]
    [SerializeField] private float mainBurstRadius = 3f;

    [Tooltip("Nombre de particules envoyées lors du burst.")]
    [SerializeField] private int mainBurstCount = 4000;

    [Tooltip("Décalage temporel (en secondes) entre le début de la séquence et le burst VFX.")]
    [SerializeField] private float mainBurstDelay = 0.6f;

    [Header("VFX secondaire (poussière ou fumée)")]
    [Tooltip("VFX optionnel pour gérer la traînée de poussière/fumée.")]
    [SerializeField] private VisualEffect secondaryVfx;

    [Tooltip("Retard avant l'activation du VFX secondaire.")]
    [SerializeField] private float secondaryVfxDelay = 0.8f;

    [Header("Morceaux physiques 'héros'")]
    [Tooltip("Rigidbodies désactivés qui seront relâchés pour simuler des morceaux solides.")]
    [SerializeField] private Rigidbody[] heroChunks = System.Array.Empty<Rigidbody>();

    [Tooltip("Retard avant l'activation des morceaux physiques.")]
    [SerializeField] private float heroChunksDelay = 0.65f;

    [Tooltip("Force d'AddExplosionForce appliquée aux rigidbodies libérés.")]
    [SerializeField] private float heroChunkExplosionForce = 200f;

    [Tooltip("Rayon utilisé par AddExplosionForce.")]
    [SerializeField] private float heroChunkExplosionRadius = 3f;

    [Tooltip("Modificateur vertical pour AddExplosionForce.")]
    [SerializeField] private float heroChunkUpwardsModifier = 1f;

    [Tooltip("Rayon dans lequel les morceaux sont initialement replacés autour du centre.")]
    [SerializeField] private float heroChunkSpawnRadius = 0.5f;

    [Header("Flash lumineux")]
    [Tooltip("Lumière activée brièvement pour simuler le flash de l'explosion.")]
    [SerializeField] private Light flashLight;

    [Tooltip("Intensité maximale du flash.")]
    [SerializeField] private float flashPeakIntensity = 8f;

    [Tooltip("Portée maximale pendant le flash.")]
    [SerializeField] private float flashPeakRange = 10f;

    [Tooltip("Retard avant le démarrage du flash lumineux.")]
    [SerializeField] private float flashDelay = 0.7f;

    [Tooltip("Durée totale du fade-out de la lumière.")]
    [SerializeField] private float flashFadeDuration = 0.4f;

    [Header("Divers")]
    [Tooltip("Transform optionnel pour définir manuellement le centre de l'explosion.")]
    [SerializeField] private Transform explosionCenterOverride;

    [Tooltip("Évènement envoyé lorsque la séquence d'explosion est terminée.")]
    [SerializeField] private UnityEvent onExplosionComplete;

    private readonly List<Vector3> heroChunksInitialLocalPositions = new List<Vector3>();
    private readonly List<Quaternion> heroChunksInitialLocalRotations = new List<Quaternion>();
    private readonly List<bool> heroChunksInitialActiveStates = new List<bool>();

    private MaterialPropertyBlock propertyBlock;
    private int dissolvePropertyId;
    private float currentDissolveValue;
    private Coroutine runningSequence;
    private float baseLightIntensity;
    private float baseLightRange;

    private void Awake()
    {
        // Préparation des références automatiques pour simplifier l'usage dans la scène.
        if (decorRenderer == null)
        {
            decorRenderer = GetComponentInChildren<Renderer>();
        }

        // Mise en cache de l'identifiant du paramètre shader afin d'éviter une allocation répétée.
        dissolvePropertyId = Shader.PropertyToID(dissolvePropertyName);
        propertyBlock = new MaterialPropertyBlock();

        // Synchronisation de la valeur initiale dans le shader pour partir d'un décor totalement intact.
        ApplyDissolveValue(initialDissolveValue);

        // Désactivation des chunks physiques en début de partie et mémorisation de leur état pour un éventuel reset.
        CacheHeroChunkStates();

        // Stockage des valeurs de base de la lumière pour pouvoir la restaurer après le flash.
        if (flashLight != null)
        {
            baseLightIntensity = flashLight.intensity;
            baseLightRange = flashLight.range;
            flashLight.intensity = 0f;
        }
    }

    /// <summary>
    ///     Déclenche manuellement la séquence complète d'explosion.
    /// </summary>
    public void TriggerExplosion()
    {
        // Évite de lancer plusieurs séquences simultanément.
        if (runningSequence != null)
        {
            StopCoroutine(runningSequence);
        }

        runningSequence = StartCoroutine(ExplosionSequence());
    }

    /// <summary>
    ///     Remet le décor dans son état initial pour rejouer l'effet (utile en mode Play ou pour des tests).
    /// </summary>
    [ContextMenu("Réinitialiser l'explosion")]
    public void ResetExplosion()
    {
        if (runningSequence != null)
        {
            StopCoroutine(runningSequence);
            runningSequence = null;
        }

        // Réactive le renderer et réapplique la valeur initiale de dissolve.
        if (decorRenderer != null)
        {
            decorRenderer.enabled = true;
        }

        ApplyDissolveValue(initialDissolveValue);

        // Replace les morceaux physiques à leur position d'origine et réinitialise leur vélocité.
        RestoreHeroChunks();

        // Restaure la lumière éventuelle.
        if (flashLight != null)
        {
            flashLight.intensity = 0f;
            flashLight.range = baseLightRange;
        }
    }

    private IEnumerator ExplosionSequence()
    {
        Vector3 explosionCenter = ResolveExplosionCenter();

        // Lance immédiatement la coroutine de dissolution pour qu'elle progresse pendant les autres actions.
        Coroutine dissolveRoutine = null;
        if (decorRenderer != null)
        {
            dissolveRoutine = StartCoroutine(DissolveMesh());
        }

        float timelineCursor = 0f;

        // 1) Burst principal du VFX Graph.
        if (mainBurstDelay >= 0f)
        {
            float wait = Mathf.Max(0f, mainBurstDelay - timelineCursor);
            if (wait > 0f)
            {
                yield return new WaitForSeconds(wait);
            }

            TriggerMainBurst(explosionCenter);
            timelineCursor = Mathf.Max(timelineCursor, mainBurstDelay);
        }

        // 2) Activation des chunks physiques.
        if (heroChunksDelay >= 0f)
        {
            float wait = Mathf.Max(0f, heroChunksDelay - timelineCursor);
            if (wait > 0f)
            {
                yield return new WaitForSeconds(wait);
            }

            ActivateHeroChunks(explosionCenter);
            timelineCursor = Mathf.Max(timelineCursor, heroChunksDelay);
        }

        // 3) Flash lumineux pour renforcer l'explosion.
        if (flashDelay >= 0f)
        {
            float wait = Mathf.Max(0f, flashDelay - timelineCursor);
            if (wait > 0f)
            {
                yield return new WaitForSeconds(wait);
            }

            TriggerFlash();
            timelineCursor = Mathf.Max(timelineCursor, flashDelay);
        }

        // 4) VFX secondaire (fumée/poussière).
        if (secondaryVfx != null && secondaryVfxDelay >= 0f)
        {
            float wait = Mathf.Max(0f, secondaryVfxDelay - timelineCursor);
            if (wait > 0f)
            {
                yield return new WaitForSeconds(wait);
            }

            TriggerSecondaryVfx(explosionCenter);
            timelineCursor = Mathf.Max(timelineCursor, secondaryVfxDelay);
        }

        // Attend la fin complète de la dissolution pour nettoyer le renderer si demandé.
        if (dissolveRoutine != null)
        {
            yield return dissolveRoutine;
        }

        onExplosionComplete?.Invoke();
        runningSequence = null;
    }

    private IEnumerator DissolveMesh()
    {
        float elapsed = 0f;
        currentDissolveValue = Mathf.Clamp01(currentDissolveValue);

        while (elapsed < dissolveDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = dissolveDuration > 0f ? Mathf.Clamp01(elapsed / dissolveDuration) : 1f;
            float curveValue = dissolveCurve != null ? dissolveCurve.Evaluate(normalizedTime) : normalizedTime;
            currentDissolveValue = Mathf.Lerp(initialDissolveValue, 1f, curveValue);
            ApplyDissolveValue(currentDissolveValue);
            yield return null;
        }

        // S'assure que le décor est complètement désactivé en fin de séquence.
        ApplyDissolveValue(1f);
        if (decorRenderer != null && disableRendererWhenComplete)
        {
            decorRenderer.enabled = false;
        }
    }

    private void TriggerMainBurst(Vector3 center)
    {
        if (mainExplosionVfx == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(burstCenterProperty))
        {
            mainExplosionVfx.SetVector3(burstCenterProperty, center);
        }

        if (!string.IsNullOrEmpty(burstRadiusProperty))
        {
            mainExplosionVfx.SetFloat(burstRadiusProperty, mainBurstRadius);
        }

        if (!string.IsNullOrEmpty(burstCountProperty))
        {
            mainExplosionVfx.SetInt(burstCountProperty, mainBurstCount);
        }

        if (!string.IsNullOrEmpty(mainBurstEventName))
        {
            mainExplosionVfx.SendEvent(mainBurstEventName);
        }
        else
        {
            mainExplosionVfx.Play();
        }
    }

    private void TriggerSecondaryVfx(Vector3 center)
    {
        if (secondaryVfx == null)
        {
            return;
        }

        secondaryVfx.transform.position = center;
        secondaryVfx.Play();
    }

    private void ActivateHeroChunks(Vector3 center)
    {
        if (heroChunks == null || heroChunks.Length == 0)
        {
            return;
        }

        for (int i = 0; i < heroChunks.Length; i++)
        {
            Rigidbody rb = heroChunks[i];
            if (rb == null)
            {
                continue;
            }

            // Réactive l'objet si nécessaire puis le repositionne proche du centre d'explosion.
            if (!rb.gameObject.activeSelf)
            {
                rb.gameObject.SetActive(true);
            }

            rb.transform.position = center + Random.insideUnitSphere * heroChunkSpawnRadius;
            rb.transform.rotation = Random.rotation;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddExplosionForce(heroChunkExplosionForce, center, heroChunkExplosionRadius, heroChunkUpwardsModifier, ForceMode.Impulse);
        }
    }

    private void TriggerFlash()
    {
        if (flashLight == null)
        {
            return;
        }

        flashLight.intensity = flashPeakIntensity;
        flashLight.range = flashPeakRange;
        StartCoroutine(FadeLight());
    }

    private IEnumerator FadeLight()
    {
        float elapsed = 0f;
        float startIntensity = flashLight.intensity;
        float startRange = flashLight.range;

        while (elapsed < flashFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = flashFadeDuration > 0f ? Mathf.Clamp01(elapsed / flashFadeDuration) : 1f;
            flashLight.intensity = Mathf.Lerp(startIntensity, 0f, t);
            flashLight.range = Mathf.Lerp(startRange, baseLightRange, t);
            yield return null;
        }

        flashLight.intensity = 0f;
        flashLight.range = baseLightRange;
    }

    private void ApplyDissolveValue(float value)
    {
        currentDissolveValue = Mathf.Clamp01(value);
        if (decorRenderer == null)
        {
            return;
        }

        decorRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(dissolvePropertyId, currentDissolveValue);
        decorRenderer.SetPropertyBlock(propertyBlock);
    }

    private Vector3 ResolveExplosionCenter()
    {
        if (explosionCenterOverride != null)
        {
            return explosionCenterOverride.position;
        }

        if (decorRenderer != null)
        {
            return decorRenderer.bounds.center;
        }

        return transform.position;
    }

    private void CacheHeroChunkStates()
    {
        heroChunksInitialLocalPositions.Clear();
        heroChunksInitialLocalRotations.Clear();
        heroChunksInitialActiveStates.Clear();

        if (heroChunks == null)
        {
            return;
        }

        foreach (Rigidbody rb in heroChunks)
        {
            if (rb == null)
            {
                heroChunksInitialLocalPositions.Add(Vector3.zero);
                heroChunksInitialLocalRotations.Add(Quaternion.identity);
                heroChunksInitialActiveStates.Add(false);
                continue;
            }

            heroChunksInitialLocalPositions.Add(rb.transform.localPosition);
            heroChunksInitialLocalRotations.Add(rb.transform.localRotation);
            heroChunksInitialActiveStates.Add(rb.gameObject.activeSelf);
            rb.gameObject.SetActive(false);
        }
    }

    private void RestoreHeroChunks()
    {
        if (heroChunks == null)
        {
            return;
        }

        for (int i = 0; i < heroChunks.Length; i++)
        {
            Rigidbody rb = heroChunks[i];
            if (rb == null)
            {
                continue;
            }

            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.transform.localPosition = i < heroChunksInitialLocalPositions.Count ? heroChunksInitialLocalPositions[i] : rb.transform.localPosition;
            rb.transform.localRotation = i < heroChunksInitialLocalRotations.Count ? heroChunksInitialLocalRotations[i] : rb.transform.localRotation;

            bool shouldBeActive = i < heroChunksInitialActiveStates.Count && heroChunksInitialActiveStates[i];
            rb.gameObject.SetActive(shouldBeActive);
        }
    }
}
