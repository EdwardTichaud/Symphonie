using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Serialization;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor; // Nécessaire uniquement dans l'éditeur pour charger l'item de référence.
#endif

[CreateAssetMenu(fileName = "New Item", menuName = "Symphonie/Item")]
public class ItemData : ScriptableObject
{
    [Header("Identifiant Unique")]
    public string itemID; // String pour correspondre au système de sauvegarde

    public string itemName;
    [TextArea] public string description;
    public Sprite itemIcon;

    [Header("Inventaire & Accessibilité")]
    [Tooltip("Catégorie affichée dans l'inventaire pour guider les nouveaux joueurs.")]
    public string inventoryCategory = "Objet";
    [TextArea, Tooltip("Résumé court utilisé dans l'inventaire et les aides contextuelles.")]
    public string inventorySummary;
    [Tooltip("Si désactivé, l'objet reste dans l'inventaire après utilisation.")]
    public bool consumeOnUse = true;
    [Tooltip("Moves conseillés pour créer des combos efficaces avec cet objet.")]
    public List<MusicalMoveSO> recommendedMusicalMoves = new();

    [System.Serializable]
    public class EffectDefinition
    {
        [Tooltip("Type d'effet appliqué lorsque l'objet est utilisé.")]
        public ItemEffectType type = ItemEffectType.None;
        [Tooltip("Valeur de référence associée à l'effet (dégâts, portée supplémentaire, durée de sommeil...).")]
        public int value = 10;
    }

    [Header("Effets principaux")]
    [Tooltip("Liste des effets appliqués lors de l'utilisation de l'objet. L'ordre détermine l'effet principal utilisé par les systèmes existants.")]
    public List<EffectDefinition> effects = new();

    [SerializeField, HideInInspector, FormerlySerializedAs("effectType")]
    private ItemEffectType legacyEffectType = ItemEffectType.None;
    [SerializeField, HideInInspector, FormerlySerializedAs("effectValue")]
    private int legacyEffectValue = 10;

    private void EnsureEffectsInitialized()
    {
        if (effects == null)
            effects = new List<EffectDefinition>();

        if (effects.Count == 0)
        {
            effects.Add(new EffectDefinition
            {
                type = legacyEffectType,
                value = legacyEffectValue
            });
        }
        else if (effects[0] == null)
        {
            effects[0] = new EffectDefinition
            {
                type = legacyEffectType,
                value = legacyEffectValue
            };
        }
    }

    private EffectDefinition GetPrimaryEffect()
    {
        EnsureEffectsInitialized();
        return effects[0];
    }

    public bool HasEffect(ItemEffectType type)
    {
        EnsureEffectsInitialized();
        foreach (var effect in effects)
        {
            if (effect == null)
                continue;

            if (effect.type == type)
                return true;
        }

        return false;
    }

    public int GetEffectValue(ItemEffectType type, int defaultValue = 0)
    {
        EnsureEffectsInitialized();
        foreach (var effect in effects)
        {
            if (effect == null)
                continue;

            if (effect.type == type)
                return effect.value;
        }

        return defaultValue;
    }

    public int GetTotalEffectValue(ItemEffectType type)
    {
        EnsureEffectsInitialized();
        int total = 0;
        foreach (var effect in effects)
        {
            if (effect == null || effect.type != type)
                continue;

            total += effect.value;
        }

        return total;
    }

    public ItemEffectType PrimaryEffectType => GetPrimaryEffect().type;
    public int PrimaryEffectValue => GetPrimaryEffect().value;

    // Compatibilité avec l'ancien champ unique.
    public ItemEffectType effectType => PrimaryEffectType;
    public int effectValue => PrimaryEffectValue;

    public bool isUsableInBattle = true;
    public float moveSpeed;
    public float castDistance;

    [Tooltip("Si vrai, l'utilisateur doit se déplacer ou se téléporter vers la cible pour l'utiliser")]
    public bool requiresMovement = true;

    [Header("Effets spéciaux")]
    // Délai entre disparition et réapparition lors d'un téléport
    // Utile pour synchroniser les effets visuels/sonores
    [Tooltip("Durée en secondes avant la réapparition après un téléport (0 = instantané)")]
    public float teleportDelay = 0f;

    [Header("Limitations d'utilisation")]
    [Tooltip("Nombre maximum d'utilisations par tour (0 = illimité)")]
    public int maxUsesPerTurn = 0;
    [Tooltip("Nombre maximum d'utilisations par combat (0 = illimité)")]
    public int maxUsesPerBattle = 0;

