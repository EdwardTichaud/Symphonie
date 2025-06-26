using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "Symphonie/CharacterData")]
public class CharacterData : ScriptableObject, ITargetable
{
    [Header("General Info")]    
    public string characterName;
    public Sprite portrait;
    public CharacterType characterType;
    public GameObject characterWorldModel;
    public GameObject characterBattleModel;

    [Header("Battlefield")]
    public int battlefieldIndex = 0; // Indice du battlefield dans la zone

    [Header("Stats")]
    public int baseInitiative;
    public float baseRange;
    public int baseHP;
    public int baseMP;
    public int baseStrength;
    public int baseDefense;
    public int baseMusicalGauge;
    public int baseFatigue;
    public int maxFatigue;
    public int baseRage;
    public int maxRage;
    public float rageDamageMultiplier = 0.1f;
    public int baseReflex;
    public float baseMobility;
    public int baseVitality;
    public int basePower;
    public int baseStability;
    public int baseSagacity;

    [Header("Animation Idle en attaque")]
    public string battleIdleAnimationName;

    [Header("Musical Attacks")]
    public MusicalMoveSO[] musicalAttacks;

    [Header("Etat (runtime)")]
    public int currentInitiative;
    public float currentRange;
    public int currentHP;
    public int currentMP;
    public int currentStrength;
    public int currentDefense;
    public int currentPower;
    public int currentStability;
    public int currentVitality;
    public int currentSagacity;
    public bool isPlayerControlled;
    public int currentRage;
    public int currentFatigue;
    public float currentReflex;
    public float currentMobility;

    [Header("Effets visuels et sonores")]
    public AudioClip hitSound;
    public GameObject hitEffect;
    public GameObject deathEffect;

    // Ajoute une référence au GameObject source
    public MonoBehaviour owner;

    private void OnEnable()
    {
        // Assure que, quand on clone, on part des bonnes valeurs de base
        currentInitiative = baseInitiative;
        currentHP = baseHP + baseVitality;
        currentMP = baseMP;
        currentStrength = baseStrength;
        currentDefense = baseDefense;
        currentRage = baseRage;
        currentPower = basePower;
        currentStability = baseStability;
        currentVitality = baseVitality;
        currentSagacity = baseSagacity;
        currentReflex = baseReflex;
        currentMobility = baseMobility;
    }

    public Transform GetTransform()
    {
        return owner != null ? owner.transform : null;
    }
}

public enum CharacterType { SquadUnit, EnemyUnit }

