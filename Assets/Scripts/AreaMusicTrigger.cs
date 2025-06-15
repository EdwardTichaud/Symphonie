using UnityEngine;

public class AreaMusicTrigger : MonoBehaviour
{
    [Tooltip("Musique jouée dans cette zone d'exploration")]
    public AudioClip zoneMusic;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        AudioManager.Instance.TransitionToNewExplorationZone(zoneMusic);
    }
}