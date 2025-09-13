using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System;
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
    /// Repositionne immédiatement l'origine de la caméra de combat sur une cible fournie.
    /// Utilisé lorsque seule l'animation du lanceur est jouée (overlay) afin de
    /// conserver une mise en scène cohérente malgré l'absence de timeline caméra
    /// spécifique à la phase en cours.
    /// </summary>
    /// <param name="cameraTarget">Objet servant de nouvelle ancre pour la caméra.</param>
    /// <param name="cameraTag">Tag de la caméra à déplacer.</param>
    /// <param name="fixedRotation">Rotation imposée, ou null pour reprendre celle de la cible.</param>
    public void AlignCameraToTarget(GameObject cameraTarget, string cameraTag, Quaternion? fixedRotation = null)
    {
        if (cameraTarget == null || string.IsNullOrEmpty(cameraTag))
            return;

        // Recherche de la caméra puis de son parent direct (BattleCamera_Origin).
        GameObject cameraGO = GameObject.FindGameObjectWithTag(cameraTag);
        Transform cameraParent = cameraGO != null ? cameraGO.transform.parent : null;

        if (cameraParent != null)
        {
            cameraParent.position = cameraTarget.transform.position;
            cameraParent.rotation = fixedRotation ?? cameraTarget.transform.rotation;
        }
    }

    /// <summary>
    /// Crée dynamiquement une cible de caméra placée entre deux unités
    /// afin de cadrer simultanément le lanceur et sa cible lorsque
    /// aucune timeline de caméra complète n'est définie.
    /// </summary>
    /// <param name="caster">Unité à l'origine de l'action.</param>
    /// <param name="target">Unité visée par l'action.</param>
    /// <param name="cameraTag">Tag de la caméra à orienter.</param>
    /// <returns>Parent de la caméra de combat après repositionnement.</returns>
    public GameObject CreateMidpointCameraTarget(GameObject caster, GameObject target, string cameraTag)
    {
        if (caster == null || target == null || string.IsNullOrEmpty(cameraTag))
            return null;

        // 🔍 Récupère la caméra de combat et son parent.
        GameObject camGO = GameObject.FindGameObjectWithTag(cameraTag);
        if (camGO == null)
            return null;

        Transform camParent = camGO.transform.parent;
        Camera cam = camGO.GetComponent<Camera>();
        if (camParent == null || cam == null)
            return null;

        // 📍 Réinitialise le parent à l'origine pour travailler en coordonnées monde.
        camParent.position = Vector3.zero;
        camParent.rotation = Quaternion.identity;

        // Positions mondes des unités concernées.
        Vector3 casterPos = caster.transform.position;
        Vector3 targetPos = target.transform.position;
        Vector3 midpoint = (casterPos + targetPos) / 2f; // Milieu entre lanceur et cible

        // 📏 Calcule la distance nécessaire pour englober les deux unités selon le FOV actuel.
        float distance = Vector3.Distance(casterPos, targetPos);
        float fovRad = cam.fieldOfView * Mathf.Deg2Rad;
        float requiredDist = (distance * 0.5f) / Mathf.Tan(fovRad / 2f);

        // 🎯 Conserve une direction proche de celle actuelle afin d'éviter les à-coups.
        Vector3 direction = (cam.transform.position - midpoint).normalized;
        if (direction == Vector3.zero)
            direction = -cam.transform.forward;

        Vector3 cameraPosition = midpoint + direction * requiredDist;

        // 🚚 Positionne la caméra enfant et l'oriente vers le milieu des deux unités.
        camGO.transform.position = cameraPosition;
        camGO.transform.rotation = Quaternion.LookRotation(midpoint - cameraPosition);

        // Le parent sert désormais d'ancre neutre pour les éventuelles timelines.
        return camParent.gameObject;
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

        // Binding explicite des pistes vers l'Animator, le GameObject du lanceur
        // ou encore le SignalReceiver associé au PlayableDirector.
        foreach (var output in timeline.outputs)
        {
            // 🔍 Type attendu par la piste (Animator, SignalReceiver, etc.)
            Type type = output.outputTargetType;
            string lower = output.streamName.ToLower();

            // Les pistes caméra sont ignorées dans ce director spécialisé.
            if (lower.Contains("camera"))
                continue;

            // ✅ Gestion spécifique des pistes de Signaux : on les relie au
            // SignalReceiver présent sur le PlayableDirector pour que les
            // événements de la timeline soient correctement reçus.
            if (type != null && typeof(Component).IsAssignableFrom(type) && type.Name.Contains("SignalReceiver"))
            {
                Component receiver = directorCaster.GetComponent(type);
                if (receiver != null)
                {
                    directorCaster.SetGenericBinding(output.sourceObject, receiver);
                }
                else
                {
                    Debug.LogWarning($"[BattleTimelineManager] {type.Name} manquant sur {directorCaster.gameObject.name} pour la timeline.");
                }
                continue; // Rien d'autre à faire pour cette piste
            }

            // 🎭 Pistes d'animation : on tente de lier un Animator du lanceur
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
                // 🧍 Autres pistes : on relie simplement le GameObject du lanceur
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

