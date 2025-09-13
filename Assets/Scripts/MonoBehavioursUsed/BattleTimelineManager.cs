using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Gestionnaire dédié au lancement des <see cref="TimelineAsset"/> durant les combats.
/// Toutes les timelines de <c>MusicalMove</c> et d'<c>Item</c> sont lues via deux
/// <see cref="PlayableDirector"/> spécialisés :
/// <list type="bullet">
/// <item>PlayableDirector_Camera pour les mouvements de caméra globaux.</item>
/// <item>PlayableDirector_Caster pour les animations du lanceur.</item>
/// </list>
/// </summary>
public class BattleTimelineManager : MonoBehaviour
{
    /// <summary>Instance statique accessible depuis les autres scripts.</summary>
    public static BattleTimelineManager Instance { get; private set; }

    /// <summary>Director dédié aux timelines de caméra globales.</summary>
    private PlayableDirector directorCamera;

    /// <summary>Director chargé des animations du lanceur.</summary>
    private PlayableDirector directorCaster;

    /// <summary>Structure interne représentant une demande de lecture de timeline caméra.</summary>
    private class TimelineRequest
    {
        public TimelineAsset timeline;      // Timeline à jouer
        public GameObject caster;           // Objet utilisé pour les bindings (animations, signaux...)
        public GameObject cameraTarget;     // Nouvelle cible pour positionner et suivre la caméra
        public string cameraTag;            // Tag de la caméra à animer
        public bool autoRestore;            // Faut-il restaurer la caméra à la fin ?
        public Quaternion? fixedRotation;   // Rotation imposée pour conserver l'orientation
    }

    /// <summary>File d'attente des timelines caméra à jouer séquentiellement.</summary>
    private readonly Queue<TimelineRequest> timelineQueue = new Queue<TimelineRequest>();

    /// <summary>Référence vers la coroutine de traitement de la file.</summary>
    private Coroutine queueCoroutine;

    private void Awake()
    {
        // Mise en place du singleton classique.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Récupération des deux PlayableDirectors enfants.
        Transform camChild = transform.Find("PlayableDirector_Camera");
        Transform casterChild = transform.Find("PlayableDirector_Caster");
        directorCamera = camChild != null ? camChild.GetComponent<PlayableDirector>() : null;
        directorCaster = casterChild != null ? casterChild.GetComponent<PlayableDirector>() : null;

        if (directorCamera == null)
            Debug.LogError("[BattleTimelineManager] PlayableDirector_Camera introuvable.");
        if (directorCaster == null)
            Debug.LogError("[BattleTimelineManager] PlayableDirector_Caster introuvable.");

        // Le TimelineManager global utilise le director de caméra pour gérer les timelines principales.
        if (TimelineManager.Instance != null && directorCamera != null)
            TimelineManager.Instance.SetExternalDirector(directorCamera);
    }

    /// <summary>
    /// Lance une timeline caméra via le PlayableDirector dédié et la file d'attente.
    /// </summary>
    public void PlayTimeline(
        TimelineAsset timeline,
        GameObject caster,
        GameObject cameraTarget,
        string cameraTag,
        bool autoRestore = true,
        Quaternion? fixedRotation = null)
    {
        if (timeline == null || TimelineManager.Instance == null || directorCamera == null)
        {
            Debug.LogWarning("[BattleTimelineManager] Lecture annulée : gestionnaire ou timeline manquante.");
            return;
        }

        timelineQueue.Enqueue(new TimelineRequest
        {
            timeline = timeline,
            caster = caster,
            cameraTarget = cameraTarget,
            cameraTag = cameraTag,
            autoRestore = autoRestore,
            fixedRotation = fixedRotation
        });

        if (queueCoroutine == null)
            queueCoroutine = StartCoroutine(ProcessQueue());
    }

    /// <summary>Traite séquentiellement toutes les timelines caméra en attente.</summary>
    private IEnumerator ProcessQueue()
    {
        while (timelineQueue.Count > 0)
        {
            var request = timelineQueue.Dequeue();

            // Assure l'utilisation du director caméra par le TimelineManager global.
            TimelineManager.Instance.SetExternalDirector(directorCamera);

            // Restaure la caméra uniquement à la fin de la dernière timeline de la file.
            bool restore = request.autoRestore && timelineQueue.Count == 0;

            TimelineManager.Instance.PlayTimeline(
                request.timeline,
                request.caster,
                request.cameraTag,
                false,
                false,
                true,
                restore,
                request.fixedRotation,
                request.cameraTarget);

            // Attente de la fin de la timeline en cours.
            while (TimelineManager.Instance.IsTimelinePlaying)
            {
                if (timelineQueue.Count > 0)
                    TimelineManager.Instance.SetAutoRestore(false);
                yield return null;
            }
        }

        // Toutes les timelines ont été jouées : on libère la coroutine.
        queueCoroutine = null;
    }

    /// <summary>
    /// Joue une timeline d'animation pour le lanceur en parallèle de la caméra.
    /// </summary>
    public void PlayCasterTimeline(TimelineAsset timeline, GameObject caster)
    {
        if (timeline == null || directorCaster == null)
            return;

        directorCaster.playableAsset = timeline;

        // Binding explicite des pistes vers l'Animator ou le GameObject du lanceur.
        foreach (var output in timeline.outputs)
        {
            string lower = output.streamName.ToLower();
            if (lower.Contains("camera"))
                continue; // Les pistes caméra sont ignorées ici.

            if (lower.Contains("caster") || lower.Contains("pnj"))
            {
                if (caster == null)
                    continue;

                var animator = caster.GetComponentInChildren<Animator>();
                if (animator != null)
                    directorCaster.SetGenericBinding(output.sourceObject, animator);
                else
                    Debug.LogWarning("[BattleTimelineManager] Animator manquant sur le caster pour la timeline.");
            }
            else
            {
                if (caster != null)
                    directorCaster.SetGenericBinding(output.sourceObject, caster);
            }
        }

        directorCaster.time = 0;
        directorCaster.Play();
    }

    /// <summary>Indique si le director du lanceur joue actuellement une timeline.</summary>
    public bool IsCasterTimelinePlaying =>
        directorCaster != null && directorCaster.state == PlayState.Playing;
}

