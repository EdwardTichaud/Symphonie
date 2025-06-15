using UnityEngine;

public class PlaySound : MonoBehaviour
{
    public AudioClip soundClip;

    void Start()
    {
        Play(soundClip);
    }

    void Play(AudioClip audioClip)
    {
        AudioSource soundSource = GetComponent<AudioSource>();
        soundSource.PlayOneShot(audioClip);
    }
}
