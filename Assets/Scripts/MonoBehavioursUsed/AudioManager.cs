using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public AudioClip[] musicTracks;
    public AudioClip[] soundEffects;
    public AudioClip[] voiceEffects;

    [Header("Audio Sources")]
    public AudioSource musicSourceA;
    public AudioSource musicSourceB;
    public AudioSource sfxSource;
    public AudioSource voiceSource;
    // Nouvelle source dédiée aux sons d'avertissement
    public AudioSource warningClipSource;

    [Header("Fade Settings")]
    public float fadeDuration = 2f;

    [Header("Volume Settings")]
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float voiceVolume = 1f;

    [Header("Normalisation automatique")]
    [Tooltip("Calcule un facteur pour que tous les clips aient un volume perçu similaire")]
    public bool autoNormalize = true;
    [Range(0f, 1f)] public float targetRms = 0.1f;

    public static AudioManager Instance { get; private set; }

    private AudioSource currentMusicSource;
    private AudioSource nextMusicSource;

    private Coroutine crossfadeRoutine;
    private AudioClip lastExplorationClip;
    private float lastExplorationTime;

    private bool isInCombat = false;

    private Dictionary<AudioClip, float> explorationPlaybackPositions = new Dictionary<AudioClip, float>();
    private Dictionary<AudioClip, float> normalizationCache = new Dictionary<AudioClip, float>();

    /// <summary>
    /// Met à jour le volume de la source d'avertissement en fonction de la musique.
    /// </summary>
    private void UpdateWarningVolume()
    {
        // Volume deux fois plus élevé que celui des sources de musique
        warningClipSource.volume = musicVolume * 2f;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        musicSourceA.loop = true;
        musicSourceB.loop = true;

        currentMusicSource = musicSourceA;
        nextMusicSource = musicSourceB;

        sfxSource.playOnAwake = false;
        voiceSource.playOnAwake = false;
        warningClipSource.playOnAwake = false;

        SetMusicVolume(musicVolume);
        SetSfxVolume(sfxVolume);
        SetVoiceVolume(voiceVolume);
        UpdateWarningVolume();
    }

    private float GetNormalizationFactor(AudioClip clip)
    {
        if (!autoNormalize || clip == null)
            return 1f;

        if (normalizationCache.TryGetValue(clip, out float factor))
            return factor;

        try
        {
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            double sumSq = 0;
            for (int i = 0; i < samples.Length; i++)
                sumSq += samples[i] * samples[i];

            double rms = Mathf.Sqrt((float)(sumSq / samples.Length));
            factor = rms > 0 ? targetRms / (float)rms : 1f;
        }
        catch
        {
            factor = 1f;
        }

        normalizationCache[clip] = factor;
        return factor;
    }

    #region \U0001F4E3 Volume

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        float factorA = GetNormalizationFactor(musicSourceA.clip);
        float factorB = GetNormalizationFactor(musicSourceB.clip);
        musicSourceA.volume = musicVolume * factorA;
        musicSourceB.volume = musicVolume * factorB;
        UpdateWarningVolume();
    }

    public void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        sfxSource.volume = sfxVolume;
    }

    public void SetVoiceVolume(float value)
    {
        voiceVolume = Mathf.Clamp01(value);
        voiceSource.volume = voiceVolume;
    }

    #endregion

    #region 🎵 Musique : Transitions

    public void PlayExplorationMusic(AudioClip newExplorationClip)
    {
        if (isInCombat || newExplorationClip == currentMusicSource.clip)
            return;

        lastExplorationClip = newExplorationClip;

        float resumeTime = explorationPlaybackPositions.TryGetValue(newExplorationClip, out float savedTime)
            ? savedTime
            : 0f;

        StartCrossfade(newExplorationClip, resumeTime);
        isInCombat = false;
    }

    public void TransitionToNewExplorationZone(AudioClip newExplorationClip)
    {
        if (isInCombat || newExplorationClip == currentMusicSource.clip)
            return;

        // Sauvegarde la position de la musique actuelle (si c'était une musique d'exploration)
        if (!isInCombat && currentMusicSource.clip != null)
        {
            explorationPlaybackPositions[currentMusicSource.clip] = currentMusicSource.time;
        }

        lastExplorationClip = newExplorationClip;

        // Si on a déjà une position sauvegardée, on la reprend
        float resumeTime = explorationPlaybackPositions.TryGetValue(newExplorationClip, out float savedTime)
            ? savedTime
            : 0f;

        StartCrossfade(newExplorationClip, resumeTime);
    }

    public void TransitionToCombat(AudioClip combatClip)
    {
        if (isInCombat)
            return;

        lastExplorationClip = currentMusicSource.clip;
        lastExplorationTime = currentMusicSource.time;
        isInCombat = true;

        // Brutal switch
        SwitchImmediately(combatClip);
    }

    public void ReturnFromBattle()
    {
        if (!isInCombat || lastExplorationClip == null)
            return;

        StartCrossfade(lastExplorationClip, lastExplorationTime);
        isInCombat = false;
    }

    private void StartCrossfade(AudioClip newClip, float startTime)
    {
        if (crossfadeRoutine != null)
            StopCoroutine(crossfadeRoutine);

        crossfadeRoutine = StartCoroutine(CrossfadeMusic(newClip, startTime));
    }

    private void SwitchImmediately(AudioClip newClip)
    {
        if (crossfadeRoutine != null)
            StopCoroutine(crossfadeRoutine);

        currentMusicSource.Stop();

        // Swap sources
        var temp = currentMusicSource;
        currentMusicSource = nextMusicSource;
        nextMusicSource = temp;

        currentMusicSource.clip = newClip;
        currentMusicSource.time = 0f;
        currentMusicSource.volume = musicVolume * GetNormalizationFactor(newClip);
        currentMusicSource.Play();
    }

    private IEnumerator CrossfadeMusic(AudioClip newClip, float startTime)
    {
        AudioSource fromSource = currentMusicSource;
        float fromFactor = GetNormalizationFactor(fromSource.clip);
        AudioSource toSource = (currentMusicSource == musicSourceA) ? musicSourceB : musicSourceA;

        toSource.clip = newClip;
        toSource.time = startTime;
        float toFactor = GetNormalizationFactor(newClip);
        toSource.volume = 0f;
        toSource.Play();

        currentMusicSource = toSource;
        nextMusicSource = fromSource;

        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float progress = t / fadeDuration;

            toSource.volume = Mathf.Lerp(0f, musicVolume * toFactor, progress);
            fromSource.volume = Mathf.Lerp(musicVolume * fromFactor, 0f, progress);
            yield return null;
        }

        fromSource.Stop();
        fromSource.volume = musicVolume * GetNormalizationFactor(fromSource.clip);
        toSource.volume = musicVolume * toFactor;

        crossfadeRoutine = null;
    }

    #endregion

    #region 🔊 Effets

    public void PlaySfx(int index)
    {
        AudioClip clip = soundEffects[index];
        float factor = GetNormalizationFactor(clip);
        sfxSource.PlayOneShot(clip, factor);
    }

    public void PlayVoice(int index) => PlayVoice(voiceEffects[index]);

    public void PlayVoice(AudioClip clip)
    {
        if (clip == null) return;

        // Crée une nouvelle source audio à partir du modèle existant
        AudioSource tempSource = Instantiate(voiceSource, transform);
        tempSource.playOnAwake = false;
        tempSource.clip = clip;
        tempSource.volume = voiceVolume * GetNormalizationFactor(clip);
        tempSource.Play();
        Destroy(tempSource.gameObject, clip.length);
    }

    /// <summary>
    /// Joue un clip d'avertissement en utilisant la source dédiée.
    /// </summary>
    public void PlayWarningClip(AudioClip clip)
    {
        if (clip == null) return;

        float factor = GetNormalizationFactor(clip);
        warningClipSource.PlayOneShot(clip, factor);
    }

    public void PlaySound(AudioClip clip)
    {
        float factor = GetNormalizationFactor(clip);
        sfxSource.PlayOneShot(clip, factor);
    }

    public void PlayTempSfx(AudioClip clip)
    {
        if (clip == null) return;

        AudioSource tempSource = Instantiate(sfxSource, transform);
        tempSource.playOnAwake = false;
        tempSource.clip = clip;
        tempSource.volume = sfxVolume * GetNormalizationFactor(clip);
        tempSource.Play();
        Destroy(tempSource.gameObject, clip.length);
    }

    #endregion
}
