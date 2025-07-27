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
    [Tooltip("Vrai si l'attaque n'est disponible qu'en état Awake")]
    public bool onlyAwake = false;
    public Sprite moveIcon;
    [TextArea] public string description;
    public AnimationClip musicalMoveTargetingAnimation;
    public bool stayFaceToTarget = true;

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

    [Header("Coup Critique")]
    [Tooltip("Active une variante lorsque le QTE est réussi")]
    public bool useCriticalVariant = false;
    public MusicalEffectType criticalEffectType = MusicalEffectType.Damage;
    public int criticalEffectValue = 20;
    public float criticalFatigueCost = 1f;
    public int criticalHarmonicCost = 1;
    public int criticalHarmonicGeneration = 0;

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

    [Tooltip("Si vrai, le lanceur reste à la position cible en fin de move")] public bool stayInPlace = false;

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

    [Header("Effets sonores")]
    [Tooltip("Son d'avertissement joué avant l'attaque pour prévenir le joueur")]
    public AudioClip warningClip;

    [Header("Timing")]
    [Tooltip("Temps en secondes à attendre après la téléportation avant d'exécuter le move")]
    public float startDelay = 2f;

    [Header("Awake")]
    [Tooltip("Si vrai, ce move fait entrer le lanceur en mode Awake")] public bool enterAwake = false;


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
        // Applique d'abord l'effet de base
        ApplySingleEffect(effectType, caster, target, effectValue, fatigueCost, isCritical && !useCriticalVariant);

        // Ajoute l'effet critique si nécessaire
        if (isCritical && useCriticalVariant)
        {
            ApplySingleEffect(criticalEffectType, caster, target, criticalEffectValue, criticalFatigueCost, false);
        }
    }

    /// <summary>
    /// Applique un effet unique en tenant compte de la puissance du lanceur et
    /// du système de fatigue éventuel.
    /// </summary>
    private void ApplySingleEffect(MusicalEffectType typeToUse, CharacterUnit caster,
        CharacterUnit target, int baseValue, float fatigueToApply, bool doubleValue)
    {
        float finalValue = baseValue;
        if (caster != null)
        {
            finalValue += caster.currentPower;
            finalValue *= caster.GetAttackMultiplier();
        }

        // Ancien comportement : simple multiplicateur si aucune variante
        if (doubleValue)
            finalValue *= 2f;

        if (typeToUse == MusicalEffectType.Damage && target.Data.characterType == CharacterType.EnemyUnit)
        {
            // Transmission de la source pour déclencher l'animation directionnelle
            target.TakeDamage(finalValue, caster != null ? caster.transform : null);
            NewBattleManager.Instance?.RegisterDamage(caster, finalValue);
        }
        else if (typeToUse == MusicalEffectType.Heal && target.Data.characterType == CharacterType.SquadUnit)
        {
            target.Heal(finalValue);
        }
        else if (typeToUse == MusicalEffectType.Sleep)
        {
            InventoryManager.Instance?.ApplySleep(target);
        }
        else if (typeToUse == MusicalEffectType.WakeUpAll)
        {
            foreach (var unit in NewBattleManager.Instance.activeCharacterUnits)
            {
                InventoryManager.Instance?.RemoveSleep(unit);
            }
        }
        else if (typeToUse == MusicalEffectType.LoyaltyMark)
        {
            var mark = target.GetComponent<LoyaltyMark>();
            if (mark == null)
                mark = target.gameObject.AddComponent<LoyaltyMark>();
            mark.SetProtector(caster);
        }
        else if (typeToUse == MusicalEffectType.LinkMark)
        {
            if (target.GetComponent<LinkMark>() == null)
                target.gameObject.AddComponent<LinkMark>();
        }

        if (caster != null && caster.Data.gameplayType == GameplayType.Fatigue)
        {
            caster.GetComponent<FatigueSystem>()?.OnActionPerformed(fatigueToApply);
        }
    }
}

public enum MusicalEffectType { Damage, Heal, Buff, Debuff, Sleep, WakeUpAll, LoyaltyMark, LinkMark }

public enum RelativePosition { Front, Back, Left, Right , NC}
