using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.Collections; // Nécessaire pour les coroutines
using System.Collections.Generic; // Gestion des files d'attente de timelines

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

    /// <summary>
    /// Représente une demande de lecture de Timeline.
    /// Chaque appel à <see cref="PlayTimeline"/> est empilé ici afin
    /// d'être joué séquentiellement sans perdre la caméra.
    /// </summary>
    private class TimelineRequest
    {
        public TimelineAsset timeline;
        public GameObject caster;
        public string cameraTag;
        public bool autoRestore;
        public Quaternion? fixedRotation;
    }

    /// <summary>
    /// File d'attente des timelines à jouer. Permet le cumul automatique
    /// lorsque plusieurs moves se déclenchent successivement.
    /// </summary>
    private readonly Queue<TimelineRequest> timelineQueue = new Queue<TimelineRequest>();

    /// <summary>
    /// Référence vers la coroutine de traitement de la file d'attente
    /// pour éviter les doublons.
    /// </summary>
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
            // proprement pour éviter toute NullReferenceException.
            Debug.LogWarning("[BattleTimelineManager] Lecture annulée : gestionnaire ou timeline manquante.");
            return;
        }

        // Empile la demande de lecture pour une exécution séquentielle.
        timelineQueue.Enqueue(new TimelineRequest
        {
            timeline = timeline,
            caster = caster,
            cameraTag = cameraTag,
            autoRestore = autoRestore,
            fixedRotation = fixedRotation
        });

        // Si aucune coroutine ne traite actuellement la file, on en démarre une.
        if (queueCoroutine == null)
            queueCoroutine = StartCoroutine(ProcessQueue());
    }

    /// <summary>
    /// Traite séquentiellement toutes les timelines en attente dans la file.
    /// Cette coroutine garantit qu'une timeline ne démarre qu'une fois la
    /// précédente terminée, permettant ainsi un véritable cumul.
    /// </summary>
    private IEnumerator ProcessQueue()
    {
        while (timelineQueue.Count > 0)
        {
            var request = timelineQueue.Dequeue();

            // S'assure que le TimelineManager utilise bien ce PlayableDirector
            // avant chaque lecture.
            TimelineManager.Instance.SetExternalDirector(director);

            // Si d'autres timelines sont déjà en file, on empêche la
            // restauration automatique afin de conserver la position de la caméra.
            bool restore = request.autoRestore && timelineQueue.Count == 0;

            TimelineManager.Instance.PlayTimeline(request.timeline, request.caster, request.cameraTag,
                                                  false, false, true, restore, request.fixedRotation);

            // Attente de la fin de la timeline en cours.
            while (TimelineManager.Instance.IsTimelinePlaying)
            {
                // Si une nouvelle timeline arrive durant la lecture, on désactive
                // immédiatement la restauration pour la timeline active.
                if (timelineQueue.Count > 0)
                    TimelineManager.Instance.SetAutoRestore(false);
                yield return null;
            }
        }

        // Toutes les timelines ont été jouées : la coroutine peut s'arrêter.
        queueCoroutine = null;
    }

    /// <summary>
    /// Joue une timeline additionnelle sans interrompre celle déjà active.
    /// Permet une superposition ponctuelle d'animations durant un combat.
    /// </summary>
    public void PlayTimelineOverlay(TimelineAsset timeline, GameObject caster)
    {
        // Ignore la demande si aucune timeline n'est fournie
        if (timeline == null)
            return;

        // Crée un PlayableDirector temporaire pour cette timeline
        var overlayDirector = gameObject.AddComponent<PlayableDirector>();
        overlayDirector.playableAsset = timeline;

        // Lie uniquement les pistes pertinentes. Les pistes sans Caster sont ignorées.
        foreach (var output in timeline.outputs)
        {
            string lower = output.streamName.ToLower();
            if (lower.Contains("camera"))
                continue; // Caméra gérée ailleurs

            if (lower.Contains("caster") || lower.Contains("pnj"))
            {
                // Si aucun caster n'est fourni, on ignore simplement la piste
                if (caster == null)
                    continue;

                var animator = caster.GetComponentInChildren<Animator>();
                if (animator != null)
                    overlayDirector.SetGenericBinding(output.sourceObject, animator);
                else
                    Debug.LogWarning("[BattleTimelineManager] Animator manquant sur le caster pour la timeline overlay.");
            }
            else
            {
                // Les autres pistes nécessitent également la référence du caster
                if (caster != null)
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
