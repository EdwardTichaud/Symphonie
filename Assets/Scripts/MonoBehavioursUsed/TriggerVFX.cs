using System; // Nécessaire pour comparer proprement les noms d'évènements.
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
        if (vfx == null)
        {
            // On journalise pour ne pas laisser un appel silencieux, cela facilite le débogage en scène.
            Debug.LogWarning("TriggerVFX n'a trouvé aucun VisualEffect à déclencher.", this);
            return;
        }

        // Lorsque l'évènement n'est pas renseigné (cas par défaut) on lance simplement le Play classique du VFX.
        if (string.IsNullOrEmpty(eventName))
        {
            vfx.Play();
            return;
        }

        // Beaucoup de graph VFX, comme DebrisBurst, attendent l'évènement par défaut "OnPlay".
        // On simplifie donc la configuration en traitant ce cas comme un appel direct à Play.
        if (eventName.Equals("OnPlay", StringComparison.OrdinalIgnoreCase))
        {
            vfx.Play();
            return;
        }

        // Si l'évènement personnalisé existe vraiment dans le graph, on le lance comme prévu.
        if (vfx.HasEvent(eventName))
        {
            vfx.SendEvent(eventName);
        }
        else
        {
            // En dernier recours (cas de DebrisBurst paramétré en "OnBurst"), on repasse sur Play afin de garantir l'effet.
            Debug.LogWarning($"L'évènement '{eventName}' n'existe pas sur le graph VFX attaché à '{vfx.visualEffectAsset?.name}'. Utilisation de Play à la place.", this);
            vfx.Play();
        }
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