    [Header("Coup Critique")]
    [Tooltip("Active une variante lorsque le QTE est réussi")]
    public bool useCriticalVariant = false;
    public ItemEffectType criticalEffectType = ItemEffectType.Damage;
    public int criticalEffectValue = 20;

    [Tooltip("Si vrai, le lanceur reste à la position cible après l'action")] public bool stayInPlace;

    [Header("Heal Settings")]
    public int healAmount = 0;
    public bool healIsPercentage = false;

    [Header("Revive Settings")]
    public int revivePercentage = 50;

    [Header("Buff Settings")]
    public BuffStatType buffStat;              // Statistique augmentée
    public int buffAmount;                     // Valeur du bonus
    public float buffDuration;                 // Durée du bonus en tours
    public bool buffIsPercentage = false;      // Interpréter buffAmount en pourcentage ?

    [Header("Debuff Settings")]
    public DebuffStatType debuffStat;          // Statistique diminuée
    public int debuffAmount;                   // Valeur du malus
    public float debuffDuration;               // Durée du malus en tours
    public bool debuffIsPercentage = false;    // Interpréter debuffAmount en pourcentage ?

    [Header("Timing Boost Settings")]
    public TimingBoostType timingType;
    public float timingBoostAmount;
    public float timingDuration;

    [Header("Ciblage")]
    public TargetType defaultTargetType = TargetType.SingleAlly;
    public List<TargetType> targetTypes = new List<TargetType>() { TargetType.SingleAlly };

    [Header("Téléportation")]
    // Effet visuel déclenché au départ du téléport
    public GameObject tpVfx_Start;
    // Effet visuel déclenché à l'arrivée du téléport
    public GameObject tpVfx_End;
    // Son joué au départ du téléport
    public AudioClipSO tpSFx_Start;
    // Son joué à l'arrivée du téléport
    public AudioClipSO tpSFx_End;

    [Header("Timeline")]
    [Tooltip("Timeline jouée lors de la préparation de l'item")]
    public TimelineAsset preparingTimeline;
    [Tooltip("Timeline complète d'utilisation. Un Signal peut y suspendre l'action en mode lent.")]
    [FormerlySerializedAs("performingTimelinePhase1")]
    [FormerlySerializedAs("performingTimeline")]
    public TimelineAsset performingTimeline;
    [Tooltip("Timeline jouée lors du repli après utilisation")]
    public TimelineAsset retreatTimeline;

    [Header("Caméras par phase")]
    [Tooltip("Plan cinématique utilisé lors de la préparation (None = conserver la vue actuelle, OverShoulderCasterLookTarget = ancre Camera_Shoulder_Stay, OverShoulderCasterToTarget = ancre Camera_Shoulder_Moving).")]
    public BattleCameraRole preparingCameraRole = BattleCameraRole.MainMenuIdle;
    [Tooltip("Plan cinématique utilisé pendant l'utilisation (None = conserver la caméra précédente).")]
    public BattleCameraRole performingCameraRole = BattleCameraRole.OverShoulderCasterToTarget;
    [Tooltip("Plan cinématique utilisé lors du repli (None = conserver la caméra précédente).")]
    public BattleCameraRole retreatCameraRole = BattleCameraRole.TargetReaction;

    [Header("Transitions de caméra")]
    [Min(0f)]
    [Tooltip("Durée (en secondes) durant laquelle la caméra reste sur le cadrage de préparation avant de se déplacer vers celui d'utilisation.")]
    public float preparingToPerformingCameraDelay = 0f;

#if UNITY_EDITOR
    // ------------------------------------------------------------------
    // Références par défaut
    // ------------------------------------------------------------------
    // Les nouveaux Items doivent initialiser leurs caméras en se basant sur
    // l'objet d'exemple "Item_LonguePortee". Cela évite de créer des
    // variations involontaires et facilite le travail des game designers.
    private const string LONGUE_PORTEE_PATH =
        "Assets/Items/Item_LonguePortee/Item_LonguePortee.asset";

    private void OnValidate()
    {
        EnsureEffectsInitialized();

        // Récupère l'Item de référence. Si introuvable, on ne fait rien pour
        // éviter les messages d'erreur intempestifs.
        var reference = AssetDatabase.LoadAssetAtPath<ItemData>(LONGUE_PORTEE_PATH);
        if (reference != null)
        {
            // Applique les valeurs par défaut uniquement si les champs sont vides.
            if (preparingCameraRole == BattleCameraRole.None)
                preparingCameraRole = reference.preparingCameraRole;
            if (performingCameraRole == BattleCameraRole.None)
                performingCameraRole = reference.performingCameraRole;
            if (retreatCameraRole == BattleCameraRole.None)
                retreatCameraRole = reference.retreatCameraRole;
        }

        ValidateConfiguration();
    }

