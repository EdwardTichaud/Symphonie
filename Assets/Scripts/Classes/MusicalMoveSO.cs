using UnityEngine;
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
    public bool stayFaceToTarget;
    public string[] musicalMoveIntroAnimationNames;
    public string[] musicalMoveAnimationNames;

    [Header("Coût et Dégâts")]
    public float power = 0;
    public float fatigueCost = 1;
    public int harmonicCost = 1;

    [Header("Ciblage")]
    public TargetType targetType = TargetType.SingleEnemy;
    public TargetType defaultTargetType = TargetType.SingleEnemy;
    public List<TargetType> targetTypes = new List<TargetType>() { TargetType.SingleEnemy };

    [Header("Effet appliqué")]
    public MusicalEffectType effectType = MusicalEffectType.Damage;
    public int effectValue = 10;

    public float moveSpeed = 20f;
    public float castDistance;
    public bool stayInPlace = false;
    [Tooltip("Si faux, le move ne peut pas être intercepté")]
    public bool interceptable = true;

    [Header("Placement autour de la cible")]
    public RelativePosition relativePosition = RelativePosition.Front;

    [Header("VFX")]
    public GameObject introVFXPrefab;
    public GameObject hitVFXPrefab;
    [Tooltip("Utiliser la téléportation à la place du déplacement classique")]
    public bool useTeleportation = false;
    [Tooltip("VFX joué au point de départ de la téléportation")]
    public GameObject teleportStartVFXPrefab;
    [Tooltip("VFX joué au point d'arrivée de la téléportation")]
    public GameObject teleportEndVFXPrefab;

    [Header("Camera")]
    [Tooltip("Trajectoire de caméra à instancier lors de l'utilisation du move")]
    public GameObject cameraPathPrefab;

    public void ApplyEffect(CharacterUnit caster, CharacterUnit target)
    {
        float finalValue = effectValue;
        if (caster != null)
            finalValue += caster.currentPower;

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

        if (caster != null && caster.Data.gameplayType == GameplayType.Fatigue)
        {
            caster.GetComponent<FatigueSystem>()?.OnActionPerformed(fatigueCost);
        }
    }
}

public enum MusicalEffectType { Damage, Heal, Buff, Debuff, Sleep, WakeUpAll }

public enum RelativePosition { Front, Back, Left, Right }
