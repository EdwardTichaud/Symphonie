using System;
using UnityEngine;
using System.Collections.Generic; // Permet l'utilisation de listes génériques
using UnityEngine.Timeline; // Référence aux timelines d'introduction
using UnityEngine.Serialization; // Facilite la migration des anciens champs (par exemple resonancePoint).

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

    [Header("Déplacement")]
    [Tooltip("Vrai si l'unité évolue dans les airs et n'est pas affectée par la gravité.")]
    public bool isAirUnit = false;

    [Header("Comportement en combat")]
    [Tooltip("Si vrai, ce personnage peut être intercepté lors de ses actions")]
    public bool interceptable = true;
    [Tooltip("Si vrai, ses attaques sont inévitables et ne peuvent être esquivées")]
    public bool avoidable = true;

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

    [Header("Sets personnalisés")]
    [Tooltip("Configurations d'attaques musicales favorites pour accélérer la sélection en combat.")]
    public List<CharacterMusicalMoveSet> musicalMoveSets = new();

    [Tooltip("Index utilisé au chargement pour activer un set musical spécifique. -1 pour aucun.")]
    public int defaultMusicalMoveSetIndex = -1;

    [Tooltip("Configurations d'items favorites pour mettre en avant les objets clefs en combat.")]
    public List<CharacterItemSet> itemSets = new();

    [Tooltip("Index utilisé au chargement pour activer un set d'objets spécifique. -1 pour aucun.")]
    public int defaultItemSetIndex = -1;

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
    [Tooltip("Trace la réserve actuelle d'harmoniques du type signature pour aider le game design.")]
    public int currentHarmonicCharge;

    [HideInInspector] public int currentMusicalMoveSetIndex = -1;
    [HideInInspector] public int currentItemSetIndex = -1;

    [Header("Effets visuels et sonores")]
    public AudioClipSO hitSound;
    // Voix jouée lors d'un coup dévastateur
    public AudioClipSO criticalHitSound;
    // Voix jouée lorsque l'unité est interceptée
    public AudioClipSO interceptedSound;
    // Voix jouée lorsque l'unité intercepte une autre unité
    public AudioClipSO interceptionSound;
    [Tooltip("Joué si la cible meurt avant la fin d'une attaque")] public AudioClipSO prematureDeathTaunt;
    public GameObject hitEffect;
    public GameObject deathEffect;

    [Header("Effets de déplacement")]
    [Tooltip("Système de particules utilisé pendant les déplacements rapides de cette unité.")]
    public GameObject dashParticlesPrefab;
    [Tooltip("Effet sonore joué lorsque les particules de dash sont générées pour cette unité.")]
    public AudioClipSO dashSoundClip;

    public GameObject awakenEffect;

    [Header("Animations spéciales")]
    // Animation générique de dégâts
    public AnimationClip hitAnimation;                // Jouée quand l'unité subit des dégâts

    public AnimationClip deathAnimation;

    // Animations liées à la téléportation
    public AnimationClip TPAnimation_Start;           // Jouée avant la téléportation
    public AnimationClip TPAnimation_Destination;     // Jouée après la téléportation
    public AnimationClip prepareToUndergoAnimation;   // Prépare la cible à subir un coup

    [Header("Timeline d'introduction")]
    [Tooltip("Timeline jouée lors de l'introduction du combat pour cette unité.")]
    public TimelineAsset introTimeline;

    [Header("Sons de déplacement")]
    public AudioClipSO moveStartClip;
    public AudioClipSO moveEndClip;

    [Header("Effets sonores d'interface")]
    [Tooltip("Clip joué lorsque cette unité devient active dans les menus de combat.")]
    public AudioClipSO menuSelectionClip;

    [Header("Voix de deuil")]
    [Tooltip("Lamentation jouée par ce personnage quand Lucian meurt")]
    public AudioClipSO weepForLucianDeath;
    [Tooltip("Lamentation jouée par ce personnage quand Thalia meurt")]
    public AudioClipSO weepForThaliaDeath;
    [Tooltip("Lamentation jouée par ce personnage quand Kael meurt")]
    public AudioClipSO weepForKaelDeath;
    [Tooltip("Lamentation jouée par ce personnage quand Link meurt")]
    public AudioClipSO weepForLinkDeath;
    [Tooltip("Lamentation jouée par ce personnage quand Luna meurt")]
    public AudioClipSO weepForLunaDeath;

    [Header("Gestion des Harmoniques")]
    [FormerlySerializedAs("resonancePoint"), Tooltip("Quantité d'harmoniques du type signature à accumuler pour pouvoir déclencher l'état Awake.")]
    public int awakeHarmonicThreshold = 1;
    [Tooltip("Seuil minimal d'harmoniques avant de sortir d'Awake")] public int dissonancePoint = 0;
    [Tooltip("Réserve d'harmoniques disponible au début d'un combat avant toute génération supplémentaire.")]
    public int baseHarmonicCharge = 1;

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
        currentHarmonicCharge = baseHarmonicCharge;

        // Assure que les sets actifs sont correctement initialisés pour les combats.
        currentMusicalMoveSetIndex = NormalizeSetIndex(musicalMoveSets, defaultMusicalMoveSetIndex);
        currentItemSetIndex = NormalizeSetIndex(itemSets, defaultItemSetIndex);
    }

    public Transform GetTransform()
    {
        return owner != null ? owner.transform : null;
    }

    /// <summary>
    /// Retourne la configuration d'attaques musicales actuellement active pour ce personnage.
    /// </summary>
    public CharacterMusicalMoveSet GetActiveMusicalMoveSet()
    {
        if (musicalMoveSets == null || musicalMoveSets.Count == 0)
            return null; // Aucun set défini : on conserve l'ordre par défaut.

        if (currentMusicalMoveSetIndex < 0 || currentMusicalMoveSetIndex >= musicalMoveSets.Count)
            currentMusicalMoveSetIndex = NormalizeSetIndex(musicalMoveSets, 0);

        return currentMusicalMoveSetIndex >= 0 ? musicalMoveSets[currentMusicalMoveSetIndex] : null;
    }

    /// <summary>
    /// Retourne la configuration d'items actuellement active pour ce personnage.
    /// </summary>
    public CharacterItemSet GetActiveItemSet()
    {
        if (itemSets == null || itemSets.Count == 0)
            return null;

        if (currentItemSetIndex < 0 || currentItemSetIndex >= itemSets.Count)
            currentItemSetIndex = NormalizeSetIndex(itemSets, 0);

        return currentItemSetIndex >= 0 ? itemSets[currentItemSetIndex] : null;
    }

    /// <summary>
    /// Met à jour le set d'attaques musicales actif en se basant sur son nom.
    /// </summary>
    public void SetActiveMusicalMoveSet(string setName)
    {
        if (musicalMoveSets == null || musicalMoveSets.Count == 0)
        {
            currentMusicalMoveSetIndex = -1;
            return;
        }

        for (int i = 0; i < musicalMoveSets.Count; i++)
        {
            if (musicalMoveSets[i] == null)
                continue;

            if (string.Equals(musicalMoveSets[i].setName, setName, StringComparison.OrdinalIgnoreCase))
            {
                currentMusicalMoveSetIndex = i;
                return;
            }
        }

        // Aucun set ne correspond au nom renseigné : on se replie sur le premier.
        currentMusicalMoveSetIndex = NormalizeSetIndex(musicalMoveSets, 0);
    }

    /// <summary>
    /// Met à jour le set d'items actif en se basant sur son nom.
    /// </summary>
    public void SetActiveItemSet(string setName)
    {
        if (itemSets == null || itemSets.Count == 0)
        {
            currentItemSetIndex = -1;
            return;
        }

        for (int i = 0; i < itemSets.Count; i++)
        {
            if (itemSets[i] == null)
                continue;

            if (string.Equals(itemSets[i].setName, setName, StringComparison.OrdinalIgnoreCase))
            {
                currentItemSetIndex = i;
                return;
            }
        }

        currentItemSetIndex = NormalizeSetIndex(itemSets, 0);
    }

    /// <summary>
    /// Applique l'ordre défini dans le set actif aux attaques musicales proposées.
    /// </summary>
    public List<MusicalMoveSO> ApplyMusicalSetOrdering(IList<MusicalMoveSO> availableMoves)
    {
        var result = new List<MusicalMoveSO>();
        if (availableMoves == null)
            return result;

        var activeSet = GetActiveMusicalMoveSet();

        if (activeSet != null && activeSet.prioritizedMoves != null)
        {
            foreach (var prioritized in activeSet.prioritizedMoves)
            {
                if (prioritized == null)
                    continue; // Sécurité : ignore les références manquantes.

                for (int i = 0; i < availableMoves.Count; i++)
                {
                    var candidate = availableMoves[i];
                    if (candidate == null || candidate != prioritized)
                        continue;

                    if (!result.Contains(candidate))
                        result.Add(candidate); // Place le move favori en tête de liste.
                    break;
                }
            }
        }

        foreach (var move in availableMoves)
        {
            if (move == null)
                continue;

            if (!result.Contains(move))
                result.Add(move); // Conserve les autres attaques dans leur ordre original.
        }

        return result;
    }

    /// <summary>
    /// Applique l'ordre défini dans le set actif aux items proposés.
    /// </summary>
    public List<ItemData> ApplyItemSetOrdering(IList<ItemData> availableItems)
    {
        var result = new List<ItemData>();
        if (availableItems == null)
            return result;

        var activeSet = GetActiveItemSet();

        if (activeSet != null && activeSet.prioritizedItems != null)
        {
            foreach (var prioritized in activeSet.prioritizedItems)
            {
                if (prioritized == null)
                    continue;

                for (int i = 0; i < availableItems.Count; i++)
                {
                    var candidate = availableItems[i];
                    if (candidate == null || candidate != prioritized)
                        continue;

                    if (!result.Contains(candidate))
                        result.Add(candidate);
                    break;
                }
            }
        }

        foreach (var item in availableItems)
        {
            if (item == null)
                continue;

            if (!result.Contains(item))
                result.Add(item);
        }

        return result;
    }

    private static int NormalizeSetIndex<T>(IList<T> sets, int desiredIndex)
    {
        if (sets == null || sets.Count == 0)
            return -1;

        if (desiredIndex < 0 || desiredIndex >= sets.Count)
            return sets.Count > 0 ? 0 : -1;

        return desiredIndex;
    }
}

public enum CharacterType { SquadUnit, EnemyUnit }
public enum GameplayType { Rage, Fatigue, Concentration, Sacrifice }

