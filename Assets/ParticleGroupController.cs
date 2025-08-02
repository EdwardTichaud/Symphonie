using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class ParticleGroupEditorController : MonoBehaviour
{
    [Header("Particle Systems à contrôler")]
    public List<ParticleSystem> particleSystems = new List<ParticleSystem>();

    [Header("Contrôle de simulation")]
    public bool simulateInEditor = false;
    public float slowdownDelay = 0.1f;
    public float slowdownFactor = 0.1f;
    public float durationOfSlowdown = 1.5f;

    private float elapsedTime = 0f;
    private bool isSlowedDown = false;
    private Dictionary<ParticleSystem, float> originalSpeeds = new();

    void Update()
    {
        if (Application.isPlaying || !simulateInEditor)
            return;

        elapsedTime += Time.deltaTime;

        foreach (var ps in particleSystems)
        {
            if (ps == null) continue;

            // Sauvegarde vitesse initiale une fois
            if (!originalSpeeds.ContainsKey(ps))
            {
                originalSpeeds[ps] = ps.main.simulationSpeed;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Simulate(0f, true, true); // Réinitialise
                ps.Play();
            }

            // Avance la simulation manuellement
            ps.Simulate(Time.deltaTime, true, false);
        }

        // Applique le ralentissement
        if (!isSlowedDown && elapsedTime >= slowdownDelay)
        {
            foreach (var ps in particleSystems)
            {
                if (ps == null) continue;
                var main = ps.main;
                main.simulationSpeed = slowdownFactor;
            }

            isSlowedDown = true;
        }

        // Restaure la vitesse après durationOfSlowdown
        if (isSlowedDown && elapsedTime >= slowdownDelay + durationOfSlowdown)
        {
            foreach (var ps in particleSystems)
            {
                if (ps == null || !originalSpeeds.ContainsKey(ps)) continue;
                var main = ps.main;
                main.simulationSpeed = originalSpeeds[ps];
            }

            simulateInEditor = false; // arrête la simulation
        }
    }

    [ContextMenu("Simuler dans l'éditeur")]
    public void StartEditorSimulation()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("La simulation d'éditeur est réservée au mode Edit.");
            return;
        }

        elapsedTime = 0f;
        isSlowedDown = false;
        originalSpeeds.Clear();
        simulateInEditor = true;

#if UNITY_EDITOR
    UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
    SceneView.RepaintAll();
#endif
    }

#if UNITY_EDITOR
public void ResetParticles()
{
    elapsedTime = 0f;
    isSlowedDown = false;
    simulateInEditor = false;

    foreach (var ps in particleSystems)
    {
        if (ps == null) continue;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Simulate(0f, true, true);
    }

    UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
    SceneView.RepaintAll();
}
#endif

}
