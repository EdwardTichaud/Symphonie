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
    // Coroutine dédiée aux fondus de volume lorsque l'on entre ou sort d'une timeline
    // ne possédant pas de musique propre. Permet d'interrompre proprement un fondu
    // si une autre timeline démarre immédiatement après la précédente.
    private Coroutine timelineFadeRoutine;
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
        warningBaseVolume = musicVolume * 2f;
        if (warningClipSource != null)
        {
            // La source de référence ne joue plus directement de son :
            // elle sert uniquement de gabarit pour les nouvelles AudioSource.
            warningClipSource.volume = warningBaseVolume;
        }
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
        RefreshDynamicVolumes(activeSfxHandles, sfxVolume);
    }

    public void SetVoiceVolume(float value)
    {
        voiceVolume = Mathf.Clamp01(value);
        RefreshDynamicVolumes(activeVoiceHandles, voiceVolume);
    }

    #endregion

    #region 🎵 Musique : Transitions

    public void PlayExplorationMusic(AudioClip newExplorationClip)
    {
        // Empêche tout changement si une timeline ou un combat est en cours
        // Utilise la propriété "CurrentMusicSource" pour récupérer la source active
        if (isInCombat || isInTimeline || newExplorationClip == CurrentMusicSource.clip)
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
        // On compare avec "CurrentMusicSource" afin d'éviter les erreurs de variable inexistante
        if (isInCombat || isInTimeline || newExplorationClip == CurrentMusicSource.clip)
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
        // 🚫 Ignore si l'on est déjà en combat
        if (isInCombat)
            return;

        if (isInTimeline)
        {
            // ✅ Un combat est lancé depuis une timeline
            //    → on met fin à la timeline sans relancer l'exploration
            isInTimeline = false;

            // Si la timeline provenait de l'exploration, on mémorise la musique à reprendre
            if (!wasInCombatBeforeTimeline)
            {
                lastExplorationClip = clipBeforeTimeline;
                lastExplorationTime = timeBeforeTimeline;
            }

            // Stoppe tout fondu éventuel lancé par la timeline
            if (timelineFadeRoutine != null)
            {
                StopCoroutine(timelineFadeRoutine);
                timelineFadeRoutine = null;
            }

            // La source active peut avoir été mise en pause : on la remet en lecture
            CurrentMusicSource.UnPause();
        }
        else
        {
            // Combat lancé hors timeline : on sauvegarde la musique d'exploration actuelle
            lastExplorationClip = CurrentMusicSource.clip;
            lastExplorationTime = CurrentMusicSource.time;
        }

        // On passe en mode combat puis on bascule immédiatement sur le thème approprié
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
    public void TransitionToTimeline(AudioClip timelineClip)
    {
        // Évite toute action si une timeline est déjà en cours
        if (isInTimeline)
            return;

        // Sauvegarde le contexte musical actuel avant d'entrer en timeline
        // "CurrentMusicSource" renvoie la source de musique active
        wasInCombatBeforeTimeline = isInCombat;
        clipBeforeTimeline = CurrentMusicSource.clip;
        timeBeforeTimeline = CurrentMusicSource.time;

        // Les timelines ne sont ni exploration ni combat
        isInTimeline = true;
        isInCombat = false;

        // Si un clip de timeline est fourni, on réalise un crossfade classique
        if (timelineClip != null)
        {
            StartCrossfade(timelineClip, 0f);
        }
        else
        {
            // Sinon, on coupe progressivement la musique actuelle pour laisser place
            // aux sons propres de la timeline (voix, bruitages...).
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

        // Rétablit l'état initial (combat ou exploration)
        isInCombat = wasInCombatBeforeTimeline;
        isInTimeline = false;

        if (clipBeforeTimeline == null)
            return;

        // Si un clip spécifique était joué pendant la timeline, on le remplace
        // par la musique précédente via un crossfade.
        if (CurrentMusicSource.clip != clipBeforeTimeline)
        {
            StartCrossfade(clipBeforeTimeline, timeBeforeTimeline);
        }
        else
        {
            // Aucun clip de timeline : la musique avait été simplement mise en pause.
            // On la relance et on remonte le volume progressivement.
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

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
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

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(0f, targetVolume, t / fadeDuration);
            yield return null;
        }

        source.volume = targetVolume;
        timelineFadeRoutine = null;
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
        AudioClip clip = soundEffects[index];
        PlaySfx(clip);
    }

    /// <summary>
    /// Joue un effet sonore arbitraire en réutilisant les paramètres globaux du gestionnaire.
    /// Cette surcharge simplifie l'utilisation d'effets dédiés (menus, dash, etc.) sans
    /// imposer de réserver un slot dans <see cref="soundEffects"/>.
    /// </summary>
    /// <param name="clip">Clip à jouer immédiatement.</param>
    /// <param name="volume">Facteur multiplicatif optionnel (1 = volume par défaut).</param>
    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
            return; // Rien à jouer : on quitte proprement.

        AudioSource template = GetTemplateSource(sfxSources);
        AudioSource src = CreateManagedSource("SFX", template);

        // Stockage du facteur de volume permettant de recalculer la valeur lorsque
        // le volume global change ou lorsqu'un warning est actif.
        DynamicAudioHandle handle = new DynamicAudioHandle
        {
            Source = src,
            BaseFactor = Mathf.Clamp01(volume) * GetNormalizationFactor(clip)
        };
        activeSfxHandles.Add(handle);

        src.clip = clip;
        ApplyVolume(handle, sfxVolume);
        src.Play();

        StartCoroutine(DestroySourceWhenFinished(src, activeSfxHandles, handle));
    }

    public void PlayVoice(int index, float volume = 1f) => PlayVoice(voiceEffects[index], volume);

    public void PlayVoice(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;

        AudioSource template = GetTemplateSource(voiceSources);
        AudioSource src = CreateManagedSource("Voice", template);

        DynamicAudioHandle handle = new DynamicAudioHandle
        {
            Source = src,
            BaseFactor = Mathf.Clamp01(volume) * GetNormalizationFactor(clip)
        };
        activeVoiceHandles.Add(handle);

        src.clip = clip;
        ApplyVolume(handle, voiceVolume);
        src.Play();

        StartCoroutine(DestroySourceWhenFinished(src, activeVoiceHandles, handle));
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
        {
            StopCoroutine(warningRoutine);
            RestoreWarningVolumes();
            CleanupWarningSource();
            warningRoutine = null;
        }

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
        AudioSource warningTemplate = warningClipSource != null ? warningClipSource : GetTemplateSource(sfxSources);
        AudioSource warningSource = CreateManagedSource("Warning", warningTemplate);
        warningSource.clip = clip;
        warningSource.volume = Mathf.Clamp01(warningBaseVolume * factor);
        warningSource.Play();
        currentWarningSource = warningSource;

        // Attente de la fin du clip puis destruction automatique de la source.
        yield return DestroySourceWhenFinished(warningSource);
        currentWarningSource = null;

        RestoreWarningVolumes();
        warningRoutine = null;
    }

    public void PlaySound(AudioClip clip)
    {
        PlaySfx(clip);
    }

    public void PlayTempSfx(AudioClip clip)
    {
        PlaySfx(clip);
    }

    #endregion
}
