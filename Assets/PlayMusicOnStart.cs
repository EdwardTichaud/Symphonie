using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PlayMusicOnStart : MonoBehaviour
{
    public AudioClip clipToPlay;

    void Start()
    {
        AudioSource audioSource = GetComponent<AudioSource>();
        audioSource.clip = clipToPlay;
        audioSource.Play();
    }
}
