using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ParticleSystemActivator : MonoBehaviour
{
    [Header("Paramètres d'activation")]
    [Tooltip("Lancer automatiquement au Start")]
    public bool activateOnStart = true;

    [Tooltip("Activer en boucle à un intervalle (secondes). Mettre à 0 pour un seul déclenchement.")]
    public float activationFrequency = 0f;

    private List<ParticleSystem> particleSystems = new List<ParticleSystem>();
    private Coroutine loopCoroutine;

    void Awake()
    {
        // Récupère tous les ParticleSystem dans ce GameObject et ses enfants
        particleSystems.AddRange(GetComponentsInChildren<ParticleSystem>(true));
    }

    void Start()
    {
        if (activateOnStart)
        {
            ActivateAllParticles();

            if (activationFrequency > 0f)
                loopCoroutine = StartCoroutine(ActivateLoop());
        }
    }

    /// <summary>
    /// Active tous les systèmes de particules listés.
    /// </summary>
    public void ActivateAllParticles()
    {
        foreach (var ps in particleSystems)
        {
            if (ps.isPlaying)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ps.Play();
        }
    }

    /// <summary>
    /// Coroutine d’activation répétée selon la fréquence définie.
    /// </summary>
    private IEnumerator ActivateLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(activationFrequency);
            ActivateAllParticles();
        }
    }

    /// <summary>
    /// Permet d’activer manuellement depuis un autre script ou événement.
    /// </summary>
    public void TriggerOnce()
    {
        ActivateAllParticles();
    }

    /// <summary>
    /// Arrête la boucle (si active)
    /// </summary>
    public void StopLoop()
    {
        if (loopCoroutine != null)
            StopCoroutine(loopCoroutine);
    }
}
