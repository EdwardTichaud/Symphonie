using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;

[CreateAssetMenu(fileName = "NewMusicalMove", menuName = "Symphonie/Musical Move")]
public class MusicalMoveSO : ScriptableObject
{
    [Header("Identité")]
    public string moveName;
    public enum MoveType { Attack, Parry, Dodge }
    public MoveType moveType;
    public Sprite moveIcon;
    [TextArea] public string description;
    public string musicalMoveTargetingAnimationName;
    public string musicalMoveRunAnimationName;
    public float maxRunDuration = 0.5f;
    public bool stayFaceToTarget;
    public string[] musicalMoveIntroAnimationNames;
    public string[] musicalMoveAnimationNames;

    [Header("Coût et Dégâts")]
    public float power = 0;
    public float fatigueCost = 1;

    [Header("Ciblage")]
    public TargetType targetType = TargetType.SingleEnemy;
    public TargetType defaultTargetType = TargetType.SingleEnemy;

    [Header("Effet appliqué")]
    public MusicalEffectType effectType = MusicalEffectType.Damage;
    public int effectValue = 10;

    public float moveSpeed = 20f;
    public float castDistance;
    public bool stayInPlace = false;

    [Header("Placement autour de la cible")]
    public RelativePosition relativePosition = RelativePosition.Front;

    [Header("VFX")]
    public GameObject introVFXPrefab;
    public GameObject hitVFXPrefab;

    public void ApplyEffect(CharacterUnit caster, CharacterUnit target)
    {
        float finalValue = effectValue;
        if (caster != null)
            finalValue += caster.currentPower;

        if (effectType == MusicalEffectType.Damage && target.Data.characterType == CharacterType.EnemyUnit)
        {
            target.TakeDamage(finalValue);
        }
        else if (effectType == MusicalEffectType.Heal && target.Data.characterType == CharacterType.SquadUnit)
        {
            target.Heal(finalValue);
        }

        if (caster != null && caster.Data.gameplayType == GameplayType.Fatigue)
        {
            caster.GetComponent<FatigueSystem>()?.OnActionPerformed(fatigueCost);
        }
    }
}

public enum MusicalEffectType { Damage, Heal, Buff, Debuff }

public enum RelativePosition { Front, Back, Left, Right }
