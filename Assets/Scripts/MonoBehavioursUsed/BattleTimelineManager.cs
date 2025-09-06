using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Gestionnaire dédié au lancement des <see cref="TimelineAsset"/> durant les combats.
/// Toutes les timelines de <c>MusicalMove</c> et d'<c>Item</c>
/// sont lues via ce <see cref="PlayableDirector"/> unique pour
/// garantir un comportement cohérent.
/// <para>
/// À ne pas confondre avec la timeline d'interface qui affiche l'ordre
/// des tours : cette dernière est gérée par <see cref="BattleTimelineUIManager"/>.
/// </para>

/// </summary>
public class BattleTimelineManager : MonoBehaviour
{
    /// <summary>
    /// Instance statique accessible depuis les autres scripts.
    /// </summary>
    public static BattleTimelineManager Instance { get; private set; }

    /// <summary>
    /// PlayableDirector utilisé pour lancer les timelines de combat.
    /// </summary>
    private PlayableDirector director;

    private void Awake()
    {
        // Mise en place du singleton classique.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Récupère le PlayableDirector présent sur ce GameObject.
        director = GetComponent<PlayableDirector>();

        // Enregistre ce director auprès du TimelineManager global
        // afin qu'il soit utilisé pour toutes les timelines.
        if (TimelineManager.Instance != null)
            TimelineManager.Instance.SetExternalDirector(director);
    }

    /// <summary>
    /// Lance une timeline via le PlayableDirector de combat.
    /// </summary>
    /// <param name="timeline">Timeline à exécuter.</param>
    /// <param name="caster">GameObject jouant la timeline.</param>
    /// <param name="cameraTag">Tag de la caméra à animer. Peut être nul.</param>
    public void PlayTimeline(TimelineAsset timeline, GameObject caster, string cameraTag)
    {
        if (timeline == null || TimelineManager.Instance == null || director == null)
            return;

        // S'assure que le TimelineManager utilise bien ce PlayableDirector
        // avant de lancer la lecture de la timeline.
        TimelineManager.Instance.SetExternalDirector(director);
        TimelineManager.Instance.PlayTimeline(timeline, caster, cameraTag);
    }
}
