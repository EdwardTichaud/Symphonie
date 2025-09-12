using UnityEngine;
using UnityEngine.Timeline;
using System.Collections.Generic;
// Les directives UnityEditor sont réservées à l'éditeur et ne doivent pas
// être incluses dans le build du joueur. Elles ont été retirées pour éviter
// les erreurs de compilation lors de l'export.

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
    // L'animation de ciblage est désormais pilotée par la Timeline de préparation
    public bool stayFaceToTarget = true;

    [System.Serializable]
    public class NoteData
    {
        [Tooltip("Input à utiliser pour réussir le QTE")]
        public Sprite noteInput;
        [Tooltip("Délai avant que la note se joue (par rapport au début ou à la note d'avant)")]
        public float rhythm = 0.5f;
    }

    [Header("Partition musicale")]
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

    [Header("Limitations d'utilisation")]
    [Tooltip("Nombre maximum d'utilisations par tour (0 = illimité)")]
    public int maxUsesPerTurn = 0;
    [Tooltip("Nombre maximum d'utilisations par combat (0 = illimité)")]
    public int maxUsesPerBattle = 0;

    [Header("Condition d'altitude")]
    [Tooltip("Détermine si le move est utilisable lorsque la cible est au sol, en l'air ou dans les deux cas")]
    public AltitudeCondition altitudeCondition = AltitudeCondition.GroundOrAir; // Par défaut: aucune restriction

    [Header("Ciblage")]
    public TargetType targetType = TargetType.SingleEnemy;
    public TargetType defaultTargetType = TargetType.SingleEnemy;
    public List<TargetType> targetTypes = new List<TargetType>() { TargetType.SingleEnemy };

    [Header("Effet appliqué")]
    public MusicalEffectType effectType = MusicalEffectType.Damage;
    public int effectValue = 10;

    [Header("Effets spéciaux")]
    [Tooltip("Prefab instancié pour certains effets spéciaux comme la création ou la suppression de sol.")]
    public GameObject effectPrefab;

    public float moveSpeed = 20f;
    public float castDistance;

    [Tooltip("Si vrai, le lanceur reste à la position cible en fin de move")] public bool stayInPlace = false;

    [Tooltip("Si faux, le move ne peut pas être intercepté")]
    public bool interceptable = true;

    [Header("Placement autour de la cible")]
    public RelativePosition relativePosition = RelativePosition.Front;

    [Header("Téléportation")]
    // Effet visuel déclenché au départ du téléport
    public GameObject tpVfx_Start;
    // Effet visuel déclenché à l'arrivée du téléport
    public GameObject tpVfx_End;
    // Son joué au départ du téléport
    public AudioClip tpSFx_Start;
    // Son joué à l'arrivée du téléport
    public AudioClip tpSFx_End;

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
    [Tooltip("Timeline jouée lors du repli après l'exécution du move")]
    public TimelineAsset retreatTimeline;

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
        else if (typeToUse == MusicalEffectType.SpawnGround)
        {
            // Crée un sol temporaire sous la cible afin de permettre l'utilisation de moves terrestres
            // sur des unités normalement en vol.
            if (effectPrefab != null && target != null)
            {
                GameObject ground = Instantiate(effectPrefab, target.transform.position, Quaternion.identity);
                // Assure que l'objet utilise bien le layer Battle_Ground pour être reconnu comme sol.
                ground.layer = LayerMask.NameToLayer("Battle_Ground");
            }
        }
        else if (typeToUse == MusicalEffectType.RemoveGround)
        {
            // Supprime visuellement le sol sous la cible en instanciant un prefab dédié,
            // la forçant ainsi à être considérée comme aérienne.
            if (effectPrefab != null && target != null)
            {
                Instantiate(effectPrefab, target.transform.position, Quaternion.identity);
            }
        }

        if (caster != null && caster.Data.gameplayType == GameplayType.Fatigue)
        {
            caster.GetComponent<FatigueSystem>()?.OnActionPerformed(fatigueToApply);
        }
    }
}

public enum MusicalEffectType { Damage, Heal, Buff, Debuff, Sleep, WakeUpAll, LoyaltyMark, LinkMark, SpawnGround, RemoveGround }

public enum RelativePosition { Front, Back, Left, Right , NC}

/// <summary>
/// Définit dans quel contexte d'altitude un <see cref="MusicalMoveSO"/> est réalisable.
/// </summary>
public enum AltitudeCondition { GroundOnly, AirOnly, GroundOrAir }
