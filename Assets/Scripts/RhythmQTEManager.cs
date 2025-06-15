using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

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

    // QTE Effect
    public AudioClip successSFX;
    public AudioClip failSFX;

    [SerializeField] private bool easyMode = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        defaultFixedDeltaTime = Time.fixedDeltaTime;
    }

    // Séquence du Musicalmove - Ajouter autant de méthodes que d'effets durant le move
    public IEnumerator MusicalMoveRoutine(MusicalMoveSO move, CharacterUnit caster, CharacterUnit target)
    {
        Debug.Log("Début de la séquence du MusicalMove: " + move + " de " + caster.name);
        isActive = true;

        if (move.musicalMoveIntroAnimationNames.Length > 0)
        {
            yield return PlayMoveAnimations(move.musicalMoveIntroAnimationNames, caster);
        }

        yield return MoveTo(caster, target, move); // Déplacement vers l’ennemi

        if (move.musicalMoveAnimationNames.Length > 0)
        {
            yield return PlayMoveAnimations(move.musicalMoveAnimationNames, caster);
        }

        yield return caster.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).length; // Attend la fin de l’animation d’attaque

        if (!move.stayInPlace)
        {
            yield return ReturnToInitialPosition(move, caster, target);
        }

        isActive = false;
        NewBattleManager.Instance.EndTurn();

        Debug.Log("Fin de la séquence du MusicalMove: " + move + " de " + caster.name);
    }

    public IEnumerator ItemRoutine(ItemData item, CharacterUnit caster, CharacterUnit target)
    {

        if (!item.stayInPlace)
        {
            yield return null;
        }
    }

    private IEnumerator MoveTo(CharacterUnit caster, CharacterUnit target, MusicalMoveSO move)
    {
        Debug.Log("Déplacement de " + caster.name + " vers " + target.name);

        // Si l'animation de course n'est pas assignée, on logue un warning et on sort
        if (move.musicalMoveRunAnimationName == null)
        {
            Debug.LogWarning($"[MoveTo] musicalMoveRunAnimationName n'est pas assigné dans {move.name} !");
            yield break;
        }

        float elapsed = 0f;
        float maxDuration = move.maxRunDuration;

        Vector3 targetPosition = target.transform.position + target.transform.forward * move.castDistance;

        Animator animator = caster.GetComponentInChildren<Animator>();
        animator.Play(move.musicalMoveRunAnimationName);

        while (Vector3.Distance(caster.transform.position, targetPosition) > 0.1f && elapsed < maxDuration)
        {
            Vector3 moveDirection = (targetPosition - caster.transform.position).normalized;
            float step = move.moveSpeed * Time.deltaTime;

            caster.transform.position = Vector3.MoveTowards(caster.transform.position, targetPosition, step);

            if (move.stayFaceToTarget && target != null)
            {
                Vector3 lookDirection = (target.transform.position - caster.transform.position).normalized;
                if (lookDirection != Vector3.zero)
                    caster.transform.forward = lookDirection;
            }
            else
            {
                if (moveDirection != Vector3.zero)
                    caster.transform.forward = Vector3.RotateTowards(
                        caster.transform.forward,
                        moveDirection,
                        step,
                        0f
                    );
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.Log("Fin du déplacement de " + caster.name);
    }

    private IEnumerator ReturnToInitialPosition(MusicalMoveSO move, CharacterUnit caster, CharacterUnit target)
    {
        Debug.Log("Retour de " + caster.name + " vers sa position parent");

        if (move.musicalMoveRunAnimationName == null)
        {
            Debug.LogWarning($"[ReturnToInitialPosition] musicalMoveRunAnimationName n'est pas assigné dans {move.name} !");
            yield break;
        }

        Animator animator = caster.GetComponentInChildren<Animator>();
        animator.Play(move.musicalMoveRunAnimationName);

        Vector3 initialPosition = caster.transform.parent.position;

        while (Vector3.Distance(caster.transform.position, initialPosition) > 0.1f)
        {
            float step = move.moveSpeed * Time.deltaTime;
            Vector3 moveDirection = (initialPosition - caster.transform.position).normalized;
            caster.transform.position = Vector3.MoveTowards(caster.transform.position, initialPosition, step);

            if (move.stayFaceToTarget && target != null)
            {
                Vector3 toTarget = (target.transform.position - caster.transform.position).normalized;
                if (toTarget != Vector3.zero)
                    caster.transform.forward = toTarget;
            }
            else
            {
                if (moveDirection != Vector3.zero)
                    caster.transform.forward = Vector3.RotateTowards(
                        caster.transform.forward,
                        moveDirection,
                        step,
                        0f
                    );
            }

            yield return null;
        }

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
            // Lance le clip courant
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

    public void TriggerQTE(float windowDelay)
    {
        StartCoroutine(QTEWindowRoutine(windowDelay));
    }

    private IEnumerator QTEWindowRoutine(float windowDelay)
    {
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
        RectTransform dynamicCircle = qteVisual.transform.Find("Circle_Dynamic")?.GetComponent<RectTransform>();
        if (dynamicCircle != null)
            dynamicCircle.localScale = Vector3.one * qteStartScale;

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

        // 💥 Résultat
        if (success)
        {
            //
        }
        else
        {
            //
        }

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
    }


    private Transform FindTargetForMove(MusicalMoveSO move)
    {
        var first = NewBattleManager.Instance.currentTargetCharacter;
        return first != null ? first.transform : null;
    }
}

public enum DefenseResult { Parry, Dodge, Jump, Miss }
