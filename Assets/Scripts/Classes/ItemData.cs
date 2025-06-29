using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Item", menuName = "Symphonie/Item")]
public class ItemData : ScriptableObject
{
    [Header("Identifiant Unique")]
    public string itemID; // String pour correspondre au système de sauvegarde

    public string itemName;
    [TextArea] public string description;
    public Sprite itemIcon;

    public int effectValue = 10;
    public bool isUsableInBattle = true;
    public float moveSpeed;
    public float castDistance;
    public bool stayInPlace;

    public AnimationClip itemTargetingAnimation;

    public ItemEffectType effectType;

    [Header("Heal Settings")]
    public int healAmount = 0;
    public bool healIsPercentage = false;

    [Header("Revive Settings")]
    public int revivePercentage = 50;

    [Header("Buff Settings")]
    public BuffStatType buffStat;
    public DebuffStatType debuffStat;
    public int buffAmount;
    public int debuffAmount;
    public float buffDuration;
    public bool buffIsPercentage = false;

    [Header("Timing Boost Settings")]
    public TimingBoostType timingType;
    public float timingBoostAmount;
    public float timingDuration;

    [Header("Ciblage")]
    public TargetType defaultTargetType = TargetType.SingleAlly;
    public List<TargetType> targetTypes = new List<TargetType>() { TargetType.SingleAlly };

    [Header("VFX")]
    public GameObject introVFXPrefab;

    [Header("Camera")]
    [Tooltip("Trajectoire de caméra à instancier lors de l'utilisation de l'item")]
    public GameObject cameraPathPrefab;

    [Header("QTE Pattern")]
    public List<float> beatPattern;

    [Header("Caméra - Séquence de positionnement relatif au lanceur")]
    public Transform currentCaster;
    public Transform currentTarget;
    public Vector3 cameraStartLocalPosition;
    public Vector3 cameraStartLocalEulerAngles;
    public Vector3 cameraEndLocalPosition;
    public Vector3 cameraEndLocalEulerAngles;
    public float cameraTransitionDuration = 0.5f;

    [System.Serializable]
    public class NoteVariant
    {
        public string label = "Frappe 1";
        public AudioClip baseNote;
        public AudioClip onParry;
        public AudioClip onEvade;
        public AudioClip onHit;
    }

    [Header("Notes avec Variantes Sonores")]
    public List<NoteVariant> notes;

    [Header("Visuel et FX")]
    public AnimationClip introAnimationClip;
    public AnimationClip animationClip;
    public GameObject visualEffect;
    public AudioClip introClip;

    public void ApplyEffect(CharacterUnit target)
    {
        switch (effectType)
        {
            case ItemEffectType.Heal:
                if (target != null)
                {
                    float amount = healIsPercentage
                        ? (target.Data.baseHP + target.currentVitality) * healAmount / 100f
                        : healAmount;
                    target.Heal(amount);
                }
                break;

            case ItemEffectType.Revive:
                if (target != null && target.currentHP <= 0)
                {
                    float maxHP = target.Data.baseHP + target.currentVitality;
                    float amount = maxHP * revivePercentage / 100f;
                    target.currentHP = Mathf.Clamp(amount, 0f, maxHP);
                    if (target.hpBar != null)
                        target.hpBar.SetValue(target.currentHP);
                }
                break;

            case ItemEffectType.Buff:
                InventoryManager.Instance?.ApplyBuff(target, buffStat, buffAmount, buffDuration, buffIsPercentage);
                break;

            case ItemEffectType.Debuff:
                InventoryManager.Instance?.ApplyDebuff(target, debuffStat, debuffAmount, buffDuration, buffIsPercentage);
                break;

            case ItemEffectType.BoostTiming:
                Debug.Log("[ItemData] Effet BoostTiming non implémenté.");
                break;

            case ItemEffectType.Damage:
                if (target != null)
                    target.TakeDamage(effectValue);
                break;

            case ItemEffectType.IncreaseRange:
                if (target != null)
                {
                    target.Data.currentRange += effectValue;
                }
                break;

            case ItemEffectType.ExtendEffects:
                InventoryManager.Instance?.ExtendAllModifiers(target, 2f);
                break;

            default:
                Debug.LogWarning($"[ItemData] Type d'effet inconnu : {effectType}");
                break;
        }
    }
}

public enum ItemEffectType { None, Heal, Revive, Buff, Debuff, BoostTiming, Damage, IncreaseRange, ExtendEffects }
public enum BuffStatType { None, Strength, Defense, Initiative }
public enum DebuffStatType { None, Strength, Defense, Initiative }
public enum TimingBoostType { None, ParryWindow, DodgeWindow }
