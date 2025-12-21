using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(ParticleSystem))]
public class ParticleLightsStable : MonoBehaviour
{
    [Header("Light Prefab & Pool")]
    public Light lightPrefab;
    [Range(1, 256)] public int maxLights = 32;   // sécurité perf (1 light par particule jusqu'à ce max)

    [Header("Fade (fraction de vie)")]
    [Range(0.01f, 0.4f)] public float edgeFraction = 0.10f; // 10% par défaut (début & fin)

    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;

    // Pool et mapping
    private class LightEntry { public Light light; public float baseIntensity; public bool inUse; }
    private readonly List<LightEntry> pool = new List<LightEntry>();
    private readonly Dictionary<uint, LightEntry> particleToLight = new Dictionary<uint, LightEntry>(); // randomSeed -> light
    private readonly HashSet<uint> seenThisFrame = new HashSet<uint>();

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        particles = new ParticleSystem.Particle[Mathf.Max(128, ps.main.maxParticles)];
        // Pré-instancie le pool (sans modifier le prefab asset)
        for (int i = 0; i < maxLights; i++)
        {
            var inst = Instantiate(lightPrefab, transform);
            var entry = new LightEntry { light = inst, baseIntensity = inst.intensity, inUse = false };
            inst.gameObject.SetActive(false);
            pool.Add(entry);
        }
    }

    void LateUpdate()
    {
        if (!ps) return;

        int alive = ps.GetParticles(particles);
        seenThisFrame.Clear();

        // Assigne / met à jour chaque particule (jusqu'à maxLights)
        for (int i = 0; i < alive; i++)
        {
            var p = particles[i];
            uint id = p.randomSeed;  // identifiant stable pendant la vie de la particule
            if (!particleToLight.TryGetValue(id, out var entry))
            {
                // Trouve une light libre dans le pool
                entry = AcquireFreeLight();
                if (entry == null) continue; // plus de lights dispos : on ignore cette particule
                particleToLight[id] = entry;
            }

            // Activer si besoin
            if (!entry.light.gameObject.activeSelf)
                entry.light.gameObject.SetActive(true);

            entry.inUse = true;
            seenThisFrame.Add(id);

            // Position = position de la particule
            entry.light.transform.position = p.position;

            // Calcul du multiplier d'intensité (fade-in/out 10%)
            float startLife = p.startLifetime > 0f ? p.startLifetime : ps.main.startLifetime.constant;
            if (startLife <= 0f) startLife = 1f; // fallback
            float t = 1f - (p.remainingLifetime / startLife); // 0..1 (0=naissance, 1=mort)
            t = Mathf.Clamp01(t);

            float e = Mathf.Clamp01(edgeFraction);
            float mul;
            if (t < e)
            {
                // fade-in (S-curve)
                mul = Mathf.SmoothStep(0f, 1f, t / e);
            }
            else if (t > 1f - e)
            {
                // fade-out (S-curve)
                mul = Mathf.SmoothStep(0f, 1f, (1f - t) / e);
            }
            else
            {
                mul = 1f;
            }

            // Applique l'intensité sans toucher aux autres propriétés du prefab
            entry.light.intensity = entry.baseIntensity * mul;
            // NB: On NE modifie pas color, range, shadows, etc. => le prefab reste la référence visuelle.
        }

        // Libère les lights dont la particule a disparu ce frame
        ReleaseUnusedLights();
    }

    private LightEntry AcquireFreeLight()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (!pool[i].inUse)
            {
                pool[i].inUse = true;
                return pool[i];
            }
        }
        return null;
    }

    private void ReleaseUnusedLights()
    {
        // Toute entrée mappée à un id non vu ce frame doit être libérée
        // On collecte d'abord les ids à libérer pour ne pas modifier le dictionnaire en itérant
        var toRelease = new List<uint>();
        foreach (var kvp in particleToLight)
        {
            if (!seenThisFrame.Contains(kvp.Key))
            {
                toRelease.Add(kvp.Key);
            }
        }

        for (int i = 0; i < toRelease.Count; i++)
        {
            var id = toRelease[i];
            var entry = particleToLight[id];
            entry.inUse = false;
            // Réinitialise seulement ce qu'on a modifié
            entry.light.intensity = entry.baseIntensity;
            entry.light.gameObject.SetActive(false);
            particleToLight.Remove(id);
        }

        // Marque tout le pool comme non-utilisé pour la frame suivante,
        // puis re-marque ceux utilisés pendant la prochaine Update.
        for (int i = 0; i < pool.Count; i++)
            pool[i].inUse = false;

        // Re-marque les encore utilisés (ceux présents dans le mapping)
        foreach (var kvp in particleToLight)
            kvp.Value.inUse = true;
    }
}
