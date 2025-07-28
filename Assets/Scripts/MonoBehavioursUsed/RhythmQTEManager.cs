using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using UnityEngine.UI;

public class RhythmQTEManager : MonoBehaviour
{
    public static RhythmQTEManager Instance { get; private set; }

    public MusicalMoveSO currentMove;
    public AudioSource audioSource;
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
    float maxDuration = 5f; // en secondes
    float elapsed = 0f;

    // Délai entre les effets de téléportation départ et arrivée
    private const float teleportDelay = 0.2f;

    // QTE Effect
    public AudioClip successSFX;
    public AudioClip failSFX;
    // Effets visuels pour indiquer le résultat du QTE
    public GameObject successEffectPrefab;
    public GameObject failEffectPrefab;

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
            if (!tauntPlayed && isActive && caster != null && caster.Data.prematureDeathTaunt != null)
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

        // Récupère le GameObject contenant l'Animator du lanceur
        GameObject casterAnimatorGO = caster.GetComponentInChildren<Animator>()?.gameObject;

        // --- Téléportation vers la position relative à la cible ---
        // Cette étape était manquante, d'où l'absence de déplacement constatée.
        if (caster != null && target != null)
        {
            // On attend la fin de la téléportation avant de poursuivre la séquence
            yield return MoveTo(caster, target, move);
        }

        // Si une Timeline est disponible, on la lit en adaptant la caméra selon le type de personnage
        bool hasTimeline = move.performingTimeline != null &&
                          TimelineLauncher.Instance != null &&
                          casterAnimatorGO != null;

        if (hasTimeline)
        {
            // Les ennemis utilisent la timeline mais sans animer la caméra (cameraTag nul)
            string cameraTag = caster.characterType == CharacterType.EnemyUnit ? null : "BattleCamera";
            TimelineLauncher.Instance.PlayTimeline(move.performingTimeline, casterAnimatorGO, cameraTag);
        }

        if (pendingNotes == 0)
        {
            // Pas de QTE : on applique immédiatement l'effet de base
            move.ApplyEffect(caster, target);
        }
        else if (!hasTimeline)
        {
            // Si aucune Timeline n'est utilisée, on attend la résolution des notes
            // Les timelines contenant leurs propres QTE gèrent elles-mêmes le rythme
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

        // Si une Timeline est jouée, on laisse celle-ci dicter le rythme
        if (hasTimeline)
        {
            yield return new WaitUntil(() => !TimelineLauncher.Instance.IsTimelineActive);
        }
        else
        {
            // Pas de timeline : petite attente pour la cohérence des animations
            yield return null;
        }

        if (!move.stayInPlace)
            yield return ReturnToInitialPosition(move, caster, target);

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

        // Prépare la barre de QTE correspondant au motif de l'objet
        if (item.beatPattern != null && item.beatPattern.Count > 0)
            PrepareQTEBar(item.beatPattern);

        if (caster == null || caster.IsDead)
        {
            isActive = false;
            yield break;
        }

        Animator animator = caster.GetComponentInChildren<Animator>();

        // Lecture d'une Timeline ou d'une animation d'intro
        bool hasTimeline = item.performingTimeline != null && TimelineLauncher.Instance != null && animator != null;

        if (hasTimeline)
        {
            TimelineLauncher.Instance.PlayTimeline(item.performingTimeline, animator.gameObject, "BattleCamera");
            // ⏳ Attendre la fin effective de la Timeline avant de continuer
            yield return new WaitUntil(() => !TimelineLauncher.Instance.IsTimelineActive);
        }
        else if (item.introAnimationClip != null && animator != null && !caster.IsDead)
        {
            animator.Play(item.introAnimationClip.name);
            yield return null;
            float clipDuration = animator.GetCurrentAnimatorStateInfo(0).length;
            yield return new WaitForSeconds(clipDuration);
        }

        // Téléportation jusqu'à la cible si aucune Timeline ne gère déjà le mouvement
        if (!hasTimeline && caster != null && target != null)
        {
            yield return SimpleMoveTo(caster, target, item);
        }

        // Animation principale si pas de Timeline
        if (!hasTimeline && item.animationClip != null && animator != null && !caster.IsDead)
        {
            animator.Play(item.animationClip.name);
            yield return null;
            float clipDuration = animator.GetCurrentAnimatorStateInfo(0).length;
            yield return new WaitForSeconds(clipDuration);
        }

        // Phase de QTE propre à l'objet uniquement sans Timeline
        LastItemSuccess = true;
        if (!hasTimeline && item.beatPattern != null && item.beatPattern.Count > 0)
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

        // Retour à la position d'origine si l'objet ne demande pas de rester en place
        if (caster != null && !item.stayInPlace)
        {
            yield return SimpleReturnToInitialPosition(caster, target, item);
        }

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

        bool hasMovement = Vector3.Distance(caster.transform.position, destination) > 0.01f;
        if (hasMovement)
        {
            // Déclenche uniquement le son et l'effet visuel si le déplacement est réel
            caster.PlayMoveStartSound();

            if (caster.Data.TPEffect_Start != null)
                Instantiate(caster.Data.TPEffect_Start, caster.transform.position, Quaternion.identity);
        }

        Animator animator = caster.GetComponentInChildren<Animator>();
        // Animation de début de téléportation
        if (!caster.IsDead)
        {
            if (caster.Data.TPAnimation_Start != null)
                animator?.Play(caster.Data.TPAnimation_Start.name);
            else if (caster.Data.moveClip != null)
                animator?.Play(caster.Data.moveClip.name);
        }

        // Laisse un court délai pour jouer l'effet avant de déplacer
        yield return new WaitForSeconds(teleportDelay);

        // Téléportation après le délai
        caster.transform.position = destination;

        if (hasMovement && caster.Data.TPEffect_Destination != null)
            Instantiate(caster.Data.TPEffect_Destination, destination, Quaternion.identity);
        if (animator != null && caster.Data.TPAnimation_Destination != null && !caster.IsDead)
            animator.Play(caster.Data.TPAnimation_Destination.name);

        // Orientation vers la cible
        Vector3 dir = (target.transform.position - caster.transform.position).normalized;
        if (dir != Vector3.zero)
            caster.transform.forward = dir;

        yield return null;

        if (hasMovement)
            caster.PlayMoveEndSound();
    }

