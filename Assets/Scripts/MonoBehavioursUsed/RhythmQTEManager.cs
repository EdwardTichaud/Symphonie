using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

public class RhythmQTEManager : MonoBehaviour
{
    public static RhythmQTEManager Instance { get; private set; }

    public MusicalMoveSO currentMove;
    public AudioSource audioSource;
    private float startTime;
    private int currentBeatIndex = 0;
    private bool isActive = false;
    private List<bool> successResults;

    // QTE
    private Coroutine beatRoutine;

    [SerializeField] private float slowMotionFactor = 0.25f; // ralentissement ×4
    private float defaultFixedDeltaTime;

    [Header("QTE Visuel")]
    public GameObject qteCirclePrefab; // Ton prefab avec les deux cercles
    public Transform qteUIParent; // Parent dans le canvas (facultatif, sinon instancié en world space)
    public float qteStartScale = 1.5f;
    public float qteEndScale = 1.0f;

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

    private CharacterUnit currentCaster;
    private CharacterUnit currentTarget;
    private int pendingNotes = 0;
    private bool qteActive = false;

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

    // Séquence du Musicalmove - Ajouter autant de méthodes que d'effets durant le move
    /// <summary>
    /// Orchestration complète d'un MusicalMove du déplacement à la résolution.
    /// </summary>
    public IEnumerator MusicalMoveRoutine(MusicalMoveSO move, CharacterUnit caster, CharacterUnit target)
    {
        Debug.Log("Début de la séquence du MusicalMove: " + move + " de " + caster.name);
        isActive = true;

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
        bool hasTimeline = move.performingTimeline != null && TimelineLauncher.Instance != null && casterAnimatorGO != null;

        if (hasTimeline)
        {
            // Les ennemis utilisent la timeline mais sans animer la caméra (cameraTag nul)
            string cameraTag = caster.characterType == CharacterType.EnemyUnit ? null : "BattleCamera";
            TimelineLauncher.Instance.PlayTimeline(move.performingTimeline, casterAnimatorGO, cameraTag);
        }

        currentMove = move;
        currentCaster = caster;
        currentTarget = target;
        pendingNotes = move.notes != null ? move.notes.Count : 0;

        if (pendingNotes == 0)
        {
            move.ApplyEffect(caster, target);
        }
        else
        {
            // Attend que toutes les notes aient été jouées via les événements d'animation
            while (pendingNotes > 0)
                yield return null;
        }

        // Si une Timeline a été jouée, on attend sa durée avant de poursuivre
        if (hasTimeline && move.performingTimeline != null)
        {
            yield return new WaitForSeconds((float)move.performingTimeline.duration);
        }
        else
        {
            // Sinon on attend simplement la fin de l'animation actuelle
            float animLength = caster.GetComponentInChildren<Animator>()
                .GetCurrentAnimatorStateInfo(0).length;
            yield return new WaitForSeconds(animLength);
        }

        if (!move.stayInPlace)
            yield return ReturnToInitialPosition(move, caster, target);

        isActive = false;
        NewBattleManager.Instance.AfterMusicalMove(move, caster);

        if (target != null)
            target.OnDeath -= deathHandler;

        currentMove = null;
        currentCaster = null;
        currentTarget = null;

        Debug.Log("Fin de la séquence du MusicalMove: " + move + " de " + caster.name);
    }

