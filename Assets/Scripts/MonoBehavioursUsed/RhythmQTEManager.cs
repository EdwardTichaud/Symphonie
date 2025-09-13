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

    // Délai entre les effets de téléportation départ et arrivée
    private const float returnDelay = 0.2f;

    // Tag de la caméra de combat à utiliser pour toutes les timelines
    private const string battleCameraTag = "BattleCamera";

    // QTE Effect
    public AudioClip successSFX;
    public AudioClip failSFX;
    // Effets visuels pour indiquer le résultat du QTE
    public GameObject successEffectPrefab;
    public GameObject failEffectPrefab;

    // ------------------------------------------------------------------------------
    // Gestion des effets sonores
    // ------------------------------------------------------------------------------
    /// <summary>
    /// Récupère une source audio SFX disponible parmi AudioSource_Sfx_1 à 3.
    /// On privilégie la première source qui ne joue aucun son afin d'éviter les
    /// coupures lorsque plusieurs effets doivent être joués simultanément.
    /// </summary>
    /// <returns>Une source libre ou la première du tableau si toutes sont occupées</returns>
    private AudioSource GetAvailableSfxSource()
    {
        // Sécurise l'accès au gestionnaire audio
        var manager = AudioManager.Instance;
        if (manager == null || manager.sfxSources == null || manager.sfxSources.Length == 0)
            return null;

        // Recherche de la première source inactive
        foreach (var src in manager.sfxSources)
        {
            if (src != null && !src.isPlaying)
                return src; // Source libre trouvée
        }

        // Toutes les sources sont en cours de lecture : on réutilise la première
        return manager.sfxSources[0];
    }

    /// <summary>
    /// Joue un effet sonore à l'aide d'une source SFX disponible.
    /// </summary>
    /// <param name="clip">Clip audio à jouer</param>
    private void PlaySfx(AudioClip clip)
    {
        if (clip == null)
            return;

        // On récupère une source libre et on lance la lecture
        var source = GetAvailableSfxSource();
        source?.PlayOneShot(clip);
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
    /// <param name="animatorGO">Objet de référence pour le binding.</param>
    /// <param name="autoRestore">Indique si la caméra doit être restaurée automatiquement.</param>
    /// <param name="initialRotation">Rotation de référence à conserver.</param>
    private void StartTimelinePhase(TimelineAsset timeline, bool overlay, GameObject animatorGO, bool autoRestore, Quaternion initialRotation)
    {
        if (timeline == null || BattleTimelineManager.Instance == null || animatorGO == null)
            return;

        if (overlay)
            // Lecture via le PlayableDirector dédié au lanceur lorsque la caméra
            // suit déjà la timeline complète.
            BattleTimelineManager.Instance.PlayCasterTimeline(timeline, animatorGO);
        else
            BattleTimelineManager.Instance.PlayTimeline(timeline, animatorGO, battleCameraTag, autoRestore, initialRotation);
    }

    /// <summary>
    /// Attend la fin d'une timeline précédemment lancée.
    /// </summary>
    /// <param name="timeline">Timeline à surveiller.</param>
    /// <param name="overlay">Vrai si la timeline est jouée en superposition.</param>
    private IEnumerator WaitForTimelinePhase(TimelineAsset timeline, bool overlay)
    {
        if (timeline == null)
            yield break;

        if (overlay)
        {
            // Suivi de la timeline via le PlayableDirector du lanceur.
            float maxDuration = (float)timeline.duration;
            float timer = 0f;
            while (BattleTimelineManager.Instance != null &&
                   BattleTimelineManager.Instance.IsCasterTimelinePlaying &&
                   timer < maxDuration)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (BattleTimelineManager.Instance != null && BattleTimelineManager.Instance.IsCasterTimelinePlaying)
                Debug.LogWarning($"[RhythmQTEManager] Timeline '{timeline.name}' encore active après {maxDuration}s. Suite forcée.");
        }
        else
        {
            float maxDuration = (float)timeline.duration;
            float timer = 0f;
            while (TimelineManager.Instance != null &&
                   TimelineManager.Instance.IsTimelineActive &&
                   timer < maxDuration)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (TimelineManager.Instance != null && TimelineManager.Instance.IsTimelineActive)
                Debug.LogWarning($"[RhythmQTEManager] Timeline '{timeline.name}' encore active après {maxDuration}s. Suite forcée.");
        }
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
        
        GameObject casterAnimatorGO = caster.GetComponentInChildren<Animator>()?.gameObject;
        // Détermine si une timeline caméra couvrant toute l'action est disponible.
        bool useOverlay = move.fullTimeline != null &&
                          BattleTimelineManager.Instance != null &&
                          TimelineManager.Instance != null &&
                          casterAnimatorGO != null;

        // Lance la timeline globale de caméra si présente.
        if (useOverlay)
            BattleTimelineManager.Instance.PlayTimeline(move.fullTimeline, casterAnimatorGO, battleCameraTag, true, initialRotation);

        // --- Phase de préparation ---
        StartTimelinePhase(move.preparingTimeline, useOverlay, casterAnimatorGO,
                           move.performingTimeline == null && move.retreatTimeline == null, initialRotation);
        yield return WaitForTimelinePhase(move.preparingTimeline, useOverlay);

        // Éventuel délai de pré-animation.
        if (move.startDelay > 0f)
            yield return new WaitForSeconds(move.startDelay);

        // Déplacement ou téléportation vers la cible.
        if (caster != null && target != null)
            yield return MoveTo(caster, target, move);

        // --- Phase d'exécution ---
        StartTimelinePhase(move.performingTimeline, useOverlay, casterAnimatorGO,
                           move.retreatTimeline == null, initialRotation);

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

        yield return WaitForTimelinePhase(move.performingTimeline, useOverlay);

        // --- Retour ou téléportation de repli ---
        if (!move.stayInPlace && caster != null && target != null)
            yield return ReturnToInitialPosition(move, caster, target, originPosition);

        // --- Phase de repli ---
        StartTimelinePhase(move.retreatTimeline, useOverlay, casterAnimatorGO, true, initialRotation);
        yield return WaitForTimelinePhase(move.retreatTimeline, useOverlay);

        // Attente finale de la timeline caméra complète.
        if (useOverlay)
            yield return WaitForTimelinePhase(move.fullTimeline, false);

        isActive = false;
        bool critical = successResults != null && successResults.Count > 0 && successResults.All(s => s);
        NewBattleManager.Instance.AfterMusicalMove(move, caster, critical);

        if (target != null)
            target.OnDeath -= deathHandler;

        currentMove = null;
        currentCaster = null;
        currentTarget = null;

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

        
        Animator animator = caster.GetComponentInChildren<Animator>();
        GameObject casterAnimatorGO = animator != null ? animator.gameObject : null;

        // Détermine si une timeline caméra complète est disponible pour l'objet.
        bool useOverlay = item.fullTimeline != null &&
                          BattleTimelineManager.Instance != null &&
                          TimelineManager.Instance != null &&
                          casterAnimatorGO != null;

        // Lance la timeline globale si présente.
        if (useOverlay)
            BattleTimelineManager.Instance.PlayTimeline(item.fullTimeline, casterAnimatorGO, battleCameraTag, true, initialRotation);

        // --- Phase de préparation ---
        StartTimelinePhase(item.preparingTimeline, useOverlay, casterAnimatorGO,
                           item.performingTimeline == null && item.retreatTimeline == null, initialRotation);
        yield return WaitForTimelinePhase(item.preparingTimeline, useOverlay);

        // Déplacement ou téléportation éventuel vers la cible.
        if (caster != null && target != null)
            yield return SimpleMoveTo(caster, target, item);

        // --- Phase d'utilisation ---
        StartTimelinePhase(item.performingTimeline, useOverlay, casterAnimatorGO,
                           item.retreatTimeline == null, initialRotation);

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

        yield return WaitForTimelinePhase(item.performingTimeline, useOverlay);

        // --- Retour à la position d'origine ---
        if (!item.stayInPlace && caster != null && target != null)
            yield return SimpleReturnToInitialPosition(caster, target, item, originPosition);

        // --- Phase de repli ---
        StartTimelinePhase(item.retreatTimeline, useOverlay, casterAnimatorGO, true, initialRotation);
        yield return WaitForTimelinePhase(item.retreatTimeline, useOverlay);

        // Attente finale de la timeline caméra complète.
        if (useOverlay)
            yield return WaitForTimelinePhase(item.fullTimeline, false);

        isActive = false;
        // Protection en cas de destruction du lanceur avant la fin de la séquence
        itemCasterName = caster != null ? caster.name : "(caster nul)";
        Debug.Log($"Fin de la séquence d'utilisation de l'objet: {item.itemName} par {itemCasterName}");

        // Nettoie la barre de QTE utilisée pour l'objet
        ClearQTEBar();
    }

    private IEnumerator SimpleMoveTo(CharacterUnit caster, CharacterUnit target, ItemData item)
    {
        if (caster == null || caster.IsDead)
            yield break;

        // Destination calculée selon la portée de l'objet
        Vector3 destination = target.transform.position + target.transform.forward * item.castDistance;
        Animator animator = caster.GetComponentInChildren<Animator>();

        bool isTeleport = item.moveSpeed <= 0f;

        if (isTeleport)
        {
            // Lecture des effets de départ
            if (item.tpSFx_Start != null)
                PlaySfx(item.tpSFx_Start);
            if (item.tpVfx_Start != null)
                Instantiate(item.tpVfx_Start, caster.transform.position, Quaternion.identity);

            // Animation de déplacement
            if (!caster.IsDead)
                animator?.Play("Dash_Battle");

            // Petit délai pour laisser apparaître l'effet
            yield return new WaitForSeconds(returnDelay);

            // Téléportation instantanée
            caster.transform.position = destination;

            if (item.tpVfx_End != null)
                Instantiate(item.tpVfx_End, destination, Quaternion.identity);
            if (item.tpSFx_End != null)
                PlaySfx(item.tpSFx_End);
        }
        else
        {
            // Animation du dash
            if (!caster.IsDead)
                animator?.Play("Dash_Battle");

            Vector3 startPos = caster.transform.position;
            float distance = Vector3.Distance(startPos, destination);
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
        }

        // Orientation vers la cible
        Vector3 dir = (target.transform.position - caster.transform.position).normalized;
        if (dir != Vector3.zero)
            caster.transform.forward = dir;

        yield return null;
    }

    // Retourne le lanceur à la position qu'il occupait au début de l'utilisation de l'objet
    private IEnumerator SimpleReturnToInitialPosition(CharacterUnit caster, CharacterUnit target, ItemData item, Vector3 originPosition)
    {
        if (caster == null || caster.IsDead)
            yield break;

        Vector3 origin = originPosition;
        Animator animator = caster.GetComponentInChildren<Animator>();
        bool isTeleport = item.moveSpeed <= 0f;

        if (isTeleport)
        {
            if (item.tpSFx_Start != null)
                PlaySfx(item.tpSFx_Start);
            if (item.tpVfx_Start != null)
                Instantiate(item.tpVfx_Start, caster.transform.position, Quaternion.identity);

            if (!caster.IsDead)
                animator?.Play("Dash_Battle");

            yield return new WaitForSeconds(returnDelay);

            caster.transform.position = origin;

            if (item.tpVfx_End != null)
                Instantiate(item.tpVfx_End, origin, Quaternion.identity);
            if (item.tpSFx_End != null)
                PlaySfx(item.tpSFx_End);
        }
        else
        {
            if (!caster.IsDead)
                animator?.Play("Dash_Battle");

            Vector3 startPos = caster.transform.position;
            float distance = Vector3.Distance(startPos, origin);
            float duration = distance / item.moveSpeed;
            float t = 0f;
            while (t < 1f)
            {
                caster.transform.position = Vector3.Lerp(startPos, origin, t);
                t += Time.deltaTime / Mathf.Max(duration, 0.0001f);
                yield return null;
            }
            caster.transform.position = origin;
        }

        // Orientation optionnelle vers la cible
        if (target != null)
        {
            Vector3 lookDir = (target.transform.position - caster.transform.position).normalized;
            if (lookDir != Vector3.zero)
                caster.transform.forward = lookDir;
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

        Animator animator = caster.GetComponentInChildren<Animator>();
        bool isTeleport = move.moveSpeed <= 0f;

        if (isTeleport)
        {
            if (move.tpSFx_Start != null)
                PlaySfx(move.tpSFx_Start);
            if (move.tpVfx_Start != null)
                Instantiate(move.tpVfx_Start, caster.transform.position, Quaternion.identity);

            if (!caster.IsDead)
                animator?.Play("Dash_Battle");

            yield return new WaitForSeconds(returnDelay);

            caster.transform.position = targetPos;

            if (move.tpVfx_End != null)
                Instantiate(move.tpVfx_End, targetPos, Quaternion.identity);
            if (move.tpSFx_End != null)
                PlaySfx(move.tpSFx_End);
        }
        else
        {
            if (!caster.IsDead)
                animator?.Play("Dash_Battle");

            Vector3 startPos = caster.transform.position;
            float distance = Vector3.Distance(startPos, targetPos);
            float duration = distance / move.moveSpeed;
            float t = 0f;
            while (t < 1f)
            {
                caster.transform.position = Vector3.Lerp(startPos, targetPos, t);
                t += Time.deltaTime / Mathf.Max(duration, 0.0001f);
                yield return null;
            }
            caster.transform.position = targetPos;
        }

        Vector3 lookDir = Vector3.zero;
        if (target != null)
            lookDir = (target.transform.position - caster.transform.position).normalized;

        if (lookDir != Vector3.zero)
            caster.transform.forward = lookDir;
        else if (offsetDir != Vector3.zero)
            caster.transform.forward = offsetDir;

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
        Animator animator = caster.GetComponentInChildren<Animator>();
        bool isTeleport = move.moveSpeed <= 0f;

        if (isTeleport)
        {
            if (move.tpSFx_Start != null)
                PlaySfx(move.tpSFx_Start);
            if (move.tpVfx_Start != null)
                Instantiate(move.tpVfx_Start, caster.transform.position, Quaternion.identity);

            if (!caster.IsDead)
                animator?.Play("Dash_Battle");

            yield return new WaitForSeconds(returnDelay);

            if (caster == null)
            {
                Debug.LogWarning("ReturnToInitialPosition : caster détruit pendant le délai");
                yield break;
            }

            caster.transform.position = initialPosition;

            if (move.tpVfx_End != null)
                Instantiate(move.tpVfx_End, initialPosition, Quaternion.identity);
            if (move.tpSFx_End != null)
                PlaySfx(move.tpSFx_End);
        }
        else
        {
            if (!caster.IsDead)
                animator?.Play("Dash_Battle");

            float distance = Vector3.Distance(startPos, initialPosition);
            float duration = distance / move.moveSpeed;
            float t = 0f;
            while (t < 1f)
            {
                caster.transform.position = Vector3.Lerp(startPos, initialPosition, t);
                t += Time.deltaTime / Mathf.Max(duration, 0.0001f);
                yield return null;
            }
            caster.transform.position = initialPosition;
        }

        yield return null;

        if (move.stayFaceToTarget && target != null)
        {
            Vector3 finalDirToTarget = (target.transform.position - caster.transform.position).normalized;
            if (finalDirToTarget != Vector3.zero)
                caster.transform.forward = finalDirToTarget;
        }
        else
        {
            Vector3 finalDirToParent = (initialPosition - caster.transform.position).normalized;
            if (finalDirToParent != Vector3.zero)
                caster.transform.forward = finalDirToParent;
        }
        Debug.Log("Le caster a terminé son retour.");
    }

    IEnumerator PlayMoveAnimations(string[] animationClips, CharacterUnit caster)
    {
        // Récupère une fois l’Animator plutôt que de l’appeler à chaque itération
        Animator animator = caster.GetComponentInChildren<Animator>();

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

    private IEnumerator PlayTauntWithDelay(AudioClip clip, float delay)
    {
        yield return new WaitForSeconds(delay);
        AudioManager.Instance?.PlayVoice(clip);
    }

    #endregion
}

public enum DefenseResult { Parry, Dodge, Jump, Miss }
