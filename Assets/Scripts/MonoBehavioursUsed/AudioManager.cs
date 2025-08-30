using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public AudioClip[] musicTracks;
    public AudioClip[] soundEffects;
    public AudioClip[] voiceEffects;

    [Header("Audio Sources")]
    // Tableaux de sources permettant de jouer plusieurs pistes simultanément
    public AudioSource[] musicSources = new AudioSource[3];
    public AudioSource[] sfxSources = new AudioSource[3];
    public AudioSource[] voiceSources = new AudioSource[3];
    // Source dédiée aux sons d'avertissement
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

    // Indices des pistes musicales utilisées pour la lecture/crossfade
    private int currentMusicIndex;
    private int nextMusicIndex;
    private AudioSource CurrentMusicSource => musicSources[currentMusicIndex];
    private AudioSource NextMusicSource => musicSources[nextMusicIndex];

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
        // Configuration des trois pistes musicales
        for (int i = 0; i < musicSources.Length; i++)
        {
            if (musicSources[i] == null)
            {
                // Création automatique si la source n'est pas assignée dans le prefab
                GameObject go = new GameObject($"AudioSource_Music_{i + 1}");
                go.transform.SetParent(transform);
                musicSources[i] = go.AddComponent<AudioSource>();
            }
            musicSources[i].loop = true;
        }

        // Les deux premiers indices servent au crossfade, le troisième reste disponible
        currentMusicIndex = 0;
        nextMusicIndex = 1;

        // Configuration des sources d'effets sonores
        for (int i = 0; i < sfxSources.Length; i++)
        {
            if (sfxSources[i] == null)
            {
                GameObject go = new GameObject($"AudioSource_Sfx_{i + 1}");
                go.transform.SetParent(transform);
                sfxSources[i] = go.AddComponent<AudioSource>();
            }
            sfxSources[i].playOnAwake = false;
        }

        // Configuration des sources de voix
        for (int i = 0; i < voiceSources.Length; i++)
        {
            if (voiceSources[i] == null)
            {
                GameObject go = new GameObject($"AudioSource_Voice_{i + 1}");
                go.transform.SetParent(transform);
                voiceSources[i] = go.AddComponent<AudioSource>();
            }
            voiceSources[i].playOnAwake = false;
        }

        // Source dédiée aux avertissements
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
        // Applique le volume sur toutes les pistes musicales
        foreach (var source in musicSources)
        {
            if (source == null) continue;
            float factor = GetNormalizationFactor(source.clip);
            source.volume = musicVolume * factor;
        }
        UpdateWarningVolume();
    }

    public void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        foreach (var source in sfxSources)
        {
            if (source != null)
                source.volume = sfxVolume;
        }
    }

    public void SetVoiceVolume(float value)
    {
        voiceVolume = Mathf.Clamp01(value);
        foreach (var source in voiceSources)
        {
            if (source != null)
                source.volume = voiceVolume;
        }
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
        if (!isInCombat && CurrentMusicSource.clip != null)
        {
            explorationPlaybackPositions[CurrentMusicSource.clip] = CurrentMusicSource.time;
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

        lastExplorationClip = CurrentMusicSource.clip;
        lastExplorationTime = CurrentMusicSource.time;
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

        CurrentMusicSource.Stop();

        // Échange des indices des sources musicales
        int temp = currentMusicIndex;
        currentMusicIndex = nextMusicIndex;
        nextMusicIndex = temp;

        AudioSource source = CurrentMusicSource;
        source.clip = newClip;
        source.time = 0f;
        source.volume = musicVolume * GetNormalizationFactor(newClip);
        source.Play();
    }

    private IEnumerator CrossfadeMusic(AudioClip newClip, float startTime)
    {
        AudioSource fromSource = CurrentMusicSource;
        float fromFactor = GetNormalizationFactor(fromSource.clip);

        // Choix de la prochaine source pour le crossfade
        int toIndex = (currentMusicIndex + 1) % musicSources.Length;
        AudioSource toSource = musicSources[toIndex];

        toSource.clip = newClip;
        toSource.time = startTime;
        float toFactor = GetNormalizationFactor(newClip);
        toSource.volume = 0f;
        toSource.Play();

        currentMusicIndex = toIndex;
        nextMusicIndex = (toIndex + 1) % musicSources.Length;

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

    /// <summary>
    /// Renvoie une source disponible dans le tableau fourni.
    /// </summary>
    private AudioSource GetAvailableSource(AudioSource[] sources)
    {
        foreach (var src in sources)
        {
            if (src != null && !src.isPlaying)
                return src;
        }
        // Si toutes les sources sont occupées, on réutilise la première
        return sources[0];
    }

    public void PlaySfx(int index)
    {
        AudioClip clip = soundEffects[index];
        float factor = GetNormalizationFactor(clip);
        AudioSource src = GetAvailableSource(sfxSources);
        src.PlayOneShot(clip, factor);
    }

    public void PlayVoice(int index, float volume = 1f) => PlayVoice(voiceEffects[index], volume);

    public void PlayVoice(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;

        AudioSource src = GetAvailableSource(voiceSources);
        src.clip = clip;
        // Application du volume global des voix, du volume personnalisé et de la normalisation
        src.volume = voiceVolume * volume * GetNormalizationFactor(clip);
        src.Play();
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
        float[] musicVols = new float[musicSources.Length];
        for (int i = 0; i < musicSources.Length; i++) musicVols[i] = musicSources[i].volume;
        float[] sfxVols = new float[sfxSources.Length];
        for (int i = 0; i < sfxSources.Length; i++) sfxVols[i] = sfxSources[i].volume;
        float[] voiceVols = new float[voiceSources.Length];
        for (int i = 0; i < voiceSources.Length; i++) voiceVols[i] = voiceSources[i].volume;

        // Calcul du multiplicateur (1 - pourcentage d'atténuation)
        float attenuationMultiplier = Mathf.Clamp01(1f - warningAttenuation);

        // Application de l'atténuation
        foreach (var src in musicSources) src.volume *= attenuationMultiplier;
        foreach (var src in sfxSources) src.volume *= attenuationMultiplier;
        foreach (var src in voiceSources) src.volume *= attenuationMultiplier;

        // Lecture du warning clip
        warningClipSource.PlayOneShot(clip, factor);

        // Attente de la fin du clip
        yield return new WaitForSeconds(clip.length);

        // Restauration des volumes
        for (int i = 0; i < musicSources.Length; i++) musicSources[i].volume = musicVols[i];
        for (int i = 0; i < sfxSources.Length; i++) sfxSources[i].volume = sfxVols[i];
        for (int i = 0; i < voiceSources.Length; i++) voiceSources[i].volume = voiceVols[i];

        warningRoutine = null;
    }

    public void PlaySound(AudioClip clip)
    {
        float factor = GetNormalizationFactor(clip);
        AudioSource src = GetAvailableSource(sfxSources);
        src.PlayOneShot(clip, factor);
    }

    public void PlayTempSfx(AudioClip clip)
    {
        if (clip == null) return;

        AudioSource src = GetAvailableSource(sfxSources);
        src.clip = clip;
        src.volume = sfxVolume * GetNormalizationFactor(clip);
        src.Play();
    }

    #endregion
}
