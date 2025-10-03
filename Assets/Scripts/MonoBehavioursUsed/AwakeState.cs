using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterUnit))]
public class AwakeState : UnitStateEffects
{
    [Tooltip("Multiplicateur appliqué aux caractéristiques en mode Awake")]
    public float statMultiplier = 1.5f;

    [Header("Aura")]
    [Tooltip("Prefab de l'aura affichée en mode Awake")] public GameObject auraPrefab;
    private GameObject auraInstance;

    [Header("FireWing")]
    [Tooltip("Référence à l'objet 'FireWing' à activer en mode Awake")]
    [SerializeField] private GameObject fireWing; // Peut rester vide : recherché automatiquement

    private CharacterUnit unit;
    private bool isAwake;

    // --- Animation --------------------------------------------------------
    private AnimatorOverrideController animatorOverrideController; // Copie runtime permettant de remplacer les clips d'Idle.
    private AnimationClip idleClipOriginalKey;                     // Clip "clé" (celui déclaré dans le controller) à substituer.
    private AnimationClip idleClipOriginalValue;                   // Valeur d'origine à restaurer lorsque l'état Awake se termine.
    private const string AnimatorIdleStateName = "Idle_Battle";    // Nom du state Idle à remplacer pendant l'Awake.
    private const string AnimatorAwakeOnStateName = "Awake_On";    // Nom de l'animation de démarrage d'Awake dans l'Animator.
    private const string AnimatorAwakeOffStateName = "Awake_Off";  // Nom de l'animation de sortie d'Awake dans l'Animator.
    private static readonly int AnimatorAwakeOnHash = Animator.StringToHash(AnimatorAwakeOnStateName);
    private static readonly int AnimatorAwakeOffHash = Animator.StringToHash(AnimatorAwakeOffStateName);
    private const int AnimatorBaseLayerIndex = 0;                  // Couche principale utilisée pour la plupart des animations.
    private const float AnimatorCrossFadeDuration = 0.1f;          // Durée standard des transitions déclenchées par script.
    private float cachedAwakeOnDuration = -1f;                     // Mise en cache de la durée de l'anim Awake_On.
    private float cachedAwakeOffDuration = -1f;                    // Mise en cache de la durée de l'anim Awake_Off.
    private Coroutine idleRestoreCoroutine;                        // Coroutine relançant l'Idle une fois les animations terminées.

    // --- Effets visuels ---------------------------------------------------
    private const float DefaultLoopTransitionDelay = 1f;           // Fallback utilisé si aucune durée précise n'est trouvée.
    private GameObject awakenStartInstance;                        // Instance de l'effet "Start" (transitoire).
    private GameObject awakenLoopInstance;                         // Instance de l'effet "Loop" (permanent).
    private Coroutine awakenEffectTransitionCoroutine;             // Coroutine basculant du Start vers le Loop.

    public bool IsAwake => isAwake;

    protected override void Awake()
    {
        base.Awake();
        unit = GetComponent<CharacterUnit>();
        // Si aucune référence n'est fournie, on recherche l'enfant nommé "FireWing"
        if (fireWing == null)
        {
            Transform child = transform.Find("FireWing");
            if (child != null)
                fireWing = child.gameObject;
        }

        // Au départ l'objet FireWing doit être désactivé
        if (fireWing != null)
            fireWing.SetActive(false);

        // Prépare un AnimatorOverrideController afin de pouvoir remplacer dynamiquement
        // le clip d'Idle par sa version éveillée tout en conservant une restauration aisée.
        InitializeAnimatorOverrideData();

        // Mise en cache des durées des animations "On" et "Off" afin d'orchestrer précisément
        // le déclenchement du loop VFX ainsi que le retour automatique sur l'Idle.
        cachedAwakeOnDuration = GetAnimationClipDuration(AnimatorAwakeOnStateName);
        cachedAwakeOffDuration = GetAnimationClipDuration(AnimatorAwakeOffStateName);

        // Relie automatiquement les clips d'entrée/sortie configurés dans la fiche personnage.
        if (unit != null && unit.Data != null)
        {
            if (unit.Data.awakeEnterClip != null)
                enterClip = unit.Data.awakeEnterClip;
            if (unit.Data.awakeExitClip != null)
                exitClip = unit.Data.awakeExitClip;
        }
    }