    private IEnumerator SimpleReturnToInitialPosition(CharacterUnit caster, CharacterUnit target, ItemData item)
    {
        if (caster == null || caster.IsDead)
            yield break;

        Vector3 origin = caster.transform.parent.position;
        bool hasMovement = Vector3.Distance(caster.transform.position, origin) > 0.01f;
        if (hasMovement)
        {
            // Jouer le son et l'effet seulement s'il y a déplacement
            caster.PlayMoveStartSound();

            if (caster.Data.TPEffect_Start != null)
                Instantiate(caster.Data.TPEffect_Start, caster.transform.position, Quaternion.identity);
        }

        Animator animator = caster.GetComponentInChildren<Animator>();
        // Animation de début de téléportation retour
        if (!caster.IsDead)
        {
            if (caster.Data.TPAnimation_Start != null)
                animator?.Play(caster.Data.TPAnimation_Start.name);
            else if (caster.Data.moveClip != null)
                animator?.Play(caster.Data.moveClip.name);
        }

        // Laisse un court délai pour jouer l'effet avant de déplacer
        yield return new WaitForSeconds(teleportDelay);
        // Téléportation vers la position d'origine
        caster.transform.position = origin;

        if (hasMovement && caster.Data.TPEffect_Destination != null)
            Instantiate(caster.Data.TPEffect_Destination, origin, Quaternion.identity);
        if (animator != null && caster.Data.TPAnimation_Destination != null && !caster.IsDead)
            animator.Play(caster.Data.TPAnimation_Destination.name);

        // Orientation optionnelle vers la cible
        if (target != null)
        {
            Vector3 lookDir = (target.transform.position - caster.transform.position).normalized;
            if (lookDir != Vector3.zero)
                caster.transform.forward = lookDir;
        }

        yield return null;

        if (hasMovement)
            caster.PlayMoveEndSound();
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

        // Téléportation obligatoire vers la cible
        {
            bool teleportHasMovement;
            Vector3 teleportOffsetDir = target.transform.forward;
            switch (move.relativePosition)
            {
                case RelativePosition.Back:
                    teleportOffsetDir = -target.transform.forward;
                    break;
                case RelativePosition.Left:
                    teleportOffsetDir = -target.transform.right;
                    break;
                case RelativePosition.Right:
                    teleportOffsetDir = target.transform.right;
                    break;
            }

            float teleportMobilityBonus = caster.currentMobility;
            Vector3 teleportTargetPosition = target.transform.position + teleportOffsetDir * (move.castDistance + teleportMobilityBonus);
            teleportHasMovement = Vector3.Distance(startPosition, teleportTargetPosition) > 0.01f;
            if (teleportHasMovement)
            {
                // Déclenche les effets uniquement en cas de déplacement
                caster.PlayMoveStartSound();

                if (caster.Data.TPEffect_Start != null)
                    Instantiate(caster.Data.TPEffect_Start, caster.transform.position, Quaternion.identity);
            }

            if (move.teleportStartVFXPrefab != null)
                Instantiate(move.teleportStartVFXPrefab, caster.transform.position, Quaternion.identity);

            Animator animator = caster.GetComponentInChildren<Animator>();
            // Animation de début de téléportation
            if (!caster.IsDead)
            {
                if (caster.Data.TPAnimation_Start != null)
                    animator?.Play(caster.Data.TPAnimation_Start.name);
                else if (caster.Data.moveClip != null)
                    animator?.Play(caster.Data.moveClip.name);
            }

            // Délai pour séparer départ et arrivée
            yield return new WaitForSeconds(teleportDelay);

            caster.transform.position = teleportTargetPosition;

            if (move.teleportEndVFXPrefab != null)
                Instantiate(move.teleportEndVFXPrefab, teleportTargetPosition, Quaternion.identity);

            if (teleportHasMovement && caster.Data.TPEffect_Destination != null)
                Instantiate(caster.Data.TPEffect_Destination, teleportTargetPosition, Quaternion.identity);
            if (animator != null && caster.Data.TPAnimation_Destination != null && !caster.IsDead)
                animator.Play(caster.Data.TPAnimation_Destination.name);

            Vector3 lookDir = Vector3.zero;
            if (target != null)
                lookDir = (target.transform.position - caster.transform.position).normalized;


            if (lookDir != Vector3.zero)
                caster.transform.forward = lookDir;
            else if (teleportOffsetDir != Vector3.zero)
                caster.transform.forward = teleportOffsetDir;

            yield return null;
            // Caster peut avoir été détruit pendant le déplacement
            moveCasterName = caster != null ? caster.name : "(caster nul)";
            Debug.Log("Fin du déplacement de " + moveCasterName);

            if (teleportHasMovement)
                caster.PlayMoveEndSound();
            yield break;
        }

    }

