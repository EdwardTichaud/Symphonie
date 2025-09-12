using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.Collections; // Nécessaire pour les coroutines

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
    /// <param name="autoRestore">False pour chaîner plusieurs timelines sans rendre la main au contrôleur caméra.</param>
    /// <param name="fixedRotation">Rotation imposée pour la caméra. Si non spécifiée, la rotation actuelle du
    /// caster est utilisée à chaque appel. Ce paramètre permet de conserver l'orientation initiale du lanceur
    /// sur toute la durée d'un move même si plusieurs timelines se succèdent.</param>
    public void PlayTimeline(TimelineAsset timeline, GameObject caster, string cameraTag, bool autoRestore = true, Quaternion? fixedRotation = null)
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
        //
        // On transmet la rotation initiale éventuelle afin que la caméra s'aligne
        // une seule fois et ignore ensuite les changements d'orientation du caster.
        TimelineManager.Instance.PlayTimeline(timeline, caster, cameraTag, false, false, true, autoRestore, fixedRotation);
    }

    /// <summary>
    /// Joue une timeline additionnelle sans interrompre celle déjà active.
    /// Permet une superposition ponctuelle d'animations durant un combat.
    /// </summary>
    public void PlayTimelineOverlay(TimelineAsset timeline, GameObject caster)
    {
        if (timeline == null || caster == null)
            return;

        // Crée un PlayableDirector temporaire pour cette timeline
        var overlayDirector = gameObject.AddComponent<PlayableDirector>();
        overlayDirector.playableAsset = timeline;

        // Lie uniquement les pistes liées au lanceur et ignore la caméra
        foreach (var output in timeline.outputs)
        {
            string lower = output.streamName.ToLower();
            if (lower.Contains("camera"))
                continue; // Caméra gérée ailleurs

            if (lower.Contains("caster") || lower.Contains("pnj"))
            {
                var animator = caster.GetComponentInChildren<Animator>();
                if (animator != null)
                    overlayDirector.SetGenericBinding(output.sourceObject, animator);
            }
            else
            {
                overlayDirector.SetGenericBinding(output.sourceObject, caster);
            }
        }

        overlayDirector.Play();
        StartCoroutine(CleanupOverlay(overlayDirector));
    }

    /// <summary>
    /// Détruit le PlayableDirector temporaire lorsqu'il a terminé sa lecture.
    /// </summary>
    private IEnumerator CleanupOverlay(PlayableDirector dir)
    {
        while (dir != null && dir.state == PlayState.Playing)
            yield return null;
        if (dir != null)
            Destroy(dir);
    }

    /// <summary>
    /// Joue une timeline additionnelle sans interrompre celle déjà active.
    /// Permet une superposition ponctuelle d'animations durant un combat.
    /// </summary>
    public void PlayTimelineOverlay(TimelineAsset timeline, GameObject caster)
    {
        if (timeline == null || caster == null)
            return;

        // Crée un PlayableDirector temporaire pour cette timeline
        var overlayDirector = gameObject.AddComponent<PlayableDirector>();
        overlayDirector.playableAsset = timeline;

        // Lie uniquement les pistes liées au lanceur et ignore la caméra
        foreach (var output in timeline.outputs)
        {
            string lower = output.streamName.ToLower();
            if (lower.Contains("camera"))
                continue; // Caméra gérée ailleurs

            if (lower.Contains("caster") || lower.Contains("pnj"))
            {
                var animator = caster.GetComponentInChildren<Animator>();
                if (animator != null)
                    overlayDirector.SetGenericBinding(output.sourceObject, animator);
            }
            else
            {
                overlayDirector.SetGenericBinding(output.sourceObject, caster);
            }
        }

        overlayDirector.Play();
        StartCoroutine(CleanupOverlay(overlayDirector));
    }

    /// <summary>
    /// Détruit le PlayableDirector temporaire lorsqu'il a terminé sa lecture.
    /// </summary>
    private IEnumerator CleanupOverlay(PlayableDirector dir)
    {
        while (dir != null && dir.state == PlayState.Playing)
            yield return null;
        if (dir != null)
            Destroy(dir);
    }
}
