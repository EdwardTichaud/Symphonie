using UnityEngine;
using System.Collections.Generic; // Permet l'utilisation de listes génériques

[CreateAssetMenu(fileName = "CharacterData", menuName = "Symphonie/CharacterData")]
public class CharacterData : ScriptableObject, ITargetable
{
    [Header("General Info")]
    public string characterName;
    public Sprite portrait;
    // Liste de sprites utilisés pour l'écran Versus et les points d'apparition
    // Un sprite est choisi aléatoirement lors de l'affichage de l'écran Versus
    public List<Sprite> versusSprites = new();
    public CharacterType characterType;
    public GameplayType gameplayType = GameplayType.Rage;
    public HarmonicType harmonicType = HarmonicType.Lumiere;
    public GameObject characterWorldModel;
    public GameObject characterBattleModel;

    [Header("Battlefield")]
    public int battlefieldIndex = 0; // Indice du battlefield dans la zone

    [Header("Stats")]
    [Header("Attributs")]
    public float baseReflex;
    public float baseMobility;
    public float baseVitality;
    public float basePower;
    public float baseStability;
    public float baseSagacity;

    [Header("Common Stats")]
    public float baseInitiative;
    public float baseRange;
    public float baseHP;
    public float baseStrength;
    public float baseDefense;
    public float baseSpeed;
    public float baseInterceptionRange;

    [Header("Custom Stats")]
    [Header("Lucian")]
    public float baseRage;
    public float maxRage;
    public float rageDamageMultiplier;

    [Header("Thalia")]
    public float baseFatigue;
    public float maxFatigue;

    [Header("Musical Attacks")]
    public MusicalMoveSO[] musicalAttacks;
    [Tooltip("Attaque musicale toujours disponible")] public MusicalMoveSO specialMusicalMove;

    [Header("Etat (runtime)")]
    public float currentInitiative;
    public float currentRange;
    public float currentHP;
    public float currentStrength;
    public float currentDefense;
    public float currentPower;
    public float currentStability;
    public float currentVitality;
    public float currentSagacity;
    public bool isPlayerControlled;
    public float currentRage;
    public float currentFatigue;
    public float currentReflex;
    public float currentMobility;
    public float currentSpeed;
    public float currentInterceptionRange;
    public float currentInterceptionChance;

    [Header("Effets visuels et sonores")]
    public AudioClip hitSound;
    // Voix jouée lors d'un coup dévastateur
    public AudioClip criticalHitSound;
    // Voix jouée lorsque l'unité est interceptée
    public AudioClip interceptedSound;
    // Voix jouée lorsque l'unité intercepte une autre unité
    public AudioClip interceptionSound;
    [Tooltip("Joué si la cible meurt avant la fin d'une attaque")] public AudioClip prematureDeathTaunt;
    public GameObject hitEffect;
    public GameObject deathEffect;

    [Header("Animations spéciales")]
    // Animation générique de dégâts
    public AnimationClip hitAnimation;                // Jouée quand l'unité subit des dégâts

    public AnimationClip deathAnimation;

    // Animations liées à la téléportation
    public AnimationClip TPAnimation_Start;           // Jouée avant la téléportation
    public AnimationClip TPAnimation_Destination;     // Jouée après la téléportation
    public AnimationClip prepareToUndergoAnimation;   // Prépare la cible à subir un coup

    [Header("Sons de déplacement")]
    public AudioClip moveStartClip;
    public AudioClip moveEndClip;

    [Header("Voix de deuil")]
    [Tooltip("Lamentation jouée par ce personnage quand Lucian meurt")]
    public AudioClip weepForLucianDeath;
    [Tooltip("Lamentation jouée par ce personnage quand Thalia meurt")]
    public AudioClip weepForThaliaDeath;
    [Tooltip("Lamentation jouée par ce personnage quand Kael meurt")]
    public AudioClip weepForKaelDeath;
    [Tooltip("Lamentation jouée par ce personnage quand Link meurt")]
    public AudioClip weepForLinkDeath;
    [Tooltip("Lamentation jouée par ce personnage quand Luna meurt")]
    public AudioClip weepForLunaDeath;

    [Header("Awake Mechanics")]
    [Tooltip("Nombre d'harmoniques requis pour entrer en Awake")] public int resonancePoint = 1;
    [Tooltip("Seuil minimal d'harmoniques avant de sortir d'Awake")] public int dissonancePoint = 0;

    // Ajoute une référence au GameObject source
    public MonoBehaviour owner;

    private void OnEnable()
    {
        // Assure que, quand on clone, on part des bonnes valeurs de base
        currentInitiative = baseInitiative;
        currentHP = baseHP + baseVitality;
        currentStrength = baseStrength;
        currentDefense = baseDefense;
        currentRage = baseRage;
        currentPower = basePower;
        currentStability = baseStability;
        currentVitality = baseVitality;
        currentSagacity = baseSagacity;
        currentReflex = baseReflex;
        currentMobility = baseMobility;
        currentRange = baseRange;
        currentFatigue = baseFatigue;
    }

    public Transform GetTransform()
    {
        return owner != null ? owner.transform : null;
    }
}

public enum CharacterType { SquadUnit, EnemyUnit }
public enum GameplayType { Rage, Fatigue, Concentration, Sacrifice }

