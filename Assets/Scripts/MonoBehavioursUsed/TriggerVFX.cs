using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(VisualEffect))]
public class TriggerVFX : MonoBehaviour
{
    [Header("Nom de l'événement à envoyer au VFX Graph")]
    [Tooltip("Doit correspondre au nom d'un Event dans ton VFX Graph (ex: OnBurst)")]
    public string eventName = "OnBurst";

    [Header("Déclenchement automatique")]
    [Tooltip("Lance automatiquement le VFX au démarrage")]
    public bool playOnStart = false;

    [Tooltip("Relancer automatiquement si le VFX a fini ?")]
    public bool loop = false;

    private VisualEffect vfx;

    void Awake()
    {
        vfx = GetComponent<VisualEffect>();
    }

    void Start()
    {
        if (playOnStart)
        {
            TriggerEffect();
        }
    }

    /// <summary>
    /// Lance le VFX avec l'événement défini.
    /// </summary>
    public void TriggerEffect()
    {
        if (vfx == null) return;

        if (!string.IsNullOrEmpty(eventName))
            vfx.SendEvent(eventName);
        else
            vfx.Play();
    }

    /// <summary>
    /// Stoppe le VFX.
    /// </summary>
    public void StopEffect()
    {
        if (vfx == null) return;
        vfx.Stop();
    }

    void Update()
    {
        if (loop && vfx != null)
        {
            // Quand il n'y a plus de particules actives, on relance
            if (vfx.aliveParticleCount == 0)
            {
                TriggerEffect();
            }
        }
    }
}