    private void ValidateConfiguration()
    {
        bool dirty = false;

        if (string.IsNullOrWhiteSpace(itemID))
            Debug.LogWarning($"[ItemData] itemID est vide pour '{name}'.", this);
        if (string.IsNullOrWhiteSpace(itemName))
            Debug.LogWarning($"[ItemData] itemName est vide pour '{name}'.", this);
        if (itemIcon == null)
            Debug.LogWarning($"[ItemData] itemIcon manquant pour '{name}'.", this);

        if (maxUsesPerTurn < 0)
        {
            maxUsesPerTurn = 0;
            dirty = true;
            Debug.LogWarning($"[ItemData] maxUsesPerTurn negatif corrige pour '{name}'.", this);
        }
        if (maxUsesPerBattle < 0)
        {
            maxUsesPerBattle = 0;
            dirty = true;
            Debug.LogWarning($"[ItemData] maxUsesPerBattle negatif corrige pour '{name}'.", this);
        }

        if (buffDuration < 0f)
        {
            buffDuration = 0f;
            dirty = true;
            Debug.LogWarning($"[ItemData] buffDuration negatif corrige pour '{name}'.", this);
        }
        if (debuffDuration < 0f)
        {
            debuffDuration = 0f;
            dirty = true;
            Debug.LogWarning($"[ItemData] debuffDuration negatif corrige pour '{name}'.", this);
        }

        if (castDistance < 0f)
        {
            castDistance = 0f;
            dirty = true;
            Debug.LogWarning($"[ItemData] castDistance negatif corrige pour '{name}'.", this);
        }

        if (moveSpeed < 0f)
        {
            moveSpeed = 0f;
            dirty = true;
            Debug.LogWarning($"[ItemData] moveSpeed negatif corrige pour '{name}'.", this);
        }

        if (targetTypes == null)
        {
            targetTypes = new List<TargetType>();
            dirty = true;
        }
        if (targetTypes.Count == 0)
        {
            targetTypes.Add(defaultTargetType);
            dirty = true;
            Debug.LogWarning($"[ItemData] targetTypes vide, ajout du defaultTargetType pour '{name}'.", this);
        }
        else if (!targetTypes.Contains(defaultTargetType))
        {
            targetTypes.Add(defaultTargetType);
            dirty = true;
            Debug.LogWarning($"[ItemData] defaultTargetType manquant dans targetTypes pour '{name}'.", this);
        }

        if (requiresMovement && castDistance <= 0f)
            Debug.LogWarning($"[ItemData] requiresMovement actif avec castDistance <= 0 pour '{name}'.", this);

        if (beatPattern != null)
        {
            for (int i = 0; i < beatPattern.Count; i++)
            {
                if (beatPattern[i] <= 0f)
                {
                    Debug.LogWarning($"[ItemData] beatPattern contient une valeur <= 0 pour '{name}'.", this);
                    break;
                }
            }
        }

        if (dirty)
            EditorUtility.SetDirty(this);
    }
#endif

    [Header("QTE Pattern")]
    public List<float> beatPattern;

    [System.Serializable]
    public class NoteVariant
    {
        public string label = "Frappe 1";
        public AudioClipSO baseNote;
        public AudioClipSO onParry;
        public AudioClipSO onEvade;
        public AudioClipSO onHit;
    }

    [Header("Notes avec Variantes Sonores")]
    public List<NoteVariant> notes;

    public void ApplyEffect(CharacterUnit caster, CharacterUnit target)
    {
        ApplyEffect(caster, target, false);
    }

    public void ApplyEffect(CharacterUnit caster, CharacterUnit target, bool isCritical)
    {
        EnsureEffectsInitialized();

        // Applique l'ensemble des effets configurés
        foreach (var effect in effects)
        {
            if (effect == null)
                continue;

            ApplySingleEffect(effect.type, caster, target, effect.value);
        }

        // Ajoute l'effet critique si nécessaire
        if (isCritical && useCriticalVariant)
        {
            ApplySingleEffect(criticalEffectType, caster, target, criticalEffectValue);
        }
    }