    private IEnumerator ReturnToInitialPosition(MusicalMoveSO move, CharacterUnit caster, CharacterUnit target)
    {
        // Peut être appelé alors que l'unité a été détruite
        string returnCasterName = caster != null ? caster.name : "(caster nul)";
        Debug.Log("Retour de " + returnCasterName + " vers sa position parent");

        // Lancer peut être détruit avant l'appel : on sécurise l'accès
        if (caster == null || caster.IsDead)
        {
            Debug.LogWarning("ReturnToInitialPosition : caster nul ou détruit à l'appel");
            yield break;
        }

        Vector3 startPos = caster.transform.position;
        Vector3 initialPosition = caster.transform.parent.position;
        bool hasMovement = Vector3.Distance(startPos, initialPosition) > 0.01f;
        if (hasMovement)
        {
            // Éviter son et effet si aucun mouvement n'est nécessaire
            caster.PlayMoveStartSound();

            if (caster.Data.TPEffect_Start != null)
                Instantiate(caster.Data.TPEffect_Start, caster.transform.position, Quaternion.identity);
        }

        // Téléportation de retour systématique
        if (move.teleportStartVFXPrefab != null)
            Instantiate(move.teleportStartVFXPrefab, caster.transform.position, Quaternion.identity);

        Animator animator = caster.GetComponentInChildren<Animator>();
        // Animation de début de retour
        if (!caster.IsDead)
        {
            if (caster.Data.TPAnimation_Start != null)
                animator?.Play(caster.Data.TPAnimation_Start.name);
            else if (caster.Data.moveClip != null)
                animator?.Play(caster.Data.moveClip.name);
        }

        // Délai avant de revenir à la position initiale
        yield return new WaitForSeconds(teleportDelay);

        // Lancer peut avoir été détruit pendant l'attente
        if (caster == null)
        {
            Debug.LogWarning("ReturnToInitialPosition : caster détruit pendant le délai");
            yield break;
        }

        caster.transform.position = initialPosition;

        if (move.teleportEndVFXPrefab != null)
            Instantiate(move.teleportEndVFXPrefab, initialPosition, Quaternion.identity);

        if (hasMovement && caster.Data.TPEffect_Destination != null)
            Instantiate(caster.Data.TPEffect_Destination, initialPosition, Quaternion.identity);
        if (animator != null && caster.Data.TPAnimation_Destination != null && !caster.IsDead)
            animator.Play(caster.Data.TPAnimation_Destination.name);

        yield return null;

        // Vérifie que le lanceur existe encore avant la suite
        if (caster == null)
        {
            Debug.LogWarning("ReturnToInitialPosition : caster détruit avant la fin du retour");
            yield break;
        }

        if (hasMovement)
            caster.PlayMoveEndSound();

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

        if (hasMovement && caster != null)
            caster.PlayMoveEndSound();

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

        // Si l'attaquant est un ennemi, la cible doit exécuter le QTE défensif
        if (currentCaster.Data.characterType == CharacterType.EnemyUnit)
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

        // Détruit uniquement la barre si elle n'est pas réutilisée
        if (qteVisualGO != null && (activeQTEBar == null || qteVisualGO != activeQTEBar.gameObject))
            Destroy(qteVisualGO);

        // Supprime la note utilisée pour libérer l'espace
        if (noteImage != null)
            Destroy(noteImage.gameObject);

        callback?.Invoke(success);

        // Affichage visuel du résultat directement sur la zone de validation
        if (activeQTEBar != null)
        {
            var zone = activeQTEBar.ValidationZone;
            GameObject effect = success ? successEffectPrefab : failEffectPrefab;
            if (effect != null && zone != null)
                Instantiate(effect, zone.position, Quaternion.identity, qteUIParent);
        }

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
