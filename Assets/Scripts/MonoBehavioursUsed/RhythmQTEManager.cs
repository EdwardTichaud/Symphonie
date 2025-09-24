using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using UnityEngine.UI;
using UnityEngine.Timeline; // Gestion des timelines de combat

public class RhythmQTEManager : MonoBehaviour
{
    public static RhythmQTEManager Instance { get; private set; }

    public MusicalMoveSO currentMove;
    private float startTime;
    private int currentBeatIndex = 0;
    private bool isActive = false;
    private List<bool> successResults;

    // Dernier résultat enregistré pour un QTE d'objet
    public bool LastItemSuccess { get; private set; }

    // QTE
    private Coroutine beatRoutine;
    // Coroutine responsable d'un éventuel changement différé de caméra.
    // Permet de conserver le cadrage de préparation pendant un court instant
    // lorsque l'on enchaîne avec la phase de performing.
    private Coroutine pendingCameraSwitch;

    private float defaultFixedDeltaTime;

    [Header("QTE Visuel")]
    public GameObject qteCirclePrefab; // Prefab du cercle QTE historique
    public Transform qteUIParent; // Parent dans le canvas (facultatif, sinon instancié en world space)

    [Header("QTE Barre")]
    public GameObject qteBarPrefab; // Prefab de la barre façon Guitar Hero

    // Instance actuellement affichée de la barre et file d'attente des notes
    private QTEBar activeQTEBar;
    private readonly Queue<Image> preparedNotes = new();

    // Temps d'avance avec lequel une note est affichée avant la zone de validation
    private const float noteAdvanceTime = 2f;

    private DefenseResult defenseResult;
    public DefenseResult GetDefenseResult() => defenseResult;

    // MoveTo
    float elapsed = 0f;

    // Durée par défaut utilisée si aucun délai n'est défini dans les données
    // Conserve l'ancien comportement en cas d'oubli de paramétrage
    private const float defaultTeleportDelay = 0.2f;

    [Header("Déplacements")]
    [Tooltip("Particules instanciées lors d'un déplacement classique afin d'accentuer la sensation de dash.")]
    [SerializeField] private ParticleSystem dashParticlesPrefab;
    [Tooltip("Clip joué lorsqu'aucun son spécifique n'est défini sur le personnage pour l'effet de dash.")]
    [SerializeField] private AudioClipSO defaultDashSound;

    // Tag de la caméra de combat à utiliser pour toutes les timelines
    private const string battleCameraTag = "BattleCamera";

    // QTE Effect
    public AudioClipSO successSFX;
    public AudioClipSO failSFX;
    // Effets visuels pour indiquer le résultat du QTE
    public GameObject successEffectPrefab;
    public GameObject failEffectPrefab;

    // ------------------------------------------------------------------------------
    // Gestion des effets sonores
    // ------------------------------------------------------------------------------
    /// <summary>
    /// Joue un effet sonore à l'aide d'une source SFX disponible.
    /// </summary>
    /// <param name="clip">Clip audio à jouer</param>
    private void PlaySfx(AudioClipSO clip)
    {
        if (clip == null)
            return;

        // Centralise la lecture via l'AudioManager afin de profiter de la normalisation et
        // des volumes globaux configurés dans le projet.
        AudioManager.Instance?.PlaySfx(clip);
    }

    private CharacterUnit currentCaster;
    private CharacterUnit currentTarget;
    private int pendingNotes = 0;
    private bool qteActive = false;

    // Si activé, le temps de jeu est figé durant les QTE pour faciliter l'exécution
    [SerializeField] private bool easyMode = false;