    public void EnterAwake()
    {
        if (isAwake) return;
        isAwake = true;
        ApplyStatBonus();
        unit?.NotifyIdleStateExit(); // Le son de sortie d'Idle doit être joué avant la transition Awake.
        // Activation des ailes de feu lorsque le personnage entre en mode Awake
        if (fireWing != null)
            fireWing.SetActive(true);
        if (auraPrefab != null && auraInstance == null)
            auraInstance = Instantiate(auraPrefab, transform.position, Quaternion.identity, transform);

        // Lance l'animation d'entrée et planifie le retour vers l'Idle une fois la transition achevée.
        PlayAwakeOnAnimation();
        OverrideIdleAnimation();
        ScheduleIdlePlayback(cachedAwakeOnDuration);

        // Déploie l'effet de démarrage puis prépare la bascule vers l'effet de loop continu.
        HandleAwakenStartEffect();

        EnterState();
    }

    public void ExitAwake()
    {
        if (!isAwake) return;
        isAwake = false;
        RemoveStatBonus();
        // Désactivation des ailes de feu quand on quitte le mode Awake
        if (fireWing != null)
            fireWing.SetActive(false);
        if (auraInstance != null)
        {
            Destroy(auraInstance);
            auraInstance = null;
        }

        // Interrompt immédiatement toute coroutine en attente et supprime les effets actifs.
        StopAwakenEffectTransitionCoroutine();
        DestroyAwakenEffect(ref awakenStartInstance);
        DestroyAwakenEffect(ref awakenLoopInstance);

        // Déclenche l'animation de sortie, remet l'Idle d'origine et programme sa relance.
        PlayAwakeOffAnimation();
        RestoreIdleAnimation();
        ScheduleIdlePlayback(cachedAwakeOffDuration);

        // Génère l'effet de fin si défini dans les données du personnage.
        SpawnAwakenEndEffect();

        ExitState();
    }

    private void ApplyStatBonus()
    {
        if (unit == null) return;
        unit.currentStrength *= statMultiplier;
        unit.currentDefense *= statMultiplier;
        unit.currentReflex *= statMultiplier;
        unit.currentMobility *= statMultiplier;
        unit.currentPower *= statMultiplier;
        unit.currentStability *= statMultiplier;
        unit.currentVitality *= statMultiplier;
        unit.currentSagacity *= statMultiplier;
    }

    private void RemoveStatBonus()
    {
        if (unit == null) return;
        unit.currentStrength /= statMultiplier;
        unit.currentDefense /= statMultiplier;
        unit.currentReflex /= statMultiplier;
        unit.currentMobility /= statMultiplier;
        unit.currentPower /= statMultiplier;
        unit.currentStability /= statMultiplier;
        unit.currentVitality /= statMultiplier;
        unit.currentSagacity /= statMultiplier;
    }

    #region Gestion des animations d'Awake

    /// <summary>
    ///     Installe un <see cref="AnimatorOverrideController"/> local pour pouvoir remplacer
    ///     l'animation Idle lorsque l'unité entre en éveil.
    /// </summary>
    private void InitializeAnimatorOverrideData()
    {
        if (animator == null)
            return;

        // On récupère l'override existant ou on en crée un nouveau pour garder la configuration d'origine intacte.
        if (animator.runtimeAnimatorController is AnimatorOverrideController existingOverride)
        {
            animatorOverrideController = existingOverride;
        }
        else if (animator.runtimeAnimatorController != null)
        {
            animatorOverrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
            animator.runtimeAnimatorController = animatorOverrideController;
        }

        if (animatorOverrideController == null)
            return;

        // On parcourt la liste des overrides pour mémoriser le couple (clé, valeur) correspondant à l'Idle.
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(animatorOverrideController.overridesCount);
        animatorOverrideController.GetOverrides(overrides);
        foreach (var pair in overrides)
        {
            if (pair.Key != null && pair.Key.name == AnimatorIdleStateName)
            {
                idleClipOriginalKey = pair.Key;
                idleClipOriginalValue = pair.Value != null ? pair.Value : pair.Key;
                break;
            }
        }
    }

