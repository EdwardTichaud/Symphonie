using UnityEngine;
using UnityEngine.Timeline;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;

[CreateAssetMenu(fileName = "NewMusicalMove", menuName = "Symphonie/Musical Move")]
public class MusicalMoveSO : ScriptableObject
{
    [Header("Identité")]
    public string moveName;
    public enum MoveType { Empty, Attack, Buff, Debuff}
    public MoveType moveType;
    public Sprite moveIcon;
    [TextArea] public string description;
    public AnimationClip musicalMoveTargetingAnimation;
    public string musicalMoveRunAnimationName;
    public float maxRunDuration = 0.5f;
    public bool stayFaceToTarget = true;
    public string[] musicalMoveIntroAnimationNames;
    public string[] musicalMoveAnimationNames;

    [System.Serializable]
    public class NoteData
    {
        public AudioClip clip;
        [Tooltip("Durée de la fenêtre QTE pour cette note (en secondes)")]
        public float rhythm = 0.5f;
    }

    [Header("Partition musicale")]
    [Range(2, 6)]
    public List<NoteData> notes = new();

    [Header("Coût et Dégâts")]
    public float power = 0;
    public float fatigueCost = 1;
    public int harmonicCost = 1;
    public int harmonicGeneration = 0;

    [Header("Temps de recharge")]
    [Tooltip("Nombre de tours avant de pouvoir réutiliser le move")]
    public int cooldown = 0;

    [Header("Ciblage")]
    public TargetType targetType = TargetType.SingleEnemy;
    public TargetType defaultTargetType = TargetType.SingleEnemy;
    public List<TargetType> targetTypes = new List<TargetType>() { TargetType.SingleEnemy };

    [Header("Effet appliqué")]
    public MusicalEffectType effectType = MusicalEffectType.Damage;
    public int effectValue = 10;

    public float moveSpeed = 20f;
    public float castDistance;
    [Tooltip("Obsolète : déplacement remplacé par la téléportation")] public bool stayInPlace = false;
    [Tooltip("Si faux, le move ne peut pas être intercepté")]
    public bool interceptable = true;

    [Header("Placement autour de la cible")]
    public RelativePosition relativePosition = RelativePosition.Front;

    [Header("VFX")]
    public GameObject introVFXPrefab;
    public GameObject hitVFXPrefab;
    [Tooltip("Obsolète : la téléportation est toujours active")] public bool useTeleportation = true;
    [Tooltip("VFX joué au point de départ de la téléportation")]
    public GameObject teleportStartVFXPrefab;
    [Tooltip("VFX joué au point d'arrivée de la téléportation")]
    public GameObject teleportEndVFXPrefab;


    [Header("Timeline")]
    [Tooltip("Timeline jouée lors de la préparation du move")]
    public TimelineAsset preparingTimeline;
    [Tooltip("Timeline à jouer lors de l'exécution du move")]
    public TimelineAsset performingTimeline;

    public void ApplyEffect(CharacterUnit caster, CharacterUnit target)
    {
        ApplyEffect(caster, target, false);
    }

    public void ApplyEffect(CharacterUnit caster, CharacterUnit target, bool isCritical)
    {
        float finalValue = effectValue;
        if (caster != null)
        {
            finalValue += caster.currentPower;
            finalValue *= caster.GetAttackMultiplier();
        }

        if (isCritical)
            finalValue *= 2f;

        if (effectType == MusicalEffectType.Damage && target.Data.characterType == CharacterType.EnemyUnit)
        {
            target.TakeDamage(finalValue);
            NewBattleManager.Instance?.RegisterDamage(caster, finalValue);
        }
        else if (effectType == MusicalEffectType.Heal && target.Data.characterType == CharacterType.SquadUnit)
        {
            target.Heal(finalValue);
        }
        else if (effectType == MusicalEffectType.Sleep)
        {
            InventoryManager.Instance?.ApplySleep(target);
        }
        else if (effectType == MusicalEffectType.WakeUpAll)
        {
            foreach (var unit in NewBattleManager.Instance.activeCharacterUnits)
            {
                InventoryManager.Instance?.RemoveSleep(unit);
            }
        }
        else if (effectType == MusicalEffectType.LoyaltyMark)
        {
            var mark = target.GetComponent<LoyaltyMark>();
            if (mark == null)
                mark = target.gameObject.AddComponent<LoyaltyMark>();
            mark.SetProtector(caster);
        }
        else if (effectType == MusicalEffectType.LinkMark)
        {
            if (target.GetComponent<LinkMark>() == null)
                target.gameObject.AddComponent<LinkMark>();
        }

        if (caster != null && caster.Data.gameplayType == GameplayType.Fatigue)
        {
            caster.GetComponent<FatigueSystem>()?.OnActionPerformed(fatigueCost);
        }
    }
}

public enum MusicalEffectType { Damage, Heal, Buff, Debuff, Sleep, WakeUpAll, LoyaltyMark, LinkMark }

public enum RelativePosition { Front, Back, Left, Right }
