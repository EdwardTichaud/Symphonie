using UnityEngine;

/// <summary>
/// Déclenche un effet de bris de caméra en combat.
/// </summary>
public class BattleCameraShatter : MonoBehaviour
{
    [Tooltip("Son joué lors du bris de la caméra")]
    public AudioClip shatterSound;
    [Tooltip("Effet visuel à instancier sur la caméra")]
    public GameObject shatterEffect;

    /// <summary>
    /// Joue le son et l'effet de bris de caméra.
    /// </summary>
    public void Break()
    {
        if (shatterSound != null)
            AudioManager.Instance?.PlaySound(shatterSound);

        if (shatterEffect != null)
            Instantiate(shatterEffect, transform.position, Quaternion.identity);
    }
}