    /// <summary>
    ///     Remplace l'animation d'Idle par la version spéciale définie dans les données du personnage.
    /// </summary>
    private void OverrideIdleAnimation()
    {
        if (animatorOverrideController == null || idleClipOriginalKey == null)
            return;

        var awakenIdle = unit != null ? unit.Data?.awakenIdleAnimation : null;
        if (awakenIdle == null)
            return;

        animatorOverrideController[idleClipOriginalKey] = awakenIdle;
    }

    /// <summary>
    ///     Restaure l'animation d'Idle d'origine lorsque l'éveil prend fin.
    /// </summary>
    private void RestoreIdleAnimation()
    {
        if (animatorOverrideController == null || idleClipOriginalKey == null || idleClipOriginalValue == null)
            return;

        animatorOverrideController[idleClipOriginalKey] = idleClipOriginalValue;
    }

    /// <summary>
    ///     Lance l'animation "Awake_On" si elle est déclarée dans le controller.
    /// </summary>
    private void PlayAwakeOnAnimation()
    {
        if (animator == null)
            return;

        if (animator.HasState(AnimatorBaseLayerIndex, AnimatorAwakeOnHash))
            animator.CrossFade(AnimatorAwakeOnHash, AnimatorCrossFadeDuration, AnimatorBaseLayerIndex, 0f);
        else
            animator.Play(AnimatorAwakeOnStateName, AnimatorBaseLayerIndex, 0f);
    }

    /// <summary>
    ///     Lance l'animation "Awake_Off" si disponible.
    /// </summary>
    private void PlayAwakeOffAnimation()
    {
        if (animator == null)
            return;

        if (animator.HasState(AnimatorBaseLayerIndex, AnimatorAwakeOffHash))
            animator.CrossFade(AnimatorAwakeOffHash, AnimatorCrossFadeDuration, AnimatorBaseLayerIndex, 0f);
        else
            animator.Play(AnimatorAwakeOffStateName, AnimatorBaseLayerIndex, 0f);
    }

    /// <summary>
    ///     Programme le relancement de l'animation Idle après une certaine durée.
    ///     Un délai minimal est toujours conservé pour laisser la transition s'installer proprement.
    /// </summary>
    private void ScheduleIdlePlayback(float delaySeconds)
    {
        if (unit == null)
            return;

        if (idleRestoreCoroutine != null)
        {
            StopCoroutine(idleRestoreCoroutine);
            idleRestoreCoroutine = null;
        }

        float safeDelay = delaySeconds > 0f ? delaySeconds : AnimatorCrossFadeDuration;
        idleRestoreCoroutine = StartCoroutine(PlayIdleAfterDelay(safeDelay));
    }

    private IEnumerator PlayIdleAfterDelay(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        unit?.PlayIdleAnimation();
        idleRestoreCoroutine = null;
    }

    /// <summary>
    ///     Renvoie la durée (en secondes) du clip portant le nom indiqué.
    /// </summary>
    private float GetAnimationClipDuration(string clipName)
    {
        if (animator == null)
            return 0f;

        var controller = animator.runtimeAnimatorController;
        if (controller == null)
            return 0f;

        foreach (var clip in controller.animationClips)
        {
            if (clip != null && clip.name == clipName)
                return clip.length;
        }

        return 0f;
    }

    #endregion

    #region Gestion des effets visuels spécifiques à l'Awake

