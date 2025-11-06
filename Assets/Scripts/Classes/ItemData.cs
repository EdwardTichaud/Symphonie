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

    public int effectValue = 10;
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

    // Le ciblage visuel est désormais géré par la Timeline de préparation
    public ItemEffectType effectType;

    [Header("Heal Settings")]
    public int healAmount = 0;
    public bool healIsPercentage = false;

    [Header("Revive Settings")]
    public int revivePercentage = 50;

    [Header("Buff Settings")]
    public BuffStatType buffStat;              // Statistique augmentée
    public int buffAmount;                     // Valeur du bonus
    public float buffDuration;                 // Durée du bonus en secondes/tours
    public bool buffIsPercentage = false;      // Interpréter buffAmount en pourcentage ?

    [Header("Debuff Settings")]
    public DebuffStatType debuffStat;          // Statistique diminuée
    public int debuffAmount;                   // Valeur du malus
    public float debuffDuration;               // Durée du malus en secondes/tours
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
        // Récupère l'Item de référence. Si introuvable, on ne fait rien pour
        // éviter les messages d'erreur intempestifs.
        var reference = AssetDatabase.LoadAssetAtPath<ItemData>(LONGUE_PORTEE_PATH);
        if (reference == null)
            return;

        // Applique les valeurs par défaut uniquement si les champs sont vides.
        if (preparingCameraRole == BattleCameraRole.None)
            preparingCameraRole = reference.preparingCameraRole;
        if (performingCameraRole == BattleCameraRole.None)
            performingCameraRole = reference.performingCameraRole;
        if (retreatCameraRole == BattleCameraRole.None)
            retreatCameraRole = reference.retreatCameraRole;
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
        // Applique d'abord l'effet principal de l'objet
        ApplySingleEffect(effectType, caster, target, effectValue);

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
                ApplySleep(target);
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

        target.Heal(amount);
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
        if (target != null)
        {
            float damage = value;
            if (caster != null)
                damage *= caster.GetAttackMultiplier();
            // Précise la source pour jouer l'animation adéquate
            target.TakeDamage(damage, caster != null ? caster.transform : null);
        }
    }

    private void ApplyIncreaseRange(CharacterUnit target, float value)
    {
        if (target != null)
            target.Data.currentRange += value;
    }

    private void ApplyInterceptionImmunity(CharacterUnit target)
    {
        CharacterStatusEffectController.ApplyInterceptionImmunity(target, Mathf.RoundToInt(buffDuration));
    }

    private void ApplyExtendEffects(CharacterUnit target)
    {
        CharacterStatusEffectController.ExtendEffectDurations(target, buffDuration);
    }

    private void ApplySleep(CharacterUnit target)
    {
        CharacterStatusEffectController.ApplySleep(target);
    }

    private void ApplyWakeUp(CharacterUnit target)
    {
        CharacterStatusEffectController.RemoveSleep(target);
    }
}

public enum ItemEffectType { None, Heal, Revive, Buff, Debuff, BoostTiming, Damage, IncreaseRange, PreventInterception, ExtendEffects, Sleep, WakeUp }
public enum BuffStatType { None, Strength, Defense, Initiative }
public enum DebuffStatType { None, Strength, Defense, Initiative }
public enum TimingBoostType { None, ParryWindow, DodgeWindow }