    #region Initialisation
    /// <summary>
    /// Configure l'instance unique et mémorise le fixedDeltaTime de départ.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        defaultFixedDeltaTime = Time.fixedDeltaTime;
    }

    /// <summary>
    /// Prépare la barre de QTE en l'affichant et en créant toutes les notes à l'avance.
    /// </summary>
    /// <param name="notes">Liste des données de notes à afficher</param>
    public void PrepareQTEBar(IList<MusicalMoveSO.NoteData> notes)
    {
        if (qteBarPrefab == null || notes == null || notes.Count == 0)
            return;

        // Nettoie une éventuelle barre précédente
        ClearQTEBar();

        var go = Instantiate(qteBarPrefab, qteUIParent);
        activeQTEBar = go.GetComponent<QTEBar>();

        preparedNotes.Clear();
        float cumulative = 0f;
        foreach (var n in notes)
        {
            cumulative += n.rhythm;
            // Les notes apparaissent noteAdvanceTime secondes avant d'arriver dans la zone
            float delay = Mathf.Max(0f, cumulative - noteAdvanceTime);
            var img = activeQTEBar.ScheduleNote(n.noteInput, delay, noteAdvanceTime);
            preparedNotes.Enqueue(img);
        }
    }

    /// <summary>
    /// Variante pour un motif simple d'item sans icônes spécifiques.
    /// </summary>
    public void PrepareQTEBar(IList<float> beatPattern)
    {
        if (qteBarPrefab == null || beatPattern == null || beatPattern.Count == 0)
            return;

        ClearQTEBar();
        var go = Instantiate(qteBarPrefab, qteUIParent);
        activeQTEBar = go.GetComponent<QTEBar>();

        preparedNotes.Clear();

        float cumulative = 0f;
        // Notes anonymes, pas d'icône
        for (int i = 0; i < beatPattern.Count; i++)
        {
            cumulative += beatPattern[i];
            float delay = Mathf.Max(0f, cumulative - noteAdvanceTime);
            var img = activeQTEBar.ScheduleNote(null, delay, noteAdvanceTime);
            preparedNotes.Enqueue(img);
        }
    }

    /// <summary>
    /// Détruit la barre de QTE active et vide la file des notes.
    /// </summary>
    public void ClearQTEBar()
    {
        if (activeQTEBar != null)
            Destroy(activeQTEBar.gameObject);

        activeQTEBar = null;
        preparedNotes.Clear();
    }

    // ------------------------------------------------------------------------------
    // Utilitaires Timeline
    // ------------------------------------------------------------------------------
    /// <summary>
    /// Lance une timeline en tant que séquence principale ou en superposition.
    /// </summary>
    /// <param name="timeline">Timeline à jouer.</param>
    /// <param name="overlay">Vrai pour une timeline en superposition.</param>
    /// <param name="caster">Unité lançant la timeline.</param>
    /// <param name="animatorGO">Objet de référence pour le binding (caster).</param>
    /// <param name="cameraTarget">Objet servant d'ancre pour la caméra.</param>
    /// <param name="autoRestore">Indique si la caméra doit être restaurée automatiquement.</param>
    /// <param name="initialRotation">Rotation de référence à conserver.</param>
    private void StartTimelinePhase(
        TimelineAsset timeline,
        bool overlay,
        CharacterUnit caster,
        GameObject animatorGO,
        GameObject cameraTarget,
        bool autoRestore,
        Quaternion initialRotation,
        BattleCameraRole cameraRole = BattleCameraRole.None,
        float cameraSwitchDelay = 0f)
    {
        if (timeline == null || BattleTimelineManager.Instance == null || caster == null)
            return;

        // 🎥 Active la caméra dédiée à cette phase si elle est renseignée.
        if (cameraRole != BattleCameraRole.None)
        {
            // 🧹 Annule toute requête précédente afin d'éviter qu'un délai résiduel
            //     ne vienne écraser un nouveau cadrage.
            CancelPendingCameraSwitch();

            if (cameraSwitchDelay <= 0f)
            {
                // ⏱️ Pas de délai demandé : on applique immédiatement le cadrage.
                BattleCameraManager.Instance?.SwitchToCamera(cameraRole);
            }
            else
            {
                // ⏳ Délai personnalisé : la caméra reste sur le rôle précédent
                //     pendant la durée souhaitée avant de basculer.
                pendingCameraSwitch = StartCoroutine(SwitchCameraAfterDelay(cameraRole, cameraSwitchDelay));
            }
        }

        // 📽️ La caméra n'étant plus pilotée par les timelines des moves/items,
        // nous ré-alignons simplement l'origine avant de lancer la timeline du lanceur.
        GameObject defaultCasterTarget = caster.GetCasterBindingTarget();
        BattleTimelineManager.Instance.AlignCameraToTarget(
            cameraTarget ?? animatorGO ?? defaultCasterTarget, // Fallback sur l'Animator enfant du lanceur
            battleCameraTag,
            initialRotation);

        // Lecture de la timeline via le PlayableDirector de l'unité concernée.
        GameObject binding = animatorGO ?? defaultCasterTarget;
        BattleTimelineManager.Instance.PlayCasterTimeline(timeline, caster, binding);
    }

    /// <summary>
    /// Interrompt la coroutine responsable d'un changement de caméra différé.
    /// </summary>
    private void CancelPendingCameraSwitch()
    {
        if (pendingCameraSwitch == null)
            return;

        StopCoroutine(pendingCameraSwitch);
        pendingCameraSwitch = null;
    }

    /// <summary>
    /// Active un rôle de caméra après un délai personnalisé.
    /// </summary>
    /// <param name="role">Rôle de caméra à activer.</param>
    /// <param name="delay">Durée à attendre avant le basculement.</param>
    private IEnumerator SwitchCameraAfterDelay(BattleCameraRole role, float delay)
    {
        // On attend patiemment la durée configurée afin que la phase en cours
        // puisse démarrer tout en conservant le cadrage précédent.
        yield return new WaitForSeconds(delay);

        // Si un autre StartTimelinePhase a été appelé durant l'attente, la coroutine
        // aura été stoppée et n'atteindra jamais ce point, évitant ainsi tout conflit.
        BattleCameraManager.Instance?.SwitchToCamera(role);

        // Le champ est libéré pour accepter un nouveau délai si nécessaire.
        pendingCameraSwitch = null;
    }

    /// <summary>
    /// Attend la fin d'une timeline précédemment lancée.
    /// </summary>
    /// <param name="timeline">Timeline à surveiller.</param>
    /// <param name="overlay">Paramètre conservé pour compatibilité, sans effet.</param>
    private IEnumerator WaitForTimelinePhase(TimelineAsset timeline, bool overlay, CharacterUnit caster)
    {
        if (timeline == null || caster == null)
            yield break;

        float maxDuration = (float)timeline.duration;
        float timer = 0f;
        while (BattleTimelineManager.Instance != null &&
               BattleTimelineManager.Instance.IsCasterTimelinePlaying(caster) &&
               timer < maxDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (BattleTimelineManager.Instance != null && BattleTimelineManager.Instance.IsCasterTimelinePlaying(caster))
            Debug.LogWarning($"[RhythmQTEManager] Timeline '{timeline.name}' encore active après {maxDuration}s. Suite forcée.");
    }

    // Séquence du Musicalmove - Ajouter autant de méthodes que d'effets durant le move
    /// <summary>
    /// Orchestration complète d'un MusicalMove du déplacement à la résolution.
    /// </summary>
    public IEnumerator MusicalMoveRoutine(MusicalMoveSO move, CharacterUnit caster, CharacterUnit target)
    {
        // Le caster peut éventuellement être détruit avant que la routine ne débute
        // On sécurise donc l'accès à son nom dans le log
        string casterName = caster != null ? caster.name : "(caster nul)";
        Debug.Log("Début de la séquence du MusicalMove: " + move + " de " + casterName);
        isActive = true;
        successResults = new List<bool>();

        // Position et rotation initiales du lanceur avant tout déplacement.
        // La rotation capturée ici (début de la phase de préparation) servira de
        // référence pour la caméra jusqu'à la fin du move, quel que soit le
        // nombre de timelines jouées.
        Vector3 originPosition = caster != null ? caster.transform.position : Vector3.zero;
        Quaternion initialRotation = caster != null ? caster.transform.rotation : Quaternion.identity;

        // Prépare les variables globales avant toute animation ou téléportation.
        // Des événements d'animation peuvent survenir très tôt et doivent
        // pouvoir accéder à ces références immédiatement.
        currentMove = move;
        currentCaster = caster;
        currentTarget = target;
        pendingNotes = move.notes != null ? move.notes.Count : 0;

        bool tauntPlayed = false;
        System.Action<CharacterUnit> deathHandler = null;
        deathHandler = (dead) =>
        {
            if (!tauntPlayed && isActive && caster != null)
            {
                StartCoroutine(PlayTauntWithDelay(caster.Data.prematureDeathTaunt, 1f));
                tauntPlayed = true;
            }
        };
        if (target != null)
        {
            target.OnDeath += deathHandler;
            target.PlayPrepareToUndergoAnimation();
        }       
        GameObject casterAnimatorGO = caster.GetCasterBindingTarget();
        // 🎯 La caméra se centre désormais directement sur l'unité ciblée du mouvement.
        // Si aucune cible n'est définie (attaque de zone, soin personnel, etc.),
        // on utilise l'Animator du lanceur pour conserver un ancrage valable.
        // 🧭 Préparation des points d'ancrage pour la caméra de combat.
        // - L'exécution doit suivre la cible directe du sort.
        // - Les phases de préparation et de repli se recentrent sur le lanceur
        //   pour garder une mise en scène cohérente.
        GameObject performingCameraTarget = null;
        if (target != null)
        {
            // On privilégie l'ancre animée de la cible pour obtenir un cadrage stable.
            performingCameraTarget = target.GetCasterBindingTarget();
            if (performingCameraTarget == null)
                performingCameraTarget = target.gameObject;
        }
        else
        {
            performingCameraTarget = casterAnimatorGO; // 🔁 Retombe sur le lanceur si aucune cible
        }
        GameObject casterCameraTarget = casterAnimatorGO; // 📌 Ancre sur le lanceur pour préparation et repli
        // Détermine si une timeline caméra couvrant toute l'action est disponible.
        // Ce système est remplacé par l'utilisation de caméras Cinemachine dédiées.
        bool useOverlay = false;

        // Configure le rig caméra avec les cibles du move avant de démarrer la mise en scène.
        BattleCameraManager.Instance?.ConfigureActionTargets(
            caster,
            target,
            null,
            casterCameraTarget != null ? casterCameraTarget.transform : null,
            performingCameraTarget != null ? performingCameraTarget.transform : null);

        // Éventuel délai de pré-animation.
        // 🔄 Cette attente se produit désormais AVANT toute timeline
        //     afin d'éviter un à-coup juste avant la téléportation.
        //     Elle laisse ainsi le temps de mettre en place des effets
        //     ou des annonces avant même le début de la préparation.
        if (move.startDelay > 0f)
            yield return new WaitForSeconds(move.startDelay);

        // L'ancienne timeline globale de caméra est désactivée.

        // --- Phase de préparation ---
        StartTimelinePhase(
            move.preparingTimeline,
            useOverlay,
            caster,
            casterAnimatorGO,
            casterCameraTarget,
            move.performingTimeline == null && move.retreatTimeline == null,
            initialRotation,
            move.preparingCameraRole);
        yield return WaitForTimelinePhase(move.preparingTimeline, useOverlay, caster);

        // Déplacement ou téléportation vers la cible si nécessaire.
        if (move.requiresMovement && caster != null && target != null)
            yield return MoveTo(caster, target, move);
        else if (caster != null && target != null)
        {
            // Sans déplacement, on oriente simplement le lanceur vers sa cible
            // Limitation de la rotation à l'axe Y pour éviter toute inclinaison
            Vector3 dir = target.transform.position - caster.transform.position;
            dir.y = 0f; // Ignore la différence de hauteur entre les deux unités
            dir = dir.normalized; // Normalisation après modification
            if (dir != Vector3.zero)
                caster.transform.forward = dir; // Applique uniquement la rotation horizontale
        }

        // --- Phase d'exécution ---
        // Le délai passé en paramètre différera uniquement la bascule de caméra,
        // laissant la timeline de performing démarrer immédiatement.
        StartTimelinePhase(
            move.performingTimeline,
            useOverlay,
            caster,
            casterAnimatorGO,
            performingCameraTarget,
            move.retreatTimeline == null,
            initialRotation,
            move.performingCameraRole,
            move.preparingToPerformingCameraDelay);

        if (pendingNotes == 0)
        {
            move.ApplyEffect(caster, target);
        }
        else
        {
            while (pendingNotes > 0)
            {
                float safeDelay = move.notes.Sum(n => n.rhythm);
                float timer = 0f;
                while (pendingNotes > 0 && timer < safeDelay)
                {
                    timer += Time.deltaTime;
                    yield return null;
                }
                if (pendingNotes > 0)
                {
                    Debug.LogWarning($"[MusicalMoveRoutine] {pendingNotes} note(s) non résolues pour {move.moveName}. Forçage de la suite.");
                    pendingNotes = 0;
                }
            }
        }

        yield return WaitForTimelinePhase(move.performingTimeline, useOverlay, caster);

        // --- Retour ou téléportation de repli ---
        if (move.requiresMovement && !move.stayInPlace && caster != null && target != null)
            yield return ReturnToInitialPosition(move, caster, target, originPosition);

        // --- Phase de repli ---
        StartTimelinePhase(
            move.retreatTimeline,
            useOverlay,
            caster,
            casterAnimatorGO,
            casterCameraTarget,
            true,
            initialRotation,
            move.retreatCameraRole);
        yield return WaitForTimelinePhase(move.retreatTimeline, useOverlay, caster);

        // Attente finale de la timeline caméra complète (désactivée).

        isActive = false;
        bool critical = successResults != null && successResults.Count > 0 && successResults.All(s => s);
        NewBattleManager.Instance.AfterMusicalMove(move, caster, critical);

        if (target != null)
            target.OnDeath -= deathHandler;

        currentMove = null;
        currentCaster = null;
        currentTarget = null;

        // Restaure la caméra par défaut en fin de move.
        // Avant de restaurer la caméra par défaut, on s'assure qu'aucun délai
        // en attente ne viendra réactiver un rôle précédent inopinément.
        CancelPendingCameraSwitch();
        BattleCameraManager.Instance?.SwitchToCamera(BattleCameraRole.None);
        BattleCameraManager.Instance?.ClearRigTargets();

        // Nettoie la barre de QTE une fois la séquence terminée
        ClearQTEBar();

        // Si le caster a été détruit durant la séquence, on évite une exception
        casterName = caster != null ? caster.name : "(caster nul)";
        Debug.Log("Fin de la séquence du MusicalMove: " + move + " de " + casterName);
    }

    /// <summary>
    /// Gère la séquence d'utilisation d'un objet en combat.
    /// Cette routine reprend le même déroulé qu'un MusicalMove :
    /// lecture d'une éventuelle introduction, déplacement optionnel,
    /// animation principale puis retour à la position initiale.
    /// </summary>
    public IEnumerator ItemRoutine(ItemData item, CharacterUnit caster, CharacterUnit target)
    {
        // Préserve la sécurité si jamais le caster est détruit avant l'appel
        string itemCasterName = caster != null ? caster.name : "(caster nul)";
        Debug.Log($"Début de la séquence d'utilisation de l'objet: {item.itemName} par {itemCasterName}");
        isActive = true;

        // Position et rotation de départ du lanceur avant toute animation.
        // La rotation enregistrée ici au tout début de la phase de préparation
        // est réutilisée pour l'intégralité de l'utilisation de l'objet afin de
        // conserver une orientation de caméra stable.
        Vector3 originPosition = caster != null ? caster.transform.position : Vector3.zero;
        Quaternion initialRotation = caster != null ? caster.transform.rotation : Quaternion.identity;

        // Prépare la barre de QTE correspondant au motif de l'objet
        if (item.beatPattern != null && item.beatPattern.Count > 0)
            PrepareQTEBar(item.beatPattern);

        if (caster == null || caster.IsDead)
        {
            isActive = false;
            yield break;
        }

        
        Animator animator = caster.GetCasterAnimator();
        GameObject casterAnimatorGO = animator != null ? animator.gameObject : null;
        // 🎯 La caméra se positionne directement sur l'unité ciblée par l'objet.
        // En l'absence de cible (consommable global, soin personnel...),
        // on conserve le lanceur comme ancre pour éviter un décalage brusque.
        // 🧭 Détermination des cibles de caméra pour les différentes phases :
        //   * exécution -> focus sur la cible de l'objet si elle existe ;
        //   * préparation et repli -> recentrage sur le lanceur.
        GameObject performingCameraTarget = null;
        if (target != null)
        {
            performingCameraTarget = target.GetCasterBindingTarget();
            if (performingCameraTarget == null)
                performingCameraTarget = target.gameObject;
        }
        else
        {
            performingCameraTarget = casterAnimatorGO; // 🔁 Retombe sur le lanceur en absence de cible
        }
        GameObject casterCameraTarget = casterAnimatorGO; // 📌 Référence fixe sur le lanceur

        // L'ancien système de timeline caméra est remplacé par les caméras Cinemachine.
        bool useOverlay = false;
        // Lancement de timeline globale désactivé.

        BattleCameraManager.Instance?.ConfigureActionTargets(
            caster,
            target,
            null,
            casterCameraTarget != null ? casterCameraTarget.transform : null,
            performingCameraTarget != null ? performingCameraTarget.transform : null);

        // --- Phase de préparation ---
        StartTimelinePhase(
            item.preparingTimeline,
            useOverlay,
            caster,
            casterAnimatorGO,
            casterCameraTarget,
            item.performingTimeline == null && item.retreatTimeline == null,
            initialRotation,
            item.preparingCameraRole);
        yield return WaitForTimelinePhase(item.preparingTimeline, useOverlay, caster);

        // Déplacement ou téléportation éventuel vers la cible.
        if (item.requiresMovement && caster != null && target != null)
            yield return SimpleMoveTo(caster, target, item);
        else if (caster != null && target != null)
        {
            // Sans déplacement, on oriente simplement l'utilisateur vers sa cible
            // Limite la rotation à l'axe Y pour éviter les inclinaisons verticales
            Vector3 dir = target.transform.position - caster.transform.position;
            dir.y = 0f; // Neutralise l'écart de hauteur
            dir = dir.normalized; // Normalisation après suppression de la composante verticale
            if (dir != Vector3.zero)
                caster.transform.forward = dir; // Applique la rotation uniquement sur le plan horizontal
        }

        // --- Phase d'utilisation ---
        // Comme pour les MusicalMoves, le délai fourni retarde uniquement le changement
        // de caméra sans bloquer l'exécution de la timeline principale.
        StartTimelinePhase(
            item.performingTimeline,
            useOverlay,
            caster,
            casterAnimatorGO,
            performingCameraTarget,
            item.retreatTimeline == null,
            initialRotation,
            item.performingCameraRole,
            item.preparingToPerformingCameraDelay);

        // QTE associé à l'objet durant l'utilisation.
        LastItemSuccess = true;
        if (item.beatPattern != null && item.beatPattern.Count > 0)
        {
            successResults = new List<bool>();
            foreach (float beat in item.beatPattern)
            {
                bool s = false;
                yield return WaitForQTE(beat, null, Vector2.zero, r => s = r);
                successResults.Add(s);
            }
            LastItemSuccess = successResults.All(v => v);
        }

        yield return WaitForTimelinePhase(item.performingTimeline, useOverlay, caster);

        // --- Retour à la position d'origine ---
        if (item.requiresMovement && !item.stayInPlace && caster != null && target != null)
            yield return SimpleReturnToInitialPosition(caster, target, item, originPosition);

        // --- Phase de repli ---
        StartTimelinePhase(
            item.retreatTimeline,
            useOverlay,
            caster,
            casterAnimatorGO,
            casterCameraTarget,
            true,
            initialRotation,
            item.retreatCameraRole);
        yield return WaitForTimelinePhase(item.retreatTimeline, useOverlay, caster);

        isActive = false;
        // Protection en cas de destruction du lanceur avant la fin de la séquence
        itemCasterName = caster != null ? caster.name : "(caster nul)";
        Debug.Log($"Fin de la séquence d'utilisation de l'objet: {item.itemName} par {itemCasterName}");

        // Nettoie la barre de QTE utilisée pour l'objet
        ClearQTEBar();

        // Retour à la caméra par défaut.
        // Comme pour les MusicalMoves, on neutralise toute attente résiduelle avant
        // de rendre la main à la caméra principale.
        CancelPendingCameraSwitch();
        BattleCameraManager.Instance?.SwitchToCamera(BattleCameraRole.None);
        BattleCameraManager.Instance?.ClearRigTargets();
    }

    /// <summary>
    /// Instancie (si disponible) le système de particules de dash et l'attache à l'unité.
    /// Cette méthode centralise l'initialisation pour s'assurer que le même comportement
    /// est appliqué lors des déplacements d'objets comme des MusicalMoves.
    /// </summary>
    /// <param name="caster">L'unité qui se déplace actuellement.</param>
    /// <returns>Une instance de particules prête à être suivie ou <c>null</c> si aucun prefab n'est configuré.</returns>
    private ParticleSystem SpawnDashParticles(CharacterUnit caster)
    {
        if (caster == null)
            return null; // Sécurité minimale : la méthode n'a pas vocation à gérer un déplacement sans unité.

        // Chaque personnage peut disposer de son propre effet de dash défini dans son CharacterData.
        // On récupère donc la référence à chaud pour respecter la personnalisation visuelle demandée.
        CharacterData casterData = caster.Data;
        ParticleSystem dashParticlesPrefab = casterData != null ? casterData.dashParticlesPrefab.GetComponentInChildren<ParticleSystem>() : null;

        if (dashParticlesPrefab == null)
            return null; // Aucun effet configuré pour cette unité : aucun système n'est créé.

        // Instancie le prefab directement à la position du lanceur pour éviter tout décalage visuel.
        ParticleSystem instance = Instantiate(dashParticlesPrefab, caster.transform.position, caster.transform.rotation);

        // Lecture optionnelle du son associé : priorité au clip personnalisé du personnage,
        // sinon utilisation d'un son par défaut défini dans le manager.
        AudioClipSO dashClip = casterData != null && casterData.dashSoundClip != null
            ? casterData.dashSoundClip
            : defaultDashSound;
        PlaySfx(dashClip);

        // Parentage temporaire : le système suit automatiquement l'unité pendant le déplacement.
        Transform instanceTransform = instance.transform;
        instanceTransform.SetParent(caster.transform, worldPositionStays: false);
        instanceTransform.localPosition = Vector3.zero; // Positionne l'effet au centre du personnage.
        instanceTransform.localRotation = Quaternion.identity; // Conserve l'orientation naturelle du prefab.
        instanceTransform.localScale = Vector3.one; // Évite tout effet d'échelle hérité inattendu.

        instance.Play(true); // Sécurise le lancement de l'effet (utile si le prefab est configuré sur "Stop").
        return instance;
    }

    /// <summary>
    /// Détache le système de particules du lanceur et lance une extinction progressive
    /// suivie de sa destruction pour éviter toute fuite de GameObject dans la scène.
    /// </summary>
    /// <param name="instance">Instance des particules précédemment créées.</param>
    private void ReleaseDashParticles(ParticleSystem instance)
    {
        if (instance == null)
            return; // Rien à relâcher : le déplacement s'est effectué sans effet visuel.

        Transform instanceTransform = instance.transform;
        if (instanceTransform != null)
        {
            // Le détachement permet de laisser l'effet se dissiper sur place même si l'unité repart immédiatement.
            instanceTransform.SetParent(null, worldPositionStays: true);
        }

        // La coroutine gère la réduction de l'émission sur 0,5 s puis la destruction après 2 s.
        StartCoroutine(FadeOutAndDestroyDashParticles(instance));
    }

    /// <summary>
    /// Réduit progressivement l'émission d'un système de particules puis le détruit
    /// afin de respecter les consignes de lisibilité en combat sans générer de déchets.
    /// </summary>
    /// <param name="instance">Système de particules à éteindre.</param>
    private IEnumerator FadeOutAndDestroyDashParticles(ParticleSystem instance)
    {
        if (instance == null)
            yield break; // Sécurité supplémentaire : l'effet peut déjà avoir été détruit ailleurs.

        // Multiplieurs de départ : ils sont utilisés pour conserver la configuration du prefab
        // (courbes, bursts, etc.) tout en la faisant décroître progressivement.
        var emission = instance.emission;
        float baseRateTimeMultiplier = emission.rateOverTimeMultiplier;
        float baseRateDistanceMultiplier = emission.rateOverDistanceMultiplier;

        const float fadeDuration = 0.5f; // Durée souhaitée pour atteindre une émission nulle.
        float timer = 0f;

        while (instance != null && timer < fadeDuration)
        {
            float factor = 1f - (timer / fadeDuration); // Interpolation linéaire de 1 vers 0.

            emission = instance.emission; // Récupération à chaque itération car il s'agit d'une struct.
            emission.rateOverTimeMultiplier = baseRateTimeMultiplier * factor;
            emission.rateOverDistanceMultiplier = baseRateDistanceMultiplier * factor;

            timer += Time.deltaTime;
            yield return null; // Attente d'une frame pour conserver une animation fluide.
        }

        if (instance == null)
            yield break; // L'effet a pu être détruit durant l'attente (ex : changement de scène).

        emission = instance.emission;
        emission.rateOverTimeMultiplier = 0f;
        emission.rateOverDistanceMultiplier = 0f;
        instance.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting); // Stoppe proprement l'émission.

        yield return new WaitForSeconds(2f); // Laisse le temps aux particules déjà générées de disparaître naturellement.

        if (instance != null)
            Destroy(instance.gameObject); // Suppression finale pour éviter toute accumulation en scène.
    }

    private IEnumerator SimpleMoveTo(CharacterUnit caster, CharacterUnit target, ItemData item)
    {
        if (caster == null || caster.IsDead)
            yield break;

        // Destination calculée selon la portée de l'objet
        Vector3 destination = target.transform.position + target.transform.forward * item.castDistance;
        Animator animator = caster.GetCasterAnimator();
        // Récupère le GameObject qui porte l'Animator pour pouvoir le masquer
        GameObject visualRoot = animator != null ? animator.gameObject : null;

        bool isTeleport = item.moveSpeed <= 0f; // Téléportation si vitesse nulle ou négative
        float distanceToDestination = Vector3.Distance(caster.transform.position, destination); // Distance à parcourir

        // Téléportation uniquement si la destination est différente de la position actuelle
        if (isTeleport && distanceToDestination > 0.01f)
        {
            // Lecture des effets de départ
            if (item.tpSFx_Start != null)
                PlaySfx(item.tpSFx_Start);
            if (item.tpVfx_Start != null)
                Instantiate(item.tpVfx_Start, caster.transform.position, Quaternion.identity);

            // Animation de déplacement
            if (!caster.IsDead)
                animator?.Play("Dash_Battle");

            // Cache le visuel le temps de la téléportation
            if (visualRoot != null)
                visualRoot.SetActive(false);

            // Délai configurable pour laisser apparaître l'effet de téléport
            float delay = item.teleportDelay >= 0f ? item.teleportDelay : defaultTeleportDelay;
            yield return new WaitForSeconds(delay);

            // Téléportation instantanée
            caster.transform.position = destination;

            // Réaffiche le personnage à la nouvelle position
            if (visualRoot != null)
                visualRoot.SetActive(true);

            if (item.tpVfx_End != null)
                Instantiate(item.tpVfx_End, destination, Quaternion.identity);
            if (item.tpSFx_End != null)
                PlaySfx(item.tpSFx_End);
        }
        else if (!isTeleport)
        {
            // Animation du dash
            if (!caster.IsDead)
                animator?.Play("Dash_Battle");

            Vector3 startPos = caster.transform.position;
            float distance = Vector3.Distance(startPos, destination);
            // Instancie les particules uniquement si un déplacement réel est nécessaire.
            ParticleSystem dashParticles = distance > 0.01f ? SpawnDashParticles(caster) : null;
            float duration = distance / item.moveSpeed;
            float t = 0f;
            // Mouvement progressif vers la destination
            while (t < 1f)
            {
                caster.transform.position = Vector3.Lerp(startPos, destination, t);
                t += Time.deltaTime / Mathf.Max(duration, 0.0001f);
                yield return null;
            }
            caster.transform.position = destination;

            // Laisse l'effet se dissiper en douceur maintenant que l'unité est arrivée.
            ReleaseDashParticles(dashParticles);
        }
        // Si isTeleport mais distance nulle, on ne fait rien : aucune téléportation nécessaire

        // Orientation vers la cible
        // Conserve uniquement la rotation horizontale pour éviter les pivots en X
        Vector3 dir = target.transform.position - caster.transform.position;
        dir.y = 0f; // Ignore la composante verticale
        dir = dir.normalized; // Normalisation pour obtenir une direction unitaire
        if (dir != Vector3.zero)
            caster.transform.forward = dir; // Applique l'orientation sur l'axe Y uniquement

        yield return null;
    }

    // Retourne le lanceur à la position qu'il occupait au début de l'utilisation de l'objet
    private IEnumerator SimpleReturnToInitialPosition(CharacterUnit caster, CharacterUnit target, ItemData item, Vector3 originPosition)
    {
        if (caster == null || caster.IsDead)
            yield break;

        Vector3 origin = originPosition;
        Animator animator = caster.GetCasterAnimator();
        // GameObject contenant l'Animator à masquer pendant la téléportation
        GameObject visualRoot = animator != null ? animator.gameObject : null;
        bool isTeleport = item.moveSpeed <= 0f; // Téléportation si vitesse nulle
        float distanceToOrigin = Vector3.Distance(caster.transform.position, origin); // Distance de retour

        // Téléportation uniquement si le retour nécessite un déplacement
        if (isTeleport && distanceToOrigin > 0.01f)
        {
            if (item.tpSFx_Start != null)
                PlaySfx(item.tpSFx_Start);
            if (item.tpVfx_Start != null)
                Instantiate(item.tpVfx_Start, caster.transform.position, Quaternion.identity);

            if (!caster.IsDead)
                animator?.Play("Retreat_Battle");

            // Cache le GameObject visuel pendant l'attente
            if (visualRoot != null)
                visualRoot.SetActive(false);

            // Délai configurable avant la réapparition à la position initiale
            float delay = item.teleportDelay >= 0f ? item.teleportDelay : defaultTeleportDelay;
            yield return new WaitForSeconds(delay);

            caster.transform.position = origin;

            // Réaffiche le personnage une fois la téléportation effectuée
            if (visualRoot != null)
                visualRoot.SetActive(true);

            if (item.tpVfx_End != null)
                Instantiate(item.tpVfx_End, origin, Quaternion.identity);
            if (item.tpSFx_End != null)
                PlaySfx(item.tpSFx_End);
        }
        else if (!isTeleport)
        {
            if (!caster.IsDead)
                animator?.Play("Retreat_Battle");

            Vector3 startPos = caster.transform.position;
            float distance = Vector3.Distance(startPos, origin);
            ParticleSystem dashParticles = distance > 0.01f ? SpawnDashParticles(caster) : null;
            float duration = distance / item.moveSpeed;
            float t = 0f;
            while (t < 1f)
            {
                caster.transform.position = Vector3.Lerp(startPos, origin, t);
                t += Time.deltaTime / Mathf.Max(duration, 0.0001f);
                yield return null;
            }
            caster.transform.position = origin;

            ReleaseDashParticles(dashParticles);
        }
        // Si isTeleport mais distance nulle, aucune action n'est nécessaire

        // Orientation optionnelle vers la cible
        // Limite la rotation au plan horizontal pour conserver l'orientation naturelle du personnage
        if (target != null)
        {
            Vector3 lookDir = target.transform.position - caster.transform.position;
            lookDir.y = 0f; // Ignore la différence de hauteur avec la cible
            lookDir = lookDir.normalized; // Normalisation après suppression de l'axe vertical
            if (lookDir != Vector3.zero)
                caster.transform.forward = lookDir; // Rotation uniquement autour de l'axe Y
        }

        yield return null;
    }

    private IEnumerator MoveTo(CharacterUnit caster, CharacterUnit target, MusicalMoveSO move)
    {
        if (caster == null || caster.IsDead)
            yield break;

        // Journalisation sécurisée en cas de destruction d'une des unités
        string moveCasterName = caster != null ? caster.name : "(caster nul)";
        string moveTargetName = target != null ? target.name : "(cible nulle)";
        Debug.Log("Déplacement de " + moveCasterName + " vers " + moveTargetName);

        Vector3 startPosition = caster.transform.position;

        // Calcul de la position cible en fonction de la position relative
        Vector3 offsetDir = target.transform.forward;
        switch (move.relativePosition)
        {
            case RelativePosition.Back:
                offsetDir = -target.transform.forward;
                break;
            case RelativePosition.Left:
                offsetDir = -target.transform.right;
                break;
            case RelativePosition.Right:
                offsetDir = target.transform.right;
                break;
        }

        float mobilityBonus = caster.currentMobility;
        Vector3 targetPos = target.transform.position + offsetDir * (move.castDistance + mobilityBonus);

        Animator animator = caster.GetCasterAnimator();
        // Stocke le GameObject visuel pour pouvoir le désactiver durant la téléportation
        GameObject visualRoot = animator != null ? animator.gameObject : null;
        bool isTeleport = move.ShouldTeleportToTarget(); // Téléportation si le temps demandé est nul ou si l'ancien move utilisait une vitesse nulle
        float distanceToTarget = Vector3.Distance(caster.transform.position, targetPos); // Distance à la cible

        // Téléportation uniquement si la cible est différente de la position actuelle
        if (isTeleport && distanceToTarget > 0.01f)
        {
            if (move.tpSFx_Start != null)
                PlaySfx(move.tpSFx_Start);
            if (move.tpVfx_Start != null)
                Instantiate(move.tpVfx_Start, caster.transform.position, Quaternion.identity);

            if (!caster.IsDead)
                animator?.Play("Dash_Battle");

            // Masque le visuel du lanceur pendant l'attente
            if (visualRoot != null)
                visualRoot.SetActive(false);

            // Délai configurable pour la téléportation du MusicalMove
            float delay = move.teleportDelay >= 0f ? move.teleportDelay : defaultTeleportDelay;
            yield return new WaitForSeconds(delay);

            caster.transform.position = targetPos;

            // Réactive l'apparence du personnage après la téléportation
            if (visualRoot != null)
                visualRoot.SetActive(true);

            if (move.tpVfx_End != null)
                Instantiate(move.tpVfx_End, targetPos, Quaternion.identity);
            if (move.tpSFx_End != null)
                PlaySfx(move.tpSFx_End);
        }
        else if (!isTeleport)
        {
            if (!caster.IsDead)
                animator?.Play("Dash_Battle");

            Vector3 startPos = caster.transform.position;
            float distance = Vector3.Distance(startPos, targetPos);
            ParticleSystem dashParticles = distance > 0.01f ? SpawnDashParticles(caster) : null;
            float duration = move.GetTravelDuration(distance);
            float t = 0f;
            while (t < 1f)
            {
                caster.transform.position = Vector3.Lerp(startPos, targetPos, t);
                t += Time.deltaTime / Mathf.Max(duration, 0.0001f);
                yield return null;
            }
            caster.transform.position = targetPos;

            ReleaseDashParticles(dashParticles);
        }
        // Si isTeleport mais aucune distance à parcourir, on ignore le déplacement

        Vector3 lookDir = Vector3.zero;
        if (target != null)
        {
            lookDir = target.transform.position - caster.transform.position;
            lookDir.y = 0f; // Ignore l'écart de hauteur
            lookDir = lookDir.normalized;
        }

        // Ajuste également la direction d'offset pour éviter tout pivot vertical
        if (offsetDir != Vector3.zero)
        {
            offsetDir.y = 0f;
            offsetDir = offsetDir.normalized;
        }

        if (lookDir != Vector3.zero)
            caster.transform.forward = lookDir; // Priorité à la direction vers la cible
        else if (offsetDir != Vector3.zero)
            caster.transform.forward = offsetDir; // Sinon on utilise la direction relative (horizontalement)

        yield return null;
        moveCasterName = caster != null ? caster.name : "(caster nul)";
        Debug.Log("Fin du déplacement de " + moveCasterName);
    }

    // Replace le lanceur exactement à la position occupée avant le début du move
    private IEnumerator ReturnToInitialPosition(MusicalMoveSO move, CharacterUnit caster, CharacterUnit target, Vector3 originPosition)
    {
        // Peut être appelé alors que l'unité a été détruite
        string returnCasterName = caster != null ? caster.name : "(caster nul)";
        Debug.Log("Retour de " + returnCasterName + " vers sa position initiale");

        if (caster == null || caster.IsDead)
        {
            Debug.LogWarning("ReturnToInitialPosition : caster nul ou détruit à l'appel");
            yield break;
        }

        Vector3 startPos = caster.transform.position;
        Vector3 initialPosition = originPosition;
        Animator animator = caster.GetCasterAnimator();
        // GameObject contenant l'Animator à désactiver durant la téléportation
        GameObject visualRoot = animator != null ? animator.gameObject : null;
        bool isTeleport = move.ShouldTeleportToTarget(); // Téléportation si le temps demandé est nul ou si l'ancien move utilisait une vitesse nulle
        float distanceToInitial = Vector3.Distance(caster.transform.position, initialPosition); // Distance à parcourir

        // Téléportation uniquement si nécessaire
        if (isTeleport && distanceToInitial > 0.01f)
        {
            if (move.tpSFx_Start != null)
                PlaySfx(move.tpSFx_Start);
            if (move.tpVfx_Start != null)
                Instantiate(move.tpVfx_Start, caster.transform.position, Quaternion.identity);

            if (!caster.IsDead)
                animator?.Play("Dash_Battle");

            // Cache temporairement le GameObject visuel
            if (visualRoot != null)
                visualRoot.SetActive(false);

            // Délai configurable avant de revenir à la position de départ
            float delay = move.teleportDelay >= 0f ? move.teleportDelay : defaultTeleportDelay;
            yield return new WaitForSeconds(delay);

            if (caster == null)
            {
                Debug.LogWarning("ReturnToInitialPosition : caster détruit pendant le délai");
                yield break;
            }

            caster.transform.position = initialPosition;

            // Réaffiche le personnage à sa position initiale
            if (visualRoot != null)
                visualRoot.SetActive(true);

            if (move.tpVfx_End != null)
                Instantiate(move.tpVfx_End, initialPosition, Quaternion.identity);
            if (move.tpSFx_End != null)
                PlaySfx(move.tpSFx_End);
        }
        else if (!isTeleport)
        {
            if (!caster.IsDead)
                animator?.Play("Dash_Battle");

            float distance = Vector3.Distance(startPos, initialPosition);
            ParticleSystem dashParticles = distance > 0.01f ? SpawnDashParticles(caster) : null;
            float duration = move.GetTravelDuration(distance);
            float t = 0f;
            while (t < 1f)
            {
                caster.transform.position = Vector3.Lerp(startPos, initialPosition, t);
                t += Time.deltaTime / Mathf.Max(duration, 0.0001f);
                yield return null;
            }
            caster.transform.position = initialPosition;

            ReleaseDashParticles(dashParticles);
        }
        // Si isTeleport mais distance nulle, pas de retour nécessaire

        yield return null;

        if (move.stayFaceToTarget && target != null)
        {
            Vector3 finalDirToTarget = target.transform.position - caster.transform.position;
            finalDirToTarget.y = 0f; // Ignore la hauteur de la cible
            finalDirToTarget = finalDirToTarget.normalized;
            if (finalDirToTarget != Vector3.zero)
                caster.transform.forward = finalDirToTarget; // Rotation horizontale vers la cible
        }
        else
        {
            Vector3 finalDirToParent = initialPosition - caster.transform.position;
            finalDirToParent.y = 0f; // Conserve uniquement la composante horizontale
            finalDirToParent = finalDirToParent.normalized;
            if (finalDirToParent != Vector3.zero)
                caster.transform.forward = finalDirToParent; // Retourne vers sa position initiale sans inclinaison
        }
        Debug.Log("Le caster a terminé son retour.");
    }

    IEnumerator PlayMoveAnimations(string[] animationClips, CharacterUnit caster)
    {
        // Récupère une fois l’Animator plutôt que de l’appeler à chaque itération
        Animator animator = caster.GetCasterAnimator();

        foreach (string clip in animationClips)
        {
            // Lance le clip courant uniquement si l'unité est en vie
            if (!caster.IsDead)
                animator.Play(clip);
            Debug.Log("Animation jouée : " + clip);

            // On attend un frame pour laisser l’Animator passer à l’état du clip
            yield return null;

            // Récupère la longueur de l’état actuel (le clip qui vient d'être joué)
            float clipDuration = animator.GetCurrentAnimatorStateInfo(0).length;

            // Si tu veux être certain de prendre la longueur du AnimationClip lui-même,
            // tu peux aussi faire : float clipDuration = clip.length;
            yield return new WaitForSeconds(clipDuration);
        }

        Debug.Log("Toutes les animations sont terminées.");
    }

    public void TriggerNote(int index)
    {
        if (currentMove == null || currentCaster == null || currentTarget == null)
            return;
        if (currentMove.notes == null || index < 0 || index >= currentMove.notes.Count)
            return;

        var note = currentMove.notes[index];

        // Si l'attaquant est un ennemi
        if (currentCaster.Data.characterType == CharacterType.EnemyUnit)
        {
            // Si l'ennemi est marqué comme inévitable, aucun QTE défensif n'est proposé
            if (currentCaster.Data.avoidable)
            {
                currentMove.ApplyEffect(currentCaster, currentTarget);
                pendingNotes = Mathf.Max(0, pendingNotes - 1);
            }
            else
            {
                StartCoroutine(WaitForDefenseQTE(result =>
                {
                    defenseResult = result;
                    switch (result)
                    {
                        case DefenseResult.Parry:
                            currentTarget.TakeParry();
                            break;
                        case DefenseResult.Dodge:
                            currentTarget.TakeDodge();
                            break;
                        default:
                            currentMove.ApplyEffect(currentCaster, currentTarget);
                            break;
                    }

                    pendingNotes = Mathf.Max(0, pendingNotes - 1);
                }));
            }
        }
        else
        {
            // Pas d'icône spécifique pour les notes, on passe null
            StartCoroutine(WaitForQTE(note.rhythm, null, Vector2.zero, success =>
            {
                currentMove.ApplyEffect(currentCaster, currentTarget, success);
                successResults.Add(success);
                pendingNotes = Mathf.Max(0, pendingNotes - 1);
            }));
        }
    }

    public void TriggerQTE(float windowDelay)
    {
        // Appel historique sans icône
        StartCoroutine(WaitForQTE(windowDelay, null, Vector2.zero, _ => { }));
    }

    /// <summary>
    /// Lance un QTE en précisant l'icône à afficher au centre du cercle.
    /// </summary>
    /// <param name="windowDelay">Durée de la fenêtre en millisecondes</param>
    /// <param name="icon">Icône de l'input à afficher</param>
    public void TriggerQTE(float windowDelay, Sprite icon)
    {
        StartCoroutine(WaitForQTE(windowDelay, icon, Vector2.zero, _ => { }));
    }

    /// <summary>
    /// Lance un QTE en précisant icône et position de l'affichage.
    /// </summary>
    /// <param name="windowDelay">Durée de la fenêtre en millisecondes</param>
    /// <param name="icon">Icône de l'input à afficher</param>
    /// <param name="position">Position du visuel dans le canvas</param>
    public void TriggerQTE(float windowDelay, Sprite icon, Vector2 position)
    {
        StartCoroutine(WaitForQTE(windowDelay, icon, position, _ => { }));
    }

    private IEnumerator WaitForQTE(float windowDelay, Sprite icon, Vector2 position, System.Action<bool> callback)
    {
        qteActive = true;
        float slowestTimeScale = 0f;
        float transitionDuration = 0.1f;
        float holdDuration = windowDelay / 1000f; // convertit en secondes
        float normalTimeScale = 1f;

        // 🔻 Ralentissement progressif uniquement en mode facile
        float t = 0f;
        if (easyMode)
        {
            while (t < transitionDuration)
            {
                t += Time.unscaledDeltaTime;
                float blend = t / transitionDuration;
                Time.timeScale = Mathf.Lerp(normalTimeScale, slowestTimeScale, blend);
                Time.fixedDeltaTime = defaultFixedDeltaTime * Time.timeScale;
                yield return null;
            }

            // 🎯 Temps ralenti & instanciation du visuel
            Time.timeScale = slowestTimeScale;
            Time.fixedDeltaTime = defaultFixedDeltaTime * slowestTimeScale;
        }

        GameObject qteVisualGO;
        QTEBar qteBar = null;
        UnityEngine.UI.Image noteImage = null;
        UnityEngine.UI.Image delayFillImage = null;
        UnityEngine.UI.Image iconImage = null;

        // Préférence : utiliser la barre Guitar Hero si une barre est préparée ou un prefab fourni
        if (activeQTEBar != null || qteBarPrefab != null)
        {
            qteBar = activeQTEBar;
            if (qteBar == null)
            {
                qteVisualGO = Instantiate(qteBarPrefab, qteUIParent);
                var rect = qteVisualGO.GetComponent<RectTransform>();
                if (rect != null)
                    rect.anchoredPosition = position;

                qteBar = qteVisualGO.GetComponent<QTEBar>();
                activeQTEBar = qteBar;
            }
            else
            {
                qteVisualGO = qteBar.gameObject;
            }

            if (preparedNotes.Count > 0)
                noteImage = preparedNotes.Dequeue();
            else if (qteBar != null)
                noteImage = qteBar.ScheduleNote(icon, 0f, noteAdvanceTime);
        }
        else
        {
            qteVisualGO = Instantiate(qteCirclePrefab, qteUIParent);
            var visualRect = qteVisualGO.GetComponent<RectTransform>();
            if (visualRect != null)
                visualRect.anchoredPosition = position;

            QTECircleUI qteVisual = qteVisualGO.GetComponent<QTECircleUI>();
            if (qteVisual != null)
            {
                delayFillImage = qteVisual.DelayFillImage;
                iconImage = qteVisual.InputIconImage;
            }
        }

        // Valeur initiale à 0 pour remplir progressivement sur la durée du QTE.
        if (delayFillImage != null)
        {
            delayFillImage.fillAmount = 0f;
        }

        // Pour le cercle historique, on applique l'icône manuellement
        if (icon != null && qteBar == null)
        {
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(true);
                iconImage.sprite = icon;
            }
            else
            {
                // Fallback si aucun Image n'est référencé
                var iconGO = new GameObject("InputIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                iconGO.transform.SetParent(qteVisualGO.transform, false);
                var rect = iconGO.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(64f, 64f);
                var img = iconGO.GetComponent<UnityEngine.UI.Image>();
                img.sprite = icon;
                iconImage = img;
            }
        }

        float elapsed = 0f;
        bool success = false;
        var confirm = InputsManager.Instance.playerInputs.Battle.Confirm;
        confirm.Enable();

        while (easyMode || elapsed < holdDuration)
        {
            float unscaledDelta = Time.unscaledDeltaTime;
            elapsed += unscaledDelta;

            float progress = Mathf.Clamp01(elapsed / holdDuration);

            // Mise à jour du cercle historique
            if (delayFillImage != null)
            {
                delayFillImage.fillAmount = progress;
            }

            if (confirm.triggered)
            {
                if (qteBar != null)
                    success = qteBar.IsNoteInValidationZone(noteImage);
                else
                    success = true;
                break;
            }

            yield return null;
        }

        confirm.Disable();

        // Évalue la position où afficher l'effet de réussite ou d'échec
        Vector3 effectPosition = Vector3.zero;
        if (activeQTEBar != null)
        {
            var zone = activeQTEBar.ValidationZone;
            effectPosition = zone != null ? zone.position : activeQTEBar.transform.position;
        }
        else if (qteVisualGO != null)
        {
            effectPosition = qteVisualGO.transform.position;
        }

        // Détruit uniquement la barre si elle n'est pas réutilisée
        if (qteVisualGO != null && (activeQTEBar == null || qteVisualGO != activeQTEBar.gameObject))
            Destroy(qteVisualGO);

        // Supprime la note utilisée pour libérer l'espace
        if (noteImage != null)
            Destroy(noteImage.gameObject);

        callback?.Invoke(success);

        // Affichage visuel du résultat
        GameObject effect = success ? successEffectPrefab : failEffectPrefab;
        if (effect != null && effectPosition != Vector3.zero)
            Instantiate(effect, effectPosition, Quaternion.identity, qteUIParent);

        // Lecture du son de réussite ou d'échec si disponible
        if (success)
            AudioManager.Instance?.PlaySound(successSFX);
        else
            AudioManager.Instance?.PlaySound(failSFX);

        // 🔺 Retour au temps normal uniquement si le temps a été modifié
        if (easyMode)
        {
            t = 0f;
            while (t < transitionDuration)
            {
                t += Time.unscaledDeltaTime;
                float blend = t / transitionDuration;
                Time.timeScale = Mathf.Lerp(slowestTimeScale, normalTimeScale, blend);
                Time.fixedDeltaTime = defaultFixedDeltaTime * Time.timeScale;
                yield return null;
            }

            Time.timeScale = normalTimeScale;
            Time.fixedDeltaTime = defaultFixedDeltaTime;
        }
        qteActive = false;
    }

    /// <summary>
    /// Variante de QTE dédiée à la défense contre une attaque ennemie.
    /// Retourne Parade, Esquive ou Échec selon le timing de l'input.
    /// </summary>
    private IEnumerator WaitForDefenseQTE(System.Action<DefenseResult> callback)
    {
        qteActive = true;
        const float parryWindow = 0.1f; // Temps pour une parade parfaite
        const float dodgeWindow = 0.2f; // Temps total pour réussir une esquive

        float slowestTimeScale = 0f;
        float transitionDuration = 0.1f;
        float normalTimeScale = 1f;

        // 🔻 Ralentissement progressif uniquement en mode facile
        float t = 0f;
        if (easyMode)
        {
            while (t < transitionDuration)
            {
                t += Time.unscaledDeltaTime;
                float blend = t / transitionDuration;
                Time.timeScale = Mathf.Lerp(normalTimeScale, slowestTimeScale, blend);
                Time.fixedDeltaTime = defaultFixedDeltaTime * Time.timeScale;
                yield return null;
            }

            // 🎯 Temps ralenti & instanciation du visuel
            Time.timeScale = slowestTimeScale;
            Time.fixedDeltaTime = defaultFixedDeltaTime * slowestTimeScale;
        }

        GameObject qteVisualGO;
        QTEBar qteBar = null;
        UnityEngine.UI.Image noteImage = null;
        UnityEngine.UI.Image delayFillImage = null;

        if (activeQTEBar != null)
        {
            qteBar = activeQTEBar;
            qteVisualGO = qteBar.gameObject;
            if (preparedNotes.Count > 0)
                noteImage = preparedNotes.Dequeue();
            else
                noteImage = qteBar.ScheduleNote(null, 0f, noteAdvanceTime);
        }
        else
        {
            qteVisualGO = Instantiate(qteCirclePrefab, qteUIParent);
            QTECircleUI qteVisual = qteVisualGO.GetComponent<QTECircleUI>();
            delayFillImage = qteVisual != null ? qteVisual.DelayFillImage : null;
            if (delayFillImage != null)
                delayFillImage.fillAmount = 0f;
        }

        float elapsed = 0f;
        bool pressed = false;
        var confirm = InputsManager.Instance.playerInputs.Battle.Confirm;
        confirm.Enable();

        while (elapsed < dodgeWindow)
        {
            float unscaledDelta = Time.unscaledDeltaTime;
            elapsed += unscaledDelta;

            if (delayFillImage != null)
                delayFillImage.fillAmount = Mathf.Clamp01(elapsed / dodgeWindow);
                
            if (confirm.triggered)
            {
                pressed = true;
                break;
            }

            yield return null;
        }

        confirm.Disable();

        if (qteVisualGO != null && (activeQTEBar == null || qteVisualGO != activeQTEBar.gameObject))
            Destroy(qteVisualGO);

        if (noteImage != null)
            Destroy(noteImage.gameObject);

        DefenseResult result;
        if (!pressed)
            result = DefenseResult.Miss;
        else if (elapsed <= parryWindow)
            result = DefenseResult.Parry;
        else
            result = DefenseResult.Dodge;

        callback?.Invoke(result);

        // 🔺 Retour au temps normal uniquement si le temps a été modifié
        if (easyMode)
        {
            t = 0f;
            while (t < transitionDuration)
            {
                t += Time.unscaledDeltaTime;
                float blend = t / transitionDuration;
                Time.timeScale = Mathf.Lerp(slowestTimeScale, normalTimeScale, blend);
                Time.fixedDeltaTime = defaultFixedDeltaTime * Time.timeScale;
                yield return null;
            }

            Time.timeScale = normalTimeScale;
            Time.fixedDeltaTime = defaultFixedDeltaTime;
        }
        qteActive = false;
    }

    /// <summary>
    /// Exécute un MusicalMove sur toutes les cibles marquées par Link.
    /// </summary>
    public IEnumerator MusicalMoveOnMarkedTargets(MusicalMoveSO move, CharacterUnit caster)
    {
        var targets = NewBattleManager.Instance.activeCharacterUnits
            .Where(u => u.GetComponent<LinkMark>() != null && u.currentHP > 0 && u != caster)
            .OrderBy(u => Vector3.Distance(caster.transform.position, u.transform.position))
            .ToList();

        foreach (var target in targets)
        {
            yield return MusicalMoveRoutine(move, caster, target);
        }
    }


    private Transform FindTargetForMove(MusicalMoveSO move)
    {
        var first = NewBattleManager.Instance.currentTargetCharacter;
        return first != null ? first.transform : null;
    }

    private IEnumerator PlayTauntWithDelay(AudioClipSO clip, float delay)
    {
        yield return new WaitForSeconds(delay);
        AudioManager.Instance?.PlayVoice(clip);
    }

    /// <summary>
    /// Recherche récursivement un enfant portant un nom donné.
    /// Utilisée ici pour trouver "Camera_TargetedPoint" sur la cible suivie.
    /// </summary>
    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent == null) return null;

        foreach (Transform child in parent)
        {
            if (child.name == targetName)
                return child;

            Transform result = FindChildRecursive(child, targetName);
            if (result != null)
                return result;
        }

        return null;
    }

    #endregion
}

public enum DefenseResult { Parry, Dodge, Jump, Miss }