    /// <summary>
    ///     Gère l'instanciation de l'effet "Start" et programme la bascule vers le loop.
    /// </summary>
    private void HandleAwakenStartEffect()
    {
        var data = unit != null ? unit.Data : null;
        if (data == null)
            return;

        StopAwakenEffectTransitionCoroutine();
        DestroyAwakenEffect(ref awakenStartInstance);
        DestroyAwakenEffect(ref awakenLoopInstance);

        if (data.awakenEffect_Start != null)
            awakenStartInstance = InstantiateAwakenEffect(data.awakenEffect_Start);

        float effectDuration = EstimateEffectDuration(awakenStartInstance);
        float delay = Mathf.Max(effectDuration, cachedAwakeOnDuration);
        if (delay <= 0f)
            delay = DefaultLoopTransitionDelay;

        if (data.awakenEffect_Loop != null)
            awakenEffectTransitionCoroutine = StartCoroutine(SpawnAwakenLoopAfterDelay(delay, data.awakenEffect_Loop));
        else if (awakenStartInstance != null)
            awakenEffectTransitionCoroutine = StartCoroutine(DestroyStartEffectAfterDelay(delay));
    }

    private IEnumerator SpawnAwakenLoopAfterDelay(float delaySeconds, GameObject loopPrefab)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);
        else
            yield return null; // On attend un frame pour s'assurer que l'effet Start a le temps d'initialiser ses particules.

        if (!isAwake)
        {
            awakenEffectTransitionCoroutine = null;
            yield break;
        }

        DestroyAwakenEffect(ref awakenStartInstance);

        if (loopPrefab != null)
            awakenLoopInstance = InstantiateAwakenEffect(loopPrefab);

        awakenEffectTransitionCoroutine = null;
    }

    private IEnumerator DestroyStartEffectAfterDelay(float delaySeconds)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);
        else
            yield return null;

        DestroyAwakenEffect(ref awakenStartInstance);
        awakenEffectTransitionCoroutine = null;
    }

    /// <summary>
    ///     Instancie l'effet de fin lorsqu'on quitte l'éveil.
    /// </summary>
    private void SpawnAwakenEndEffect()
    {
        var data = unit != null ? unit.Data : null;
        if (data?.awakenEffect_End == null)
            return;

        InstantiateAwakenEffect(data.awakenEffect_End);
    }

    /// <summary>
    ///     Arrête la coroutine qui gérait la transition Start -> Loop.
    /// </summary>
    private void StopAwakenEffectTransitionCoroutine()
    {
        if (awakenEffectTransitionCoroutine != null)
        {
            StopCoroutine(awakenEffectTransitionCoroutine);
            awakenEffectTransitionCoroutine = null;
        }
    }

    /// <summary>
    ///     Détruit proprement une instance d'effet et vide la référence associée.
    /// </summary>
    private void DestroyAwakenEffect(ref GameObject effectInstance)
    {
        if (effectInstance == null)
            return;

        Destroy(effectInstance);
        effectInstance = null;
    }

    /// <summary>
    ///     Instancie un prefab d'effet sur l'unité en s'assurant qu'il suit le transform parent.
    /// </summary>
    private GameObject InstantiateAwakenEffect(GameObject prefab)
    {
        if (prefab == null)
            return null;

        var instance = Instantiate(prefab, transform.position, Quaternion.identity, transform);
        instance.transform.localPosition = Vector3.zero;
        return instance;
    }

    /// <summary>
    ///     Essaie d'estimer la durée de vie d'un effet basé sur des ParticleSystems afin de déterminer
    ///     quand lancer l'effet de loop. Si aucune information fiable n'est disponible, renvoie 0.
    /// </summary>
    private static float EstimateEffectDuration(GameObject effectInstance)
    {
        if (effectInstance == null)
            return 0f;

        float maxDuration = 0f;
        var particleSystems = effectInstance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particleSystems)
        {
            var main = ps.main;

            if (main.loop)
                return 0f; // Les systèmes bouclant indéfiniment ne permettent pas d'estimer une durée fiable.

            float simulationSpeed = Mathf.Max(main.simulationSpeed, 0.01f);
            float duration = main.duration / simulationSpeed;
            duration += main.startDelay.constantMax;
            duration += main.startLifetime.constantMax;

            if (duration > maxDuration)
                maxDuration = duration;
        }

        return maxDuration;
    }

    #endregion
}
