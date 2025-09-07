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

        // 🔧 Si aucun PlayableDirector n'est trouv\u00e9 (objet oubli\u00e9 ou supprim\u00e9),
        // on en ajoute un dynamiquement afin d'\u00e9viter que les timelines
        // ne restent silencieuses en combat.
        if (director == null)
        {
            director = gameObject.AddComponent<PlayableDirector>();
            Debug.LogWarning("[BattleTimelineManager] Aucun PlayableDirector trouv\u00e9, ajout d'un composant par d\u00e9faut.");
        }

        // Enregistre ce director aupr\u00e8s du TimelineManager global afin qu'il soit
        // utilis\u00e9 pour toutes les timelines lanc\u00e9es pendant les combats.
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
        if (timeline == null || TimelineManager.Instance == null)
        {
            // Pas de timeline ou de gestionnaire disponible : on abandonne
            // proprement pour \u00e9viter toute NullReferenceException.
            Debug.LogWarning("[BattleTimelineManager] Lecture annul\u00e9e : gestionnaire ou timeline manquante.");
            return;
        }

        // S'assure que le TimelineManager utilise bien ce PlayableDirector
        // avant de lancer la lecture de la timeline.
        TimelineManager.Instance.SetExternalDirector(director);
        // Les timelines d'Item et de MusicalMove ne doivent ni couper la musique
        // ni afficher de fondu au noir : on force donc l'absence de fondu
        // (paramètre withFade = false) et on conserve la bande-son actuelle
        // en désactivant l'interruption musicale (interruptMusic = false).
        TimelineManager.Instance.PlayTimeline(timeline, caster, cameraTag, false, false);
    }
}
