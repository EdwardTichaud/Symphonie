using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(VisualEffect))]
public class TriggerVFX : MonoBehaviour
{
    [Header("Nom de l'événement VFX (ex: OnBurst). Laisse vide pour utiliser Play().")]
    public string eventName = "OnBurst";

    [Header("Déclenchement automatique")]
    public bool playOnStart = false;
    public bool loop = false;

    private VisualEffect vfx;

    void Awake()
    {
        vfx = GetComponent<VisualEffect>();
    }

    void Start()
    {
        if (playOnStart)
            TriggerEffect();
    }

    /// <summary> Déclenche le VFX (SendEvent si eventName renseigné, sinon Play). </summary>
    public void TriggerEffect()
    {
        if (!vfx)
        {
            Debug.LogWarning("TriggerVFX: aucun VisualEffect trouvé sur cet objet.", this);
            return;
        }

        if (string.IsNullOrEmpty(eventName) || eventName.Equals("OnPlay"))
        {
            // Graph configuré sur OnPlay (par défaut)
            vfx.Play();
        }
        else
        {
            // Envoie l'événement personnalisé (ex: OnBurst)
            vfx.SendEvent(eventName);
        }
    }

    /// <summary> Stoppe le VFX. </summary>
    public void StopEffect()
    {
        if (vfx) vfx.Stop();
    }

    void Update()
    {
        if (loop && vfx != null)
        {
            // Quand il n'y a plus de particules actives, on relance
            if (vfx.aliveParticleCount == 0)
                TriggerEffect();
        }
    }

    // Pour tester rapidement depuis le menu contextuel du composant
    [ContextMenu("Trigger Now")]
    void ContextTrigger() => TriggerEffect();
}
