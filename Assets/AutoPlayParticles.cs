using UnityEngine;

public class AutoPlayParticles : MonoBehaviour
{
    void Start()
    {
        // Récupère tous les ParticleSystem dans ce GameObject et ses enfants
        ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>();

        foreach (ParticleSystem ps in particles)
        {
            ps.Play(); // Lance le système de particules
        }
    }
}
