using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public AudioClipSO[] musicTracks;
    public AudioClipSO[] soundEffects;
    public AudioClipSO[] voiceEffects;

    // Règles sonores par défaut imposées par la documentation de production.
    private const float DefaultVolume = 0.8f;
    private const bool DefaultLoop = false;

    /// <summary>
    /// Récupère le clip Unity encapsulé dans le ScriptableObject.
    /// </summary>
    private static AudioClip ResolveClip(AudioClipSO clipAsset) => clipAsset != null ? clipAsset.Clip : null;

    /// <summary>
    /// Calcule un volume effectif en combinant la valeur du ScriptableObject et un éventuel fallback.
    /// </summary>
    private static float ResolveVolume(AudioClipSO clipAsset, float fallback = DefaultVolume)
    {
        return clipAsset != null ? clipAsset.Volume : Mathf.Clamp01(fallback);
    }

    /// <summary>
    /// Indique si le clip doit être lu en boucle selon les données configurées.
    /// </summary>
    private static bool ResolveLoop(AudioClipSO clipAsset, bool fallback = DefaultLoop)
    {
        return clipAsset != null ? clipAsset.Loop : fallback;
    }

    [Header("Audio Sources")]
    // Tableaux de sources permettant de jouer plusieurs pistes simultanément
    public AudioSource[] musicSources = new AudioSource[3];
    public AudioSource[] sfxSources = new AudioSource[3];
    public AudioSource[] voiceSources = new AudioSource[3];
    // Source modèle facultative dédiée aux sons d'avertissement.
    // ⚠️ Aucun son ne doit être joué directement via cette référence :
    // elle sert uniquement de gabarit pour copier les réglages (mixer group,
    // spatialisation, etc.) vers une AudioSource éphémère créée à la volée.
    public AudioSource warningClipTemplate;

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
    // Coroutine dédiée aux fondus de volume lorsque l'on entre ou sort d'une timeline
    // ne possédant pas de musique propre. Permet d'interrompre proprement un fondu
    // si une autre timeline démarre immédiatement après la précédente.
    private Coroutine timelineFadeRoutine;
    // Coroutine gérant l'atténuation temporaire lors de la lecture d'un warning
    private Coroutine warningRoutine;
    private AudioClipSO lastExplorationClip;
    private float lastExplorationTime;

    private bool isInCombat = false;

    // 📈 Indique si une timeline est en cours.
    // Permet de suspendre la musique actuelle et de la reprendre à la fin de la cinématique.
    private bool isInTimeline = false;

    // 📊 Sauvegarde l'état avant l'entrée en timeline.
    private bool wasInCombatBeforeTimeline = false; // → vrai si l'on était en combat avant la cinématique
    private AudioClipSO clipBeforeTimeline;         // → musique en cours avant la timeline
    private float timeBeforeTimeline;               // → position de lecture de cette musique

    private readonly Dictionary<AudioClipSO, float> explorationPlaybackPositions = new Dictionary<AudioClipSO, float>();
    private AudioClipSO currentMusicAsset;
    private Dictionary<AudioClip, float> normalizationCache = new Dictionary<AudioClip, float>();

    /// <summary>
    /// Classe interne dédiée au suivi des AudioSource créées dynamiquement pour les SFX/voix.
    /// Elle nous permet de recalculer facilement les volumes lorsque les réglages globaux changent
    /// (volume SFX/voix, atténuation lors d'un avertissement, etc.).
    /// </summary>
    private class DynamicAudioHandle
    {
        public AudioSource Source;
        public float BaseFactor;
    }

    // Listes des instances dynamiques actuellement en cours de lecture.
    private readonly List<DynamicAudioHandle> activeSfxHandles = new List<DynamicAudioHandle>();
    private readonly List<DynamicAudioHandle> activeVoiceHandles = new List<DynamicAudioHandle>();

    // Compteur pour nommer clairement les AudioSource engendrées dynamiquement.
    private int dynamicSourceCounter = 0;

    // Volume de base utilisé pour les avertissements (dépend du volume musique).
    private float warningBaseVolume = 1f;

    // Facteur appliqué aux SFX/voix lorsque la lecture d'un avertissement est en cours.
    private float currentWarningAttenuation = 1f;

    // Référence vers la source actuellement utilisée pour jouer un avertissement.
    private AudioSource currentWarningSource;

    // Sauvegardes temporaires des volumes pendant la lecture d'un avertissement.
    private float[] cachedWarningMusicVolumes;
    private float[] cachedWarningSfxVolumes;
    private float[] cachedWarningVoiceVolumes;

    /// <summary>
    /// Met à jour le volume de la source d'avertissement en fonction de la musique.
    /// </summary>
    private void UpdateWarningVolume()
    {
        // Volume deux fois plus élevé que celui des sources de musique
        // → utilisé au moment de générer la prochaine source d'avertissement éphémère.
        warningBaseVolume = musicVolume * 2f;
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
        if (warningClipTemplate != null)
        {
            // Sécurise l'éventuel gabarit renseigné dans l'inspecteur pour éviter
            // toute lecture intempestive lors de l'initialisation.
            warningClipTemplate.playOnAwake = false;
        }

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
        RefreshDynamicVolumes(activeSfxHandles, sfxVolume);
    }

    public void SetVoiceVolume(float value)
    {
        voiceVolume = Mathf.Clamp01(value);
        RefreshDynamicVolumes(activeVoiceHandles, voiceVolume);
    }

    #endregion

    #region 🎵 Musique : Transitions

    public void PlayExplorationMusic(AudioClipSO newExplorationClip)
    {
        AudioClip clip = ResolveClip(newExplorationClip);
        if (clip == null)
            return;

        if (isInCombat || isInTimeline || currentMusicAsset == newExplorationClip)
            return;

        lastExplorationClip = newExplorationClip;

        float resumeTime = explorationPlaybackPositions.TryGetValue(newExplorationClip, out float savedTime)
            ? savedTime
            : 0f;

        StartCrossfade(newExplorationClip, resumeTime);
        isInCombat = false;
    }

    public void TransitionToNewExplorationZone(AudioClipSO newExplorationClip)
    {
        AudioClip clip = ResolveClip(newExplorationClip);
        if (clip == null)
            return;

        if (isInCombat || isInTimeline || currentMusicAsset == newExplorationClip)
            return;

        if (!isInCombat && currentMusicAsset != null && CurrentMusicSource.clip != null)
        {
            explorationPlaybackPositions[currentMusicAsset] = CurrentMusicSource.time;
        }

        lastExplorationClip = newExplorationClip;

        float resumeTime = explorationPlaybackPositions.TryGetValue(newExplorationClip, out float savedTime)
            ? savedTime
            : 0f;

        StartCrossfade(newExplorationClip, resumeTime);
    }

    public void TransitionToCombat(AudioClipSO combatClip)
    {
        AudioClip clip = ResolveClip(combatClip);
        if (clip == null)
            return;

        if (isInCombat)
            return;

        if (isInTimeline)
        {
            isInTimeline = false;

            if (!wasInCombatBeforeTimeline)
            {
                lastExplorationClip = clipBeforeTimeline;
                lastExplorationTime = timeBeforeTimeline;
            }

            if (timelineFadeRoutine != null)
            {
                StopCoroutine(timelineFadeRoutine);
                timelineFadeRoutine = null;
            }

            CurrentMusicSource.UnPause();
        }
        else if (currentMusicAsset != null)
        {
            lastExplorationClip = currentMusicAsset;
            lastExplorationTime = CurrentMusicSource.time;
        }

        isInCombat = true;
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
    public void TransitionToTimeline(AudioClipSO timelineClip)
    {
        if (isInTimeline)
            return;

        wasInCombatBeforeTimeline = isInCombat;
        clipBeforeTimeline = currentMusicAsset;
        timeBeforeTimeline = CurrentMusicSource.time;

        isInTimeline = true;
        isInCombat = false;

        if (ResolveClip(timelineClip) != null)
        {
            StartCrossfade(timelineClip, 0f);
        }
        else
        {
            if (timelineFadeRoutine != null)
                StopCoroutine(timelineFadeRoutine);
            timelineFadeRoutine = StartCoroutine(FadeOutCurrentMusic());
        }
    }

    /// <summary>
    /// Restaure la musique précédente après une timeline.
    /// </summary>
    public void ReturnFromTimeline()
    {
        if (!isInTimeline)
            return;

        isInCombat = wasInCombatBeforeTimeline;
        isInTimeline = false;

        if (clipBeforeTimeline == null)
            return;

        if (currentMusicAsset != clipBeforeTimeline)
        {
            StartCrossfade(clipBeforeTimeline, timeBeforeTimeline);
        }
        else
        {
            if (timelineFadeRoutine != null)
                StopCoroutine(timelineFadeRoutine);
            CurrentMusicSource.UnPause();
            timelineFadeRoutine = StartCoroutine(FadeInCurrentMusic());
        }
    }

    /// <summary>
    /// Réduit progressivement le volume de la musique actuelle puis la met en pause.
    /// Utilisé lors d'une timeline sans musique dédiée.
    /// </summary>
    private IEnumerator FadeOutCurrentMusic()
    {
        AudioSource source = CurrentMusicSource;
        float startVolume = source.volume;
        float t = 0f;

        // ⏱️ Utilise le temps non-scalé afin que le fondu reste fonctionnel
        // même lorsque la Timeline met le jeu en pause via un timeScale nul.
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, t / fadeDuration);
            yield return null;
        }

        source.volume = 0f;
        source.Pause();
        timelineFadeRoutine = null;
    }

    /// <summary>
    /// Restaure progressivement le volume de la musique après une timeline.
    /// </summary>
    private IEnumerator FadeInCurrentMusic()
    {
        AudioSource source = CurrentMusicSource;
        float targetVolume = musicVolume * GetNormalizationFactor(source.clip);
        source.volume = 0f;
        float t = 0f;

        // ⏱️ Utilise Time.unscaledDeltaTime pour que le fondu soit insensible
        // aux ralentis ou arrêts complets provoqués par les timelines.
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(0f, targetVolume, t / fadeDuration);
            yield return null;
        }

        source.volume = targetVolume;
        timelineFadeRoutine = null;
    }

    private void StartCrossfade(AudioClipSO newClip, float startTime)
    {
        currentMusicAsset = newClip;
        AnnounceMusic(newClip);

        if (crossfadeRoutine != null)
            StopCoroutine(crossfadeRoutine);

        crossfadeRoutine = StartCoroutine(CrossfadeMusic(newClip, startTime));
    }

    private void SwitchImmediately(AudioClipSO newClip)
    {
        currentMusicAsset = newClip;
        AnnounceMusic(newClip);

        if (crossfadeRoutine != null)
            StopCoroutine(crossfadeRoutine);

        CurrentMusicSource.Stop();

        int temp = currentMusicIndex;
        currentMusicIndex = nextMusicIndex;
        nextMusicIndex = temp;

        AudioSource source = CurrentMusicSource;
        AudioClip clip = ResolveClip(newClip);

        if (clip == null)
        {
            source.clip = null;
            source.loop = ResolveLoop(newClip);
            source.volume = 0f;
            return;
        }

        source.clip = clip;
        source.loop = ResolveLoop(newClip);
        source.time = 0f;

        float normalization = GetNormalizationFactor(clip);
        float volumeFactor = ResolveVolume(newClip, DefaultVolume);
        source.volume = musicVolume * normalization * volumeFactor;
        source.Play();
    }

    /// <summary>
    /// Met à jour la boîte d'information musicale en bas de l'écran.
    /// </summary>
    private void AnnounceMusic(AudioClipSO clip)
    {
        if (clip == null)
            return;

        MusicInfoBoxUI.Instance.Show(clip);
    }

    private IEnumerator CrossfadeMusic(AudioClipSO newClip, float startTime)
    {
        AudioSource fromSource = CurrentMusicSource;
        float fromInitialVolume = fromSource.volume;
        float fromFactor = GetNormalizationFactor(fromSource.clip);
        float baseVolume = Mathf.Max(0.0001f, musicVolume * fromFactor);
        float previousVolumeFactor = baseVolume > 0f ? Mathf.Clamp01(fromInitialVolume / baseVolume) : 1f;

        int toIndex = (currentMusicIndex + 1) % musicSources.Length;
        AudioSource toSource = musicSources[toIndex];

        AudioClip clip = ResolveClip(newClip);
        if (clip == null)
        {
            float t = 0f;
            // 🎵 Même logique : on s'appuie sur le temps non-scalé pour que
            // le fondu reste actif si la timeline fige le gameplay.
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float progress = t / fadeDuration;
                fromSource.volume = Mathf.Lerp(fromInitialVolume, 0f, progress);
                yield return null;
            }

            fromSource.Stop();
            fromSource.volume = musicVolume * fromFactor * previousVolumeFactor;
            crossfadeRoutine = null;
            yield break;
        }

        toSource.clip = clip;
        toSource.loop = ResolveLoop(newClip);
        float clampedStart = Mathf.Clamp(startTime, 0f, clip.length > 0f ? clip.length : 0f);
        toSource.time = clampedStart;
        float toFactor = GetNormalizationFactor(clip);
        float toVolumeFactor = ResolveVolume(newClip, DefaultVolume);
        float targetVolume = musicVolume * toFactor * toVolumeFactor;
        toSource.volume = 0f;
        toSource.Play();

        currentMusicIndex = toIndex;
        nextMusicIndex = (toIndex + 1) % musicSources.Length;

        float tCrossfade = 0f;

        // 🎚️ Crossfade piloté par le temps non-scalé afin de rester fluide
        // pendant les cinématiques qui manipulent le timeScale global.
        while (tCrossfade < fadeDuration)
        {
            tCrossfade += Time.unscaledDeltaTime;
            float progress = tCrossfade / fadeDuration;

            toSource.volume = Mathf.Lerp(0f, targetVolume, progress);
            fromSource.volume = Mathf.Lerp(fromInitialVolume, 0f, progress);
            yield return null;
        }

        fromSource.Stop();
        fromSource.volume = musicVolume * fromFactor * previousVolumeFactor;
        toSource.volume = targetVolume;

        crossfadeRoutine = null;
    }

    #endregion

    #region 🔊 Effets

    /// <summary>
    /// Récupère une AudioSource modèle dans le tableau fourni. On privilégie la première
    /// non nulle afin de conserver les réglages renseignés dans l'inspecteur (mixer group, etc.).
    /// </summary>
    private AudioSource GetTemplateSource(AudioSource[] sources)
    {
        foreach (var src in sources)
        {
            if (src != null)
                return src;
        }

        return null;
    }

    /// <summary>
    /// Copie les paramètres pertinents d'une AudioSource modèle vers une nouvelle instance.
    /// On évite volontairement de recopier "volume", "loop" ou "playOnAwake" qui sont
    /// définis dynamiquement pour chaque son joué.
    /// </summary>
    private static void CopySourceSettings(AudioSource template, AudioSource target)
    {
        if (template == null || target == null)
            return;

        target.outputAudioMixerGroup = template.outputAudioMixerGroup;
        target.priority = template.priority;
        target.pitch = template.pitch;
        target.panStereo = template.panStereo;
        target.spatialBlend = template.spatialBlend;
        target.reverbZoneMix = template.reverbZoneMix;
        target.dopplerLevel = template.dopplerLevel;
        target.spread = template.spread;
        target.rolloffMode = template.rolloffMode;
        target.minDistance = template.minDistance;
        target.maxDistance = template.maxDistance;
        target.velocityUpdateMode = template.velocityUpdateMode;
        target.ignoreListenerVolume = template.ignoreListenerVolume;
        target.ignoreListenerPause = template.ignoreListenerPause;
    }

    /// <summary>
    /// Crée une nouvelle AudioSource enfant de l'AudioManager, entièrement gérée par ce dernier.
    /// Note : cette méthode sert de colonne vertébrale à toutes les sources éphémères
    /// (warnings, SFX/voix temporaires, etc.).
    /// </summary>
    private AudioSource CreateManagedSource(string category, AudioSource template, bool loop = false)
    {
        GameObject go = new GameObject($"AudioSource_{category}_{++dynamicSourceCounter}");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;

        CopySourceSettings(template, source);

        return source;
    }

    /// <summary>
    /// Applique un volume cohérent à une source gérée dynamiquement.
    /// </summary>
    private void ApplyVolume(DynamicAudioHandle handle, float globalVolume)
    {
        if (handle?.Source == null)
            return;

        float finalVolume = Mathf.Clamp01(globalVolume * handle.BaseFactor * currentWarningAttenuation);
        handle.Source.volume = finalVolume;
    }

    /// <summary>
    /// Réactualise le volume de toutes les sources dynamiques d'une catégorie.
    /// </summary>
    private void RefreshDynamicVolumes(List<DynamicAudioHandle> handles, float globalVolume)
    {
        for (int i = handles.Count - 1; i >= 0; i--)
        {
            DynamicAudioHandle handle = handles[i];
            if (handle?.Source == null)
            {
                // Nettoyage défensif : si la source a été détruite ailleurs, on évite les références mortes.
                handles.RemoveAt(i);
                continue;
            }

            ApplyVolume(handle, globalVolume);
        }
    }

    /// <summary>
    /// Détruit une AudioSource une fois le clip terminé (pour les sons non bouclés).
    /// </summary>
    private IEnumerator DestroySourceWhenFinished(AudioSource source, List<DynamicAudioHandle> registry = null, DynamicAudioHandle handle = null)
    {
        if (source == null || source.loop)
            yield break;

        // Attente minimale d'une frame pour s'assurer que la lecture a bien démarré.
        yield return null;

        // Patiente jusqu'à la fin effective du clip afin de respecter la durée
        // complète avant destruction de l'AudioSource temporaire.
        while (source != null && source.isPlaying)
        {
            yield return null;
        }

        if (registry != null && handle != null)
            registry.Remove(handle);

        if (source != null)
            Destroy(source.gameObject);
    }

    /// <summary>
    /// Interrompt et détruit la source utilisée pour un avertissement si elle est encore active.
    /// </summary>
    private void CleanupWarningSource()
    {
        if (currentWarningSource == null)
            return;

        // Sécurise la destruction même si le clip est toujours en cours de lecture
        // (cas d'un nouvel avertissement qui arrive avant la fin du précédent).
        if (currentWarningSource.isPlaying)
            currentWarningSource.Stop();

        Destroy(currentWarningSource.gameObject);
        currentWarningSource = null;
    }

    /// <summary>
    /// Restaure les volumes sauvegardés lors d'un avertissement et remet à zéro
    /// les multiplicateurs appliqués sur les sources dynamiques.
    /// </summary>
    private void RestoreWarningVolumes()
    {
        if (cachedWarningMusicVolumes != null)
        {
            for (int i = 0; i < musicSources.Length && i < cachedWarningMusicVolumes.Length; i++)
            {
                if (musicSources[i] != null)
                    musicSources[i].volume = cachedWarningMusicVolumes[i];
            }
        }

        if (cachedWarningSfxVolumes != null)
        {
            for (int i = 0; i < sfxSources.Length && i < cachedWarningSfxVolumes.Length; i++)
            {
                if (sfxSources[i] != null)
                    sfxSources[i].volume = cachedWarningSfxVolumes[i];
            }
        }

        if (cachedWarningVoiceVolumes != null)
        {
            for (int i = 0; i < voiceSources.Length && i < cachedWarningVoiceVolumes.Length; i++)
            {
                if (voiceSources[i] != null)
                    voiceSources[i].volume = cachedWarningVoiceVolumes[i];
            }
        }

        cachedWarningMusicVolumes = null;
        cachedWarningSfxVolumes = null;
        cachedWarningVoiceVolumes = null;

        currentWarningAttenuation = 1f;
        RefreshDynamicVolumes(activeSfxHandles, sfxVolume);
        RefreshDynamicVolumes(activeVoiceHandles, voiceVolume);
    }

    public void PlaySfx(int index)
    {
        if (soundEffects == null || index < 0 || index >= soundEffects.Length)
            return;

        PlaySfx(soundEffects[index]);
    }

    /// <summary>
    /// Lecture simplifiée d'un effet sonore configuré via un <see cref="AudioClipSO"/>.
    /// </summary>
    public void PlaySfx(AudioClipSO clipAsset)
    {
        if (clipAsset == null)
            return;

        PlaySfx(ResolveClip(clipAsset), ResolveVolume(clipAsset), ResolveLoop(clipAsset));
    }

    /// <summary>
    /// Joue un effet sonore arbitraire en réutilisant les paramètres globaux du gestionnaire.
    /// Cette surcharge simplifie l'utilisation d'effets dédiés (menus, dash, etc.) sans
    /// imposer de réserver un slot dans <see cref="soundEffects"/>.
    /// </summary>
    /// <param name="clip">Clip à jouer immédiatement.</param>
    /// <param name="volume">Facteur multiplicatif optionnel (1 = volume par défaut).</param>
    /// <param name="loop">Lecture en boucle ?</param>
    public void PlaySfx(AudioClip clip, float volume = DefaultVolume, bool loop = DefaultLoop)
    {
        if (clip == null)
            return; // Rien à jouer : on quitte proprement.

        AudioSource template = GetTemplateSource(sfxSources);
        AudioSource src = CreateManagedSource("SFX", template, loop);

        DynamicAudioHandle handle = new DynamicAudioHandle
        {
            Source = src,
            BaseFactor = Mathf.Clamp01(volume) * GetNormalizationFactor(clip)
        };
        activeSfxHandles.Add(handle);

        src.clip = clip;
        ApplyVolume(handle, sfxVolume);
        src.Play();

        if (!loop)
            StartCoroutine(DestroySourceWhenFinished(src, activeSfxHandles, handle));
    }

    public void PlayVoice(int index, float volume = DefaultVolume)
    {
        if (voiceEffects == null || index < 0 || index >= voiceEffects.Length)
            return;

        PlayVoice(voiceEffects[index], volume);
    }

    /// <summary>
    /// Lecture d'une voix via un <see cref="AudioClipSO"/>.
    /// </summary>
    public void PlayVoice(AudioClipSO clipAsset, float volumeMultiplier = 1f)
    {
        if (clipAsset == null)
            return;

        float baseVolume = ResolveVolume(clipAsset);
        float finalVolume = Mathf.Clamp01(baseVolume * Mathf.Clamp01(volumeMultiplier));
        PlayVoice(ResolveClip(clipAsset), finalVolume, ResolveLoop(clipAsset));
    }

    public void PlayVoice(AudioClip clip, float volume = DefaultVolume, bool loop = DefaultLoop)
    {
        if (clip == null) return;

        AudioSource template = GetTemplateSource(voiceSources);
        AudioSource src = CreateManagedSource("Voice", template, loop);

        DynamicAudioHandle handle = new DynamicAudioHandle
        {
            Source = src,
            BaseFactor = Mathf.Clamp01(volume) * GetNormalizationFactor(clip)
        };
        activeVoiceHandles.Add(handle);

        src.clip = clip;
        ApplyVolume(handle, voiceVolume);
        src.Play();

        if (!loop)
            StartCoroutine(DestroySourceWhenFinished(src, activeVoiceHandles, handle));
    }

    /// <summary>
    /// Joue un clip d'avertissement en utilisant la source dédiée.
    /// </summary>
    public void PlayWarningClip(AudioClipSO clipAsset)
    {
        AudioClip clip = ResolveClip(clipAsset);
        if (clip == null) return;

        float normalization = GetNormalizationFactor(clip);
        float clipVolume = ResolveVolume(clipAsset, DefaultVolume);

        if (warningRoutine != null)
        {
            StopCoroutine(warningRoutine);
            RestoreWarningVolumes();
            // Comme la source d'avertissement est désormais éphémère, on la supprime
            // immédiatement pour éviter qu'elle ne persiste en parallèle du nouveau clip.
            CleanupWarningSource();
            warningRoutine = null;
        }

        warningRoutine = StartCoroutine(WarningCoroutine(clipAsset, clip, normalization, clipVolume));
    }

    /// <summary>
    /// Coroutine qui réduit temporairement le volume des autres sources pendant
    /// la lecture d'un warning clip, puis restaure les volumes initiaux.
    /// </summary>
    private IEnumerator WarningCoroutine(AudioClipSO clipAsset, AudioClip clip, float normalizationFactor, float volumeFactor)
    {
        // Sauvegarde des volumes actuels
        float[] musicVols = new float[musicSources.Length];
        for (int i = 0; i < musicSources.Length; i++) musicVols[i] = musicSources[i].volume;
        float[] sfxVols = new float[sfxSources.Length];
        for (int i = 0; i < sfxSources.Length; i++) sfxVols[i] = sfxSources[i].volume;
        float[] voiceVols = new float[voiceSources.Length];
        for (int i = 0; i < voiceSources.Length; i++) voiceVols[i] = voiceSources[i].volume;

        cachedWarningMusicVolumes = musicVols;
        cachedWarningSfxVolumes = sfxVols;
        cachedWarningVoiceVolumes = voiceVols;

        // Calcul du multiplicateur (1 - pourcentage d'atténuation)
        float attenuationMultiplier = Mathf.Clamp01(1f - warningAttenuation);

        // Application de l'atténuation
        foreach (var src in musicSources) src.volume *= attenuationMultiplier;
        foreach (var src in sfxSources) src.volume *= attenuationMultiplier;
        foreach (var src in voiceSources) src.volume *= attenuationMultiplier;

        // Les nouvelles sources dynamiques doivent également être prises en compte.
        currentWarningAttenuation = attenuationMultiplier;
        RefreshDynamicVolumes(activeSfxHandles, sfxVolume);
        RefreshDynamicVolumes(activeVoiceHandles, voiceVolume);

        // Lecture du warning clip via une AudioSource dédiée créée pour l'occasion.
        // On privilégie le gabarit dédié s'il a été assigné, sinon on recycle
        // les réglages d'un effet sonore classique pour rester cohérent.
        AudioSource warningTemplate = warningClipTemplate != null ? warningClipTemplate : GetTemplateSource(sfxSources);

        // Conformément aux directives de production, la source servant réellement à la
        // lecture du warning est créée dynamiquement et ne vit que le temps du clip.
        // Les warnings étant par nature ponctuels, on force l'absence de boucle pour
        // garantir la destruction automatique de la source éphémère.
        AudioSource warningSource = CreateManagedSource("Warning", warningTemplate, false);
        warningSource.clip = clip;
        warningSource.volume = Mathf.Clamp01(warningBaseVolume * normalizationFactor * volumeFactor);
        warningSource.Play();
        currentWarningSource = warningSource;

        // Attente de la fin du clip puis destruction automatique de la source,
        // garantissant que la source n'existe que pendant la lecture du warning.
        yield return DestroySourceWhenFinished(warningSource);
        currentWarningSource = null;

        RestoreWarningVolumes();
        warningRoutine = null;
    }

    public void PlaySound(AudioClip clip)
    {
        PlaySfx(clip);
    }

    public void PlaySound(AudioClipSO clipAsset)
    {
        PlaySfx(clipAsset);
    }

    public void PlayTempSfx(AudioClip clip)
    {
        PlaySfx(clip);
    }

    public void PlayTempSfx(AudioClipSO clipAsset)
    {
        PlaySfx(clipAsset);
    }

    #endregion
}
