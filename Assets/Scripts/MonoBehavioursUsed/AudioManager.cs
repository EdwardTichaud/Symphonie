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

    [Header("Warning Clip Settings")]
    [Tooltip("Pourcentage d'atténuation appliqué aux autres sources lors de la lecture d'un warning clip")]
    [Range(0f, 1f)]
    public float warningAttenuation = 0.8f; // 80 % d'atténuation par défaut

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
    // Coroutine gérant l'atténuation temporaire lors de la lecture d'un warning
    private Coroutine warningRoutine;
    private AudioClip lastExplorationClip;
    private float lastExplorationTime;

    private bool isInCombat = false;

    // 📈 Indique si une timeline est en cours.
    // Permet de suspendre la musique actuelle et de la reprendre à la fin de la cinématique.
    private bool isInTimeline = false;

    // 📊 Sauvegarde l'état avant l'entrée en timeline.
    private bool wasInCombatBeforeTimeline = false; // → vrai si l'on était en combat avant la cinématique
    private AudioClip clipBeforeTimeline;           // → musique en cours avant la timeline
    private float timeBeforeTimeline;               // → position de lecture de cette musique

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
        // Empêche tout changement si une timeline ou un combat est en cours
        if (isInCombat || isInTimeline || newExplorationClip == currentMusicSource.clip)
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
        // Aucun fondu si une timeline ou un combat est actif, ou si le clip est identique
        if (isInCombat || isInTimeline || newExplorationClip == currentMusicSource.clip)
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
        // ⚠️ Ignore si l'on est déjà en combat ou dans une timeline
        if (isInCombat || isInTimeline)
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

    /// <summary>
    /// Lance la musique d'une timeline en sauvegardant l'état précédent
    /// (exploration ou combat).
    /// </summary>
    /// <param name="timelineClip">Musique à jouer durant la cinématique.</param>
    public void TransitionToTimeline(AudioClip timelineClip)
    {
        if (isInTimeline || timelineClip == null)
            return;

        // Mémorise le contexte avant d'entrer en timeline
        wasInCombatBeforeTimeline = isInCombat;
        clipBeforeTimeline = currentMusicSource.clip;
        timeBeforeTimeline = currentMusicSource.time;

        // Les timelines ne sont ni exploration ni combat
        isInTimeline = true;
        isInCombat = false;

        // Démarre la musique de timeline en fondu
        StartCrossfade(timelineClip, 0f);
    }

    /// <summary>
    /// Restaure la musique précédente après une timeline.
    /// </summary>
    public void ReturnFromTimeline()
    {
        if (!isInTimeline || clipBeforeTimeline == null)
            return;

        // Reprend la musique précédente à sa position
        StartCrossfade(clipBeforeTimeline, timeBeforeTimeline);

        // Rétablit l'état initial (combat ou exploration)
        isInCombat = wasInCombatBeforeTimeline;
        isInTimeline = false;
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

    public void PlayVoice(int index, float volume = 1f) => PlayVoice(voiceEffects[index], volume);

    public void PlayVoice(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;

        // Crée une nouvelle source audio à partir du modèle existant
        AudioSource tempSource = Instantiate(voiceSource, transform);
        tempSource.playOnAwake = false;
        tempSource.clip = clip;

        // Application du volume global des voix, du volume personnalisé et de la normalisation
        tempSource.volume = voiceVolume * volume * GetNormalizationFactor(clip);

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

        // Arrête toute atténuation précédente
        if (warningRoutine != null)
            StopCoroutine(warningRoutine);

        // Lance la coroutine gérant l'atténuation temporaire
        warningRoutine = StartCoroutine(WarningCoroutine(clip, factor));
    }

    /// <summary>
    /// Coroutine qui réduit temporairement le volume des autres sources pendant
    /// la lecture d'un warning clip, puis restaure les volumes initiaux.
    /// </summary>
    private IEnumerator WarningCoroutine(AudioClip clip, float factor)
    {
        // Sauvegarde des volumes actuels
        float musicAVol = musicSourceA.volume;
        float musicBVol = musicSourceB.volume;
        float sfxVol = sfxSource.volume;
        float voiceVol = voiceSource.volume;

        // Calcul du multiplicateur (1 - pourcentage d'atténuation)
        float attenuationMultiplier = Mathf.Clamp01(1f - warningAttenuation);

        // Application de l'atténuation
        musicSourceA.volume *= attenuationMultiplier;
        musicSourceB.volume *= attenuationMultiplier;
        sfxSource.volume *= attenuationMultiplier;
        voiceSource.volume *= attenuationMultiplier;

        // Lecture du warning clip
        warningClipSource.PlayOneShot(clip, factor);

        // Attente de la fin du clip
        yield return new WaitForSeconds(clip.length);

        // Restauration des volumes
        musicSourceA.volume = musicAVol;
        musicSourceB.volume = musicBVol;
        sfxSource.volume = sfxVol;
        voiceSource.volume = voiceVol;

        warningRoutine = null;
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
