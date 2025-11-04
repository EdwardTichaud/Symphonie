using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// Décrit l'ensemble des animations utilisables par un personnage en combat.
/// L'objectif est de privilégier l'Animator pour la majorité des actions afin
/// de limiter le recours aux timelines coûteuses à maintenir.
/// </summary>
[CreateAssetMenu(menuName = "Symphonie/Combat/Animation Groups", fileName = "CombatAnimationGroups")]
public class CombatAnimationGroups : ScriptableObject
{
    [SerializeField, Tooltip("Liste sérialisée des animations disponibles triées par identifiant logique.")]
    private CombatAnimationSlot[] animations = Array.Empty<CombatAnimationSlot>();

    private readonly Dictionary<CombatAnimationKey, CombatAnimationDefinition> lookup =
        new Dictionary<CombatAnimationKey, CombatAnimationDefinition>();

    /// <summary>
    /// Fournit un accès en lecture à toutes les animations déclarées, utile pour les outils éditeur.
    /// </summary>
    public IReadOnlyList<CombatAnimationSlot> Animations => animations;

    private void OnEnable()
    {
        BuildLookup();
    }

    private void OnValidate()
    {
        BuildLookup();
    }

    /// <summary>
    /// Reconstitue le dictionnaire interne à partir du tableau sérialisé.
    /// </summary>
    private void BuildLookup()
    {
        lookup.Clear();

        if (animations == null)
            return;

        foreach (var slot in animations)
        {
            if (slot.definition == null)
                continue;

            lookup[slot.key] = slot.definition;
        }
    }

    /// <summary>
    /// Recherche la définition associée à une clé d'animation donnée.
    /// </summary>
    public bool TryGetAnimation(CombatAnimationKey key, out CombatAnimationDefinition definition)
    {
        if (lookup.Count == 0)
            BuildLookup();

        return lookup.TryGetValue(key, out definition) && definition != null;
    }

    /// <summary>
    /// Retourne la définition associée à la clé demandée ou <c>null</c> si elle n'existe pas.
    /// </summary>
    public CombatAnimationDefinition GetAnimationOrDefault(CombatAnimationKey key)
    {
        TryGetAnimation(key, out var def);
        return def;
    }

    [Serializable]
    public struct CombatAnimationSlot
    {
        [Tooltip("Identifiant logique de l'animation.")]
        public CombatAnimationKey key;

        [Tooltip("Données décrivant l'animation à lancer (state Animator, paramètres et fallback Timeline).")]
        public CombatAnimationDefinition definition;
    }
}

/// <summary>
/// Liste des animations supportées par l'Animator de combat.
/// </summary>
public enum CombatAnimationKey
{
    None = 0,
    Idle,
    IdleVariantA,
    IdleVariantB,
    MoveBlendTree,
    ApproachBlendTree,
    RetreatBlendTree,
    GuardHold,
    PrepareHit,
    Defend,
    AimSkill,
    AimItem,
    SkillUseDynamic,
    ItemUseDynamic,
    AttackChainA_Light,
    AttackChainA_Heavy,
    AttackChainA_Thrust,
    AttackChainA_Cleave,
    AttackProjectile_Start,
    AttackChainB_Light,
    AttackChainB_Heavy,
    AttackChainB_Finisher,
    AttackProjectile_Release,
    CastLow,
    CastMid,
    CastMax,
    EvadeForward,
    EvadeBackward,
    EvadeLeft,
    EvadeRight,
    HitFront,
    HitBack,
    HitLeft,
    HitRight,
    Knockdown,
    GetUp,
    Death,
    Turn90Left,
    Turn90Right,
    Turn180,
    EnterCombat,
    BattlePose,
    ItemUseStatic,
}

/// <summary>
/// Paramétrage détaillé d'une animation de combat.
/// </summary>
[Serializable]
public class CombatAnimationDefinition
{
    [Tooltip("Nom du state Animator à jouer (chemin complet si contenu dans un sous-state machine).")]
    public string animatorStateName;

    [Tooltip("Index de layer Animator sur lequel l'état est déclaré (0 = base layer).")]
    public int layerIndex = 0;

    [Tooltip("Durée du crossfade appliqué lors de la transition vers cet état.")]
    public float crossFadeDuration = 0.08f;

    [Tooltip("Forcer l'utilisation de la Timeline fournie même si un state Animator est renseigné.")]
    public bool useTimelineByDefault = false;

    [Tooltip("Timeline alternative à jouer lorsque l'Animator ne dispose pas de l'état demandé.")]
    public TimelineAsset timeline;

    [Tooltip("Liste des paramètres Animator à appliquer avant de lancer le state.")]
    public AnimatorParameterOverride[] parameterOverrides = Array.Empty<AnimatorParameterOverride>();
}

/// <summary>
/// Type de paramètre Animator supporté par les overrides.
/// </summary>
public enum AnimatorParameterType
{
    Float,
    Int,
    Bool,
    Trigger,
}

/// <summary>
/// Valeur à appliquer sur un paramètre Animator avant de déclencher l'animation.
/// </summary>
[Serializable]
public class AnimatorParameterOverride
{
    [Tooltip("Nom exact du paramètre Animator à modifier.")]
    public string parameterName;

    [Tooltip("Type de paramètre (Float, Int, Bool ou Trigger).")]
    public AnimatorParameterType parameterType = AnimatorParameterType.Float;

    [Tooltip("Valeur flottante à appliquer lorsque le type est Float.")]
    public float floatValue;

    [Tooltip("Valeur entière utilisée pour les paramètres Int.")]
    public int intValue;

    [Tooltip("Valeur booléenne pour les paramètres Bool.")]
    public bool boolValue;
}