    /// <summary>
    /// Gère la séquence d'utilisation d'un objet en combat.
    /// Cette routine reprend le même déroulé qu'un MusicalMove :
    /// lecture d'une éventuelle introduction, déplacement optionnel,
    /// animation principale puis retour à la position initiale.
    /// </summary>
    public IEnumerator ItemRoutine(ItemData item, CharacterUnit caster, CharacterUnit target)
    {
        Debug.Log($"Début de la séquence d'utilisation de l'objet: {item.itemName} par {caster.name}");
        isActive = true;

        if (caster == null || caster.IsDead)
        {
            isActive = false;
            yield break;
        }

        Animator animator = caster.GetComponentInChildren<Animator>();

        // Lecture d'une Timeline ou d'une animation d'intro
        if (item.performingTimeline != null && TimelineLauncher.Instance != null && animator != null)
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

        // Téléportation jusqu'à la cible
        if (caster != null && target != null)
        {
            yield return SimpleMoveTo(caster, target, item);
        }

        // Animation principale si pas de Timeline
        if (item.performingTimeline == null && item.animationClip != null && animator != null && !caster.IsDead)
        {
            animator.Play(item.animationClip.name);
            yield return null;
            float clipDuration = animator.GetCurrentAnimatorStateInfo(0).length;
            yield return new WaitForSeconds(clipDuration);
        }

        // Retour à la position d'origine si l'objet ne demande pas de rester en place
        if (caster != null && !item.stayInPlace)
        {
            yield return SimpleReturnToInitialPosition(caster, target, item);
        }

        isActive = false;
        Debug.Log($"Fin de la séquence d'utilisation de l'objet: {item.itemName} par {caster.name}");
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

        Debug.Log("Déplacement de " + caster.name + " vers " + target.name);

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
            Debug.Log("Fin du déplacement de " + caster.name);

            if (teleportHasMovement)
                caster.PlayMoveEndSound();
            yield break;
        }

    }

    private IEnumerator ReturnToInitialPosition(MusicalMoveSO move, CharacterUnit caster, CharacterUnit target)
    {
        Debug.Log("Retour de " + caster.name + " vers sa position parent");

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

        caster.transform.position = initialPosition;

        if (move.teleportEndVFXPrefab != null)
            Instantiate(move.teleportEndVFXPrefab, initialPosition, Quaternion.identity);

        if (hasMovement && caster.Data.TPEffect_Destination != null)
            Instantiate(caster.Data.TPEffect_Destination, initialPosition, Quaternion.identity);
        if (animator != null && caster.Data.TPAnimation_Destination != null && !caster.IsDead)
            animator.Play(caster.Data.TPAnimation_Destination.name);

        yield return null;

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

        if (hasMovement)
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
        if (note.clip != null)
            audioSource.PlayOneShot(note.clip);

        // Pas d'icône spécifique pour les notes, on passe null
        StartCoroutine(WaitForQTE(note.rhythm, null, Vector2.zero, success =>
        {
            currentMove.ApplyEffect(currentCaster, currentTarget, success);
            pendingNotes = Mathf.Max(0, pendingNotes - 1);
        }));
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

        // 🔻 Ralentissement progressif
        float t = 0f;
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

        GameObject qteVisual = Instantiate(qteCirclePrefab, qteUIParent);
        // Positionne le visuel selon la valeur fournie par le Trigger
        var visualRect = qteVisual.GetComponent<RectTransform>();
        if (visualRect != null)
            visualRect.anchoredPosition = position;
        RectTransform dynamicCircle = qteVisual.transform.Find("Circle_Dynamic")?.GetComponent<RectTransform>();
        if (dynamicCircle != null)
            dynamicCircle.localScale = Vector3.one * qteStartScale;

        // Ajoute dynamiquement l'icône si fournie
        if (icon != null)
        {
            var iconGO = new GameObject("InputIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            iconGO.transform.SetParent(qteVisual.transform, false);
            var rect = iconGO.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(64f, 64f);
            var img = iconGO.GetComponent<UnityEngine.UI.Image>();
            img.sprite = icon;
        }

        float elapsed = 0f;
        bool success = false;
        var confirm = InputsManager.Instance.playerInputs.Battle.Confirm;
        confirm.Enable();

        while (easyMode || elapsed < holdDuration)
        {
            float unscaledDelta = Time.unscaledDeltaTime;
            elapsed += unscaledDelta;

            if (dynamicCircle != null)
            {
                float progress = Mathf.Clamp01(elapsed / holdDuration);
                float scale = Mathf.Lerp(qteStartScale, qteEndScale, progress);
                dynamicCircle.localScale = Vector3.one * scale;
            }

            if (confirm.triggered)
            {
                success = true;
                break;
            }

            yield return null;
        }

        confirm.Disable();
        Destroy(qteVisual);

        callback?.Invoke(success);

        // 🔺 Retour au temps normal
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
