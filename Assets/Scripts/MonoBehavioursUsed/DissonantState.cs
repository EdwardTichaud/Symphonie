using System.Collections;
using UnityEngine;

/// <summary>
///     Gère la transition d'une unité vers l'état « Dissonant » lorsque ses harmoniques
///     chutent sous le seuil défini. L'objectif est de reproduire un fonctionnement
///     comparable à l'Awake tout en conservant une identité visuelle et sonore propre.
/// </summary>
[RequireComponent(typeof(CharacterUnit))]
public class DissonantState : UnitStateEffects
{
    [Header("Paramètres d'animation")] [Tooltip("Nom de l'état Idle utilisé comme clé d'override dans l'Animator.")]
    private const string AnimatorIdleStateName = "Idle_Battle";

    private const string AnimatorDissonanceOnStateName = "Dissonance_On";  // Animation jouée quand l'état démarre.
    private const string AnimatorDissonanceOffStateName = "Dissonance_Off"; // Animation jouée quand l'état se termine.

    private static readonly int AnimatorDissonanceOnHash = Animator.StringToHash(AnimatorDissonanceOnStateName);
    private static readonly int AnimatorDissonanceOffHash = Animator.StringToHash(AnimatorDissonanceOffStateName);

    private const int AnimatorBaseLayerIndex = 0;      // Index de la couche par défaut.
    private const float AnimatorCrossFadeDuration = .1f; // Durée des transitions déclenchées en script.

    private CharacterUnit unit;                        // Référence directe à l'unité pour accéder aux données.
    private bool isDissonant;                          // Indique si l'état est actuellement actif.

    private AnimatorOverrideController animatorOverrideController; // Permet de remplacer l'Idle dynamiquement.
    private AnimationClip idleClipOriginalKey;         // Clip servant de clé dans l'AnimatorOverrideController.
    private AnimationClip idleClipOriginalValue;       // Valeur à restaurer lorsque l'état prend fin.

    private float cachedDissonanceOnDuration = -1f;    // Durées mises en cache pour planifier le retour à l'Idle.
    private float cachedDissonanceOffDuration = -1f;
    private Coroutine idleRestoreCoroutine;            // Coroutine relançant l'Idle après les transitions.

    /// <summary>
    ///     Indique publiquement si l'état Dissonant est actif. Simplifie les vérifications côté CharacterUnit.
    /// </summary>
    public bool IsDissonant => isDissonant;

    protected override void Awake()
    {
        base.Awake();
        unit = GetComponent<CharacterUnit>();

        // Mise en place du contrôleur d'override pour pouvoir substituer l'Idle par sa version dissonante.
        InitializeAnimatorOverrideData();

        // Mise en cache immédiate des durées des animations On/Off afin de garantir un timing cohérent.
        cachedDissonanceOnDuration = GetAnimationClipDuration(AnimatorDissonanceOnStateName);
        cachedDissonanceOffDuration = GetAnimationClipDuration(AnimatorDissonanceOffStateName);
    }

    /// <summary>
    ///     Active l'état Dissonant : animations spécifiques, Idle alternatif et effets hérités de UnitStateEffects.
    /// </summary>
    public void EnterDissonant()
    {
        if (isDissonant)
            return; // Aucun traitement inutile si l'état est déjà actif.

        isDissonant = true;

        unit?.NotifyIdleStateExit(); // Permet de jouer immédiatement le son de sortie d'Idle si défini.

        PlayDissonanceOnAnimation();
        OverrideIdleAnimation();
        ScheduleIdlePlayback(cachedDissonanceOnDuration);

        EnterState(); // Déclenche les effets visuels/sonores configurés sur UnitStateEffects.
    }

    /// <summary>
    ///     Désactive l'état Dissonant et restaure l'Idle de base.
    /// </summary>
    public void ExitDissonant()
    {
        if (!isDissonant)
            return;

        isDissonant = false;

        PlayDissonanceOffAnimation();
        RestoreIdleAnimation();
        ScheduleIdlePlayback(cachedDissonanceOffDuration);

        ExitState();
    }

    #region Gestion de l'override d'Idle

    private void InitializeAnimatorOverrideData()
    {
        if (animator == null)
            return;

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

        var overrides = new System.Collections.Generic.List<KeyValuePair<AnimationClip, AnimationClip>>(animatorOverrideController.overridesCount);
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

    private void OverrideIdleAnimation()
    {
        if (animatorOverrideController == null || idleClipOriginalKey == null)
            return;

        var dissonantIdle = unit != null ? unit.Data?.dissonantIdleAnimation : null;
        if (dissonantIdle == null)
            return; // Pas de configuration spécifique : on garde l'Idle original.

        animatorOverrideController[idleClipOriginalKey] = dissonantIdle;
    }

    private void RestoreIdleAnimation()
    {
        if (animatorOverrideController == null || idleClipOriginalKey == null || idleClipOriginalValue == null)
            return;

        animatorOverrideController[idleClipOriginalKey] = idleClipOriginalValue;
    }

    #endregion

    #region Gestion des animations Dissonance_On / Off

    private void PlayDissonanceOnAnimation()
    {
        if (animator == null)
            return;

        if (animator.HasState(AnimatorBaseLayerIndex, AnimatorDissonanceOnHash))
            animator.CrossFade(AnimatorDissonanceOnHash, AnimatorCrossFadeDuration, AnimatorBaseLayerIndex, 0f);
        else
            animator.Play(AnimatorDissonanceOnStateName, AnimatorBaseLayerIndex, 0f);
    }

    private void PlayDissonanceOffAnimation()
    {
        if (animator == null)
            return;

        if (animator.HasState(AnimatorBaseLayerIndex, AnimatorDissonanceOffHash))
            animator.CrossFade(AnimatorDissonanceOffHash, AnimatorCrossFadeDuration, AnimatorBaseLayerIndex, 0f);
        else
            animator.Play(AnimatorDissonanceOffStateName, AnimatorBaseLayerIndex, 0f);
    }

    #endregion

    #region Gestion du retour vers l'Idle

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
        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);
        else
            yield return null; // Permet à l'Animator de valider son changement d'état au frame suivant.

        unit?.PlayIdleAnimation();
        idleRestoreCoroutine = null;
    }

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
}
