using System;
using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class ZoneNameDisplay : MonoBehaviour
{
    public static ZoneNameDisplay Instance { get; private set; }

    [Header("UI References")]
    public TextMeshProUGUI sceneNameText;
    public TextMeshProUGUI sceneDescriptionText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private ParticleSystem[] particleSystems;

    [Header("Animation")]
    [SerializeField, Range(0.1f, 5f)] private float fadeDurationSeconds = 1f;
    [SerializeField, Range(0f, 20f)] private float displayDurationSeconds = 5f;
    [SerializeField, Range(0f, 50f)] private float particleEmissionTarget = 5f;

    private Coroutine displayRoutine;

    private struct ParticleState
    {
        public ParticleSystem system;
    }

    private ParticleState[] particleStates = Array.Empty<ParticleState>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (!GameRoot.KeepManagersSceneBound)
            DontDestroyOnLoad(gameObject);

        canvasGroup = canvasGroup ?? GetComponentInChildren<CanvasGroup>(true) ?? gameObject.AddComponent<CanvasGroup>();
        InitializeCanvasGroup();
        CacheParticleStates();
        HideImmediate();
    }

    /// <summary>
    /// Récupère la ZoneSO actuelle via ZoneManager et met à jour le texte.
    /// </summary>
    public void ShowCurrentZoneInfo()
    {
        if (ZoneManager.Instance == null || ZoneManager.Instance.currentZone == null)
        {
            Debug.LogWarning("[LevelName] Aucune Zone courante trouvée !");
            return;
        }

        ZoneSO currentZone = ZoneManager.Instance.currentZone;

        sceneNameText.text = currentZone.zoneName;
        sceneDescriptionText.text = currentZone.description;

        if (displayRoutine != null)
            StopCoroutine(displayRoutine);

        displayRoutine = StartCoroutine(DisplaySequence());

        Debug.Log($"[LevelName] Affiche : {currentZone.zoneName} / {currentZone.description}");
    }

    private void InitializeCanvasGroup()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private void CacheParticleStates()
    {
        ParticleSystem[] resolvedSystems = particleSystems != null && particleSystems.Length > 0
            ? particleSystems
            : GetComponentsInChildren<ParticleSystem>(true);

        if (resolvedSystems == null || resolvedSystems.Length == 0)
        {
            particleStates = Array.Empty<ParticleState>();
            return;
        }

        particleStates = new ParticleState[resolvedSystems.Length];
        for (int i = 0; i < resolvedSystems.Length; i++)
        {
            var ps = resolvedSystems[i];
            if (ps == null)
                continue;

            particleStates[i] = new ParticleState
            {
                system = ps
            };

            SetParticleEmission(ps, 0f);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private IEnumerator DisplaySequence()
    {
        SetContainerActive(true);
        yield return FadeTo(1f);
        if (displayDurationSeconds > 0f)
            yield return new WaitForSecondsRealtime(displayDurationSeconds);
        yield return FadeTo(0f);
        SetContainerActive(false);
        displayRoutine = null;
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        if (canvasGroup == null)
            yield break;

        float startAlpha = canvasGroup.alpha;
        float duration = Mathf.Max(0.01f, fadeDurationSeconds);
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            ApplyVisualState(alpha);
            yield return null;
        }

        ApplyVisualState(targetAlpha);
        canvasGroup.blocksRaycasts = targetAlpha > 0f;
        canvasGroup.interactable = targetAlpha > 0f;
    }

    private void ApplyVisualState(float alpha)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = Mathf.Clamp01(alpha);

        UpdateParticleEmission(alpha);
    }

    private void UpdateParticleEmission(float normalizedAlpha)
    {
        if (particleStates == null || particleStates.Length == 0)
            return;

        float clamped = Mathf.Clamp01(normalizedAlpha);
        float targetRate = Mathf.Lerp(0f, particleEmissionTarget, clamped);
        foreach (var state in particleStates)
        {
            var system = state.system;
            if (system == null)
                continue;

            SetParticleEmission(system, targetRate);

            if (targetRate > 0.001f)
            {
                if (!system.isPlaying)
                    system.Play(true);
            }
            else if (system.isPlaying)
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    private void HideImmediate()
    {
        if (canvasGroup == null)
            return;

        SetContainerActive(false);
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        UpdateParticleEmission(0f);
    }

    private void SetContainerActive(bool active)
    {
        if (canvasGroup == null)
            return;

        var target = canvasGroup.gameObject;
        if (target == null)
            return;

        if (target == gameObject)
            return;

        if (target.activeSelf == active)
            return;

        target.SetActive(active);
    }

    private void SetParticleEmission(ParticleSystem system, float value)
    {
        if (system == null)
            return;

        var emission = system.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(value);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
