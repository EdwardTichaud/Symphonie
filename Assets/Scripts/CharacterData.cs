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

    [Header("UI")]
    public GameObject uiPrefab;

    [Header("Stats")]
    public int baseInitiative;
    public float baseRange;
    public int baseHP;
    public int baseMP;
    public int baseStrength;
    public int baseDefense;
    public int baseMusicalGauge;
    public int baseRage;
    public int maxRage;
    public float rageDamageMultiplier = 0.1f;
    public int baseReflex;
    public float baseMobility;

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
    public bool isPlayerControlled;

    [Header("Effets visuels et sonores")]
    public AudioClip hitSound;
    public GameObject hitEffect;
    public GameObject deathEffect;

    public float currentReflex;
    public float currentMobility;
    public int currentRage;
    // Ajoute une référence au GameObject source
    public MonoBehaviour owner;

    private void OnEnable()
    {
        // Assure que, quand on clone, on part des bonnes valeurs de base
        currentInitiative = baseInitiative;
        currentHP = baseHP;
        currentMP = baseMP;
        currentStrength = baseStrength;
        currentDefense = baseDefense;
        currentRage = baseRage;
    }

    public Transform GetTransform()
    {
        return owner != null ? owner.transform : null;
    }
}

public enum CharacterType { SquadUnit, EnemyUnit }
