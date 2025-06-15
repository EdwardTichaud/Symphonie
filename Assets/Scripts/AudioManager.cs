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

    [Header("Fade Settings")]
    public float fadeDuration = 2f;

    public static AudioManager Instance { get; private set; }

    private AudioSource currentMusicSource;
    private AudioSource nextMusicSource;

    private Coroutine crossfadeRoutine;
    private AudioClip lastExplorationClip;
    private float lastExplorationTime;

    private bool isInCombat = false;

    private Dictionary<AudioClip, float> explorationPlaybackPositions = new Dictionary<AudioClip, float>();

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
    }

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

    public void ReturnFromCombat()
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
        currentMusicSource.volume = 1f;
        currentMusicSource.Play();
    }

    private IEnumerator CrossfadeMusic(AudioClip newClip, float startTime)
    {
        AudioSource fromSource = currentMusicSource;
        AudioSource toSource = (currentMusicSource == musicSourceA) ? musicSourceB : musicSourceA;

        toSource.clip = newClip;
        toSource.time = startTime;
        toSource.volume = 0f;
        toSource.Play();

        currentMusicSource = toSource;
        nextMusicSource = fromSource;

        float startVol = fromSource.volume;
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float progress = t / fadeDuration;

            toSource.volume = Mathf.Lerp(0f, startVol, progress);
            fromSource.volume = Mathf.Lerp(startVol, 0f, progress);
            yield return null;
        }

        fromSource.Stop();
        fromSource.volume = startVol;
        toSource.volume = startVol;

        crossfadeRoutine = null;
    }

    #endregion

    #region 🔊 Effets

    public void PlaySfx(int index) => sfxSource.PlayOneShot(soundEffects[index]);

    public void PlayVoice(int index) => voiceSource.PlayOneShot(voiceEffects[index]);

    public void PlayVoice(AudioClip clip)
    {
        voiceSource.clip = clip;
        voiceSource.Play();
    }

    public void PlaySound(AudioClip clip) => sfxSource.PlayOneShot(clip);

    #endregion
}
