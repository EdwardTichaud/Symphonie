using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PlayMusicOnStart : MonoBehaviour
{
    [Tooltip("ScriptableObject contenant le clip et ses paramètres par défaut.")]
    public AudioClipSO clipToPlay;

    void Start()
    {
        AudioSource audioSource = GetComponent<AudioSource>();
        if (clipToPlay == null || clipToPlay.Clip == null)
            return;

        if (AudioManager.Instance != null)
        {
            if (clipToPlay.type == AudioClipSO.AudioClipType.Music)
                AudioManager.Instance.PlayMusicOverride(clipToPlay);
            else
                AudioManager.Instance.PlaySfx(clipToPlay);
            return;
        }

        audioSource.loop = clipToPlay.Loop;
        audioSource.clip = clipToPlay.Clip;
        audioSource.volume = clipToPlay.Volume;
        audioSource.Play();
    }
}