    /// <summary>
    /// Applique un effet unique selon son type.
    /// </summary>
    private void ApplySingleEffect(ItemEffectType type, CharacterUnit caster, CharacterUnit target, float value = 0f)
    {
        switch (type)
        {
            case ItemEffectType.Heal:
                ApplyHeal(target);
                break;
            case ItemEffectType.Revive:
                ApplyRevive(target);
                break;
            case ItemEffectType.Buff:
                ApplyBuff(target);
                break;
            case ItemEffectType.Debuff:
                ApplyDebuff(target);
                break;
            case ItemEffectType.BoostTiming:
                Debug.Log("[ItemData] Effet BoostTiming non implémenté.");
                break;
            case ItemEffectType.Damage:
                ApplyDamage(caster, target, value);
                break;
            case ItemEffectType.IncreaseRange:
                ApplyIncreaseRange(target, value);
                break;
            case ItemEffectType.PreventInterception:
                ApplyInterceptionImmunity(target);
                break;
            case ItemEffectType.ExtendEffects:
                ApplyExtendEffects(target);
                break;
            case ItemEffectType.Sleep:
                ApplySleep(target, value);
                break;
            case ItemEffectType.Stun:
                ApplyStun(target, value);
                break;
            case ItemEffectType.WakeUp:
                ApplyWakeUp(target);
                break;
            default:
                Debug.LogWarning($"[ItemData] Type d'effet inconnu : {effectType}");
                break;
        }
    }

    private void ApplyHeal(CharacterUnit target)
    {
        if (target == null)
            return;

        float amount = healIsPercentage
            ? (target.Data.baseHP + target.currentVitality) * healAmount / 100f
            : healAmount;

        CombatPipeline.ApplyHealing(null, target, amount, new CombatPipeline.HealOptions
        {
            includePower = false,
            useSagacity = false,
            applyAttackMultiplier = false,
            applyModifiers = false,
            clampToBaseValue = false,
            valueMultiplier = 1f
        });
    }

    private void ApplyRevive(CharacterUnit target)
    {
        if (target == null || target.currentHP > 0)
            return;

        float maxHP = target.Data.baseHP + target.currentVitality;
        float amount = maxHP * revivePercentage / 100f;
        target.currentHP = Mathf.Clamp(amount, 0f, maxHP);
        if (target.hpBar != null)
            target.hpBar.SetValue(target.currentHP);
    }

    private void ApplyBuff(CharacterUnit target)
    {
        CharacterStatusEffectController.ApplyBuff(target, buffStat, buffAmount, buffDuration, buffIsPercentage);
    }

    private void ApplyDebuff(CharacterUnit target)
    {
        // Utilise les champs dédiés aux débuffs pour éviter toute confusion
        CharacterStatusEffectController.ApplyDebuff(target, debuffStat, debuffAmount, debuffDuration, debuffIsPercentage);
    }

    private void ApplyDamage(CharacterUnit caster, CharacterUnit target, float value)
    {
        if (target == null)
            return;

        CombatPipeline.ApplyDamage(caster, target, value, new CombatPipeline.DamageOptions
        {
            includePower = false,
            applyAttackMultiplier = caster != null,
            applyModifiers = false,
            clampToBaseValue = false,
            registerDamage = false,
            allowRedirect = true,
            valueMultiplier = 1f
        });
    }

    private void ApplyIncreaseRange(CharacterUnit target, float value)
    {
        if (target != null)
            target.currentRange += value;
    }

    private void ApplyInterceptionImmunity(CharacterUnit target)
    {
        CharacterStatusEffectController.ApplyInterceptionImmunity(target, Mathf.RoundToInt(buffDuration));
    }

    private void ApplyExtendEffects(CharacterUnit target)
    {
        CharacterStatusEffectController.ExtendEffectDurations(target, buffDuration);
    }

    private void ApplySleep(CharacterUnit target, float value)
    {
        CharacterStatusEffectController.ApplySleep(target, Mathf.Max(1, Mathf.RoundToInt(value)));
    }

    private void ApplyStun(CharacterUnit target, float value)
    {
        CharacterStatusEffectController.ApplyStun(target, Mathf.Max(1, Mathf.RoundToInt(value)));
    }

    private void ApplyWakeUp(CharacterUnit target)
    {
        CharacterStatusEffectController.RemoveSleep(target);
    }
}

public enum ItemEffectType { None, Heal, Revive, Buff, Debuff, BoostTiming, Damage, IncreaseRange, PreventInterception, ExtendEffects, Sleep, WakeUp, Stun }
public enum BuffStatType { None, Strength, Defense, Initiative, MaxHP }
public enum DebuffStatType { None, Strength, Defense, Initiative, MaxHP }
public enum TimingBoostType { None, ParryWindow, DodgeWindow }
