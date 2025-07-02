using UnityEngine;

public class CinematicTrigger : MonoBehaviour
{
    [Header("Joueur")] public string playerTag = "Player";
    public CinematicPlayer cinematicPlayer;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            cinematicPlayer?.Play();
        }
    }
}
