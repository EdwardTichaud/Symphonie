using System;
using UnityEngine;
using System.Collections.Generic; // Permet l'utilisation de listes génériques
using UnityEngine.Timeline; // Référence aux timelines d'introduction
using UnityEngine.Serialization; // Facilite la migration des anciens champs (par exemple resonancePoint).

/// <summary>
/// ScriptableObject décrivant complètement un personnage jouable ou ennemi.
/// L'objectif de cette classe est d'offrir des catégories clairement identifiées
/// pour faciliter la maintenance et l'extension du contenu.
/// </summary>
[CreateAssetMenu(fileName = "CharacterData", menuName = "Symphonie/CharacterData")]
public class CharacterData : ScriptableObject, ITargetable
{
    #region Présentation & Classification

    [Header("Identité visuelle")]
    [Tooltip("Nom complet affiché dans l'interface et les dialogues.")]
    public string characterName;

    [Tooltip("Portrait principal utilisé dans les menus et fiches de personnage.")]
    public Sprite portrait;

    [Tooltip("Liste de portraits alternatifs pour l'écran Versus et les points d'apparition.")]
    public List<Sprite> versusSprites = new();

    [Header("Catégories de gameplay")]
    [Tooltip("Type narratif du personnage, utilisé pour filtrer les interactions scénaristiques.")]
    public CharacterType characterType;

    [Tooltip("Style de jeu principal du personnage (ex : Rage, Harmonie...).")]
    public GameplayType gameplayType = GameplayType.Rage;

    [Tooltip("Affinité harmonique majeure qui influence les synergies de l'équipe.")]
    public HarmonicType harmonicType = HarmonicType.Lumiere;

    [Header("Représentation 3D")]
    [Tooltip("Prefab affiché dans les environnements explorables.")]
    public GameObject characterWorldModel;

    [Tooltip("Prefab utilisé pendant les combats.")]
    public GameObject characterBattleModel;

    [Header("Positionnement dans les zones")]
    [Tooltip("Indice du slot de départ dans un champ de bataille scénarisé.")]
    public int battlefieldIndex = 0;

    #endregion

    #region Mobilité & Comportement

    [Header("Mobilité")]
    [Tooltip("Vrai si l'unité évolue dans les airs et n'est pas affectée par la gravité.")]
    public bool isAirUnit = false;

    [Tooltip("Hauteur supplémentaire appliquée lors du spawn pour les unités aériennes.")]
    public float distanceFromGround = 0f;

    [Header("Réactions en combat")]
    [Tooltip("Si vrai, ce personnage peut être intercepté lors de ses actions.")]
    public bool interceptable = true;

    [Tooltip("Si vrai, ses attaques sont évitables par les adversaires.")]
    public bool avoidable = true;

    #endregion

    #region Statistiques de base

    [Header("Attributs fondamentaux")]
    [Tooltip("Mesure de la réactivité aux événements (esquives, parades, etc.).")]
    public float baseReflex;

    [Tooltip("Capacité à se déplacer rapidement sur le terrain.")]
    public float baseMobility;

    [Tooltip("Résilience globale qui augmente les points de vie effectifs.")]
    public float baseVitality;

    [Tooltip("Puissance harmonique servant de base aux calculs magiques.")]
    public float basePower;

    [Tooltip("Stabilité émotionnelle réduisant les risques de désaccords harmoniques.")]
    public float baseStability;

    [Tooltip("Perspicacité permettant d'exploiter les faiblesses ennemies.")]
    public float baseSagacity;

    [Header("Paramètres communs")]
    [Tooltip("Initiative de départ utilisée dans la timeline de combat.")]
    public float baseInitiative;

    [Tooltip("Portée moyenne des attaques physiques.")]
    public float baseRange;

    [Tooltip("Points de vie de base hors bonus narratifs.")]
    public float baseHP;

    [Tooltip("Attaque physique moyenne.")]
    public float baseStrength;

    [Tooltip("Défense physique moyenne.")]
    public float baseDefense;

    [Tooltip("Vitesse de déplacement et d'exécution de base.")]
    public float baseSpeed;

    [Tooltip("Portée d'interception utilisée pour stopper une action adverse.")]
    public float baseInterceptionRange;

    [Header("Paramètres spécifiques - Lucian")]
    [Tooltip("Rage initiale de Lucian au début d'un affrontement.")]
    public float baseRage;

    [Tooltip("Réserve maximale de rage que Lucian peut cumuler.")]
    public float maxRage;

    [Tooltip("Multiplicateur appliqué aux dégâts lorsque Lucian consomme sa rage.")]
    public float rageDamageMultiplier;

    [Header("Paramètres spécifiques - Thalia")]
    [Tooltip("Fatigue initiale de Thalia, utile pour calibrer la difficulté d'accès à ses combos.")]
    public float baseFatigue;

    [Tooltip("Limite de fatigue avant les malus narratifs ou mécaniques.")]
    public float maxFatigue;

    #endregion

    #region Configurations de combat

    [Header("Attaques musicales")]
    [Tooltip("Répertoire complet des attaques disponibles pour ce personnage.")]
    public MusicalMoveSO[] musicalAttacks;

    [Tooltip("Attaque musicale toujours disponible quel que soit l'équipement.")]
    public MusicalMoveSO specialMusicalMove;

    [Header("Jeux d'actions favoris")]
    [Tooltip("Configurations d'attaques musicales favorites pour accélérer la sélection en combat.")]
    public List<CharacterMusicalMoveSet> musicalMoveSets = new();

    [Tooltip("Index utilisé au chargement pour activer un set musical spécifique. -1 pour aucun.")]
    public int defaultMusicalMoveSetIndex = -1;

    [Tooltip("Configurations d'items favorites pour mettre en avant les objets clefs en combat.")]
    public List<CharacterItemSet> itemSets = new();

    [Tooltip("Index utilisé au chargement pour activer un set d'objets spécifique. -1 pour aucun.")]
    public int defaultItemSetIndex = -1;

    #endregion

    #region Etats runtime

    [Header("Valeurs dynamiques")]
    [Tooltip("Initiative courante utilisée pour l'ordre de jeu.")]
    public float currentInitiative;

    [Tooltip("Portée instantanée tenant compte des buffs et malus.")]
    public float currentRange;

    [Tooltip("Points de vie courants.")]
    public float currentHP;

    [Tooltip("Force physique courante.")]
    public float currentStrength;

    [Tooltip("Défense physique courante.")]
    public float currentDefense;

    [Tooltip("Puissance harmonique courante.")]
    public float currentPower;

    [Tooltip("Stabilité émotionnelle courante.")]
    public float currentStability;

    [Tooltip("Vitalité courante.")]
    public float currentVitality;

    [Tooltip("Perspicacité courante.")]
    public float currentSagacity;

    [Tooltip("Indique si l'unité est contrôlée par le joueur à l'instant T.")]
    public bool isPlayerControlled;

    [Tooltip("Rage accumulée actuellement (spécifique à Lucian).")]
    public float currentRage;

    [Tooltip("Fatigue accumulée actuellement (spécifique à Thalia).")]
    public float currentFatigue;

    [Tooltip("Réflexes courants après application des buffs.")]
    public float currentReflex;

    [Tooltip("Mobilité courante après les modificateurs.")]
    public float currentMobility;

    [Tooltip("Vitesse courante tenant compte des effets en cours.")]
    public float currentSpeed;

    [Tooltip("Portée d'interception courante.")]
    public float currentInterceptionRange;

    [Tooltip("Chance de réussir une interception à l'instant T.")]
    public float currentInterceptionChance;

    [Tooltip("Réserve actuelle d'harmoniques du type signature pour aider le game design.")]
    public int currentHarmonicCharge;

    [HideInInspector]
    public int currentMusicalMoveSetIndex = -1;

    [HideInInspector]
    public int currentItemSetIndex = -1;

    #endregion

    #region Audio & Effets

    [Header("Impacts & réactions")]
    [Tooltip("Effet sonore principal joué lors d'un coup reçu.")]
    public AudioClipSO hitSound;

    [Tooltip("Cri joué lors d'un coup critique infligé.")]
    public AudioClipSO criticalHitSound;

    [Tooltip("Voix jouée lorsque l'unité est interceptée.")]
    public AudioClipSO interceptedSound;

    [Tooltip("Voix jouée lorsque l'unité intercepte une autre unité.")]
    public AudioClipSO interceptionSound;

    [Tooltip("Réplique jouée si la cible meurt avant la fin d'une attaque.")]
    public AudioClipSO prematureDeathTaunt;

    [Tooltip("Effet visuel déclenché lorsqu'un coup touche la cible.")]
    public GameObject hitEffect;

    [Tooltip("Effet visuel déclenché lors de la mort du personnage.")]
    public GameObject deathEffect;

    [Header("Déplacements rapides")]
    [Tooltip("Système de particules utilisé pendant les déplacements rapides de cette unité.")]
    public GameObject dashParticlesPrefab;

    [Tooltip("Effet sonore joué lorsque les particules de dash sont générées pour cette unité.")]
    public AudioClipSO dashSoundClip;

    [Tooltip("Effet visuel déclenché au début de l'éveil (Awake).")]
    public GameObject awakenEffect_Start;

    [Tooltip("Effet visuel maintenu durant l'éveil (Awake).")]
    public GameObject awakenEffect_Loop;

    [Tooltip("Effet visuel utilisé à la fin de l'éveil (Awake).")]
    public GameObject awakenEffect_End;

    [Tooltip("Effet visuel déclenché lorsque l'unité sombre en dissonance.")]
    public GameObject dissonanceEffect_Start;

    [Tooltip("Effet visuel maintenu tant que l'unité est dissonante.")]
    public GameObject dissonanceEffect_Loop;

    [Tooltip("Effet visuel joué lorsque l'unité se libère de la dissonance.")]
    public GameObject dissonanceEffect_End;

    [Header("Réglages Awake & Dissonance")]
    [Tooltip("Multiplicateur appliqué à toutes les statistiques courantes lorsque l'unité entre en état Awake.")]
    public float awakeStatMultiplier = 1.5f;

    #endregion

    #region Animations & Timeline

    [Header("Animations d'impact")]
    [Tooltip("Animation jouée quand l'unité subit des dégâts.")]
    public AnimationClip hitAnimation;

    [Tooltip("Animation de décès.")]
    public AnimationClip deathAnimation;

    [Header("Animations de téléportation")]
    [Tooltip("Animation jouée avant la téléportation.")]
    public AnimationClip TPAnimation_Start;

    [Tooltip("Animation jouée juste après la téléportation.")]
    public AnimationClip TPAnimation_Destination;

    [Tooltip("Animation utilisée pour préparer la cible à subir un coup.")]
    public AnimationClip prepareToUndergoAnimation;

    [Header("Animations d'états spéciaux")]
    [Tooltip("Animation Idle utilisée lorsque l'unité est en état Awake.")]
    public AnimationClip awakenIdleAnimation;

    [Tooltip("Animation Idle utilisée lorsque l'unité est dissonante.")]
    public AnimationClip dissonantIdleAnimation;

    [Tooltip("Animation d'entrée en Awake utilisée pour overrider l'Animator si nécessaire.")]
    public AnimationClip awakeEnterAnimation;

    [Tooltip("Animation de sortie de l'Awake utilisée pour overrider l'Animator si nécessaire.")]
    public AnimationClip awakeExitAnimation;

    [Tooltip("Animation d'entrée dans la dissonance.")]
    public AnimationClip dissonanceEnterAnimation;

    [Tooltip("Animation jouée lorsque la dissonance prend fin.")]
    public AnimationClip dissonanceExitAnimation;

    [Header("Timeline d'introduction")]
    [Tooltip("Timeline jouée lors de l'introduction du combat pour cette unité.")]
    public TimelineAsset introTimeline;

    #endregion

    #region Audio contextuel

    [Header("Sons de déplacement")]
    [Tooltip("Clip joué au début d'un mouvement.")]
    public AudioClipSO moveStartClip;

    [Tooltip("Clip joué à la fin d'un mouvement.")]
    public AudioClipSO moveEndClip;

    [Header("Interface & sélection")]
    [Tooltip("Clip joué lorsque cette unité devient active dans les menus de combat.")]
    public AudioClipSO menuSelectionClip;

    [Header("Transitions d'état")] // Clips joués lors des transitions entre les états majeurs de l'unité.
    [Tooltip("Clip joué lorsque l'unité entre automatiquement en éveil (Awake).")]
    public AudioClipSO awakeEnterClip;

    [Tooltip("Clip joué lorsque l'unité quitte l'éveil (Awake).")]
    public AudioClipSO awakeExitClip;

    [Tooltip("Clip joué lorsque l'unité devient dissonante.")]
    public AudioClipSO dissonanceEnterClip;

    [Tooltip("Clip joué lorsque l'unité sort de la dissonance.")]
    public AudioClipSO dissonanceExitClip;

    [Tooltip("Clip joué lorsqu'une animation d'Idle démarre (tous états confondus).")]
    public AudioClipSO idleEnterClip;

    [Tooltip("Clip joué lorsqu'une animation d'Idle est interrompue.")]
    public AudioClipSO idleExitClip;

    [Header("Voix de deuil")]
    [Tooltip("Lamentation jouée par ce personnage quand Lucian meurt.")]
    public AudioClipSO weepForLucianDeath;

    [Tooltip("Lamentation jouée par ce personnage quand Thalia meurt.")]
    public AudioClipSO weepForThaliaDeath;

    [Tooltip("Lamentation jouée par ce personnage quand Kael meurt.")]
    public AudioClipSO weepForKaelDeath;

    [Tooltip("Lamentation jouée par ce personnage quand Link meurt.")]
    public AudioClipSO weepForLinkDeath;

    [Tooltip("Lamentation jouée par ce personnage quand Luna meurt.")]
    public AudioClipSO weepForLunaDeath;

    #endregion

    #region Gestion harmonique & métadonnées

    [Header("Gestion des harmoniques")]
    [FormerlySerializedAs("resonancePoint"), Tooltip("Quantité d'harmoniques du type signature à accumuler pour pouvoir déclencher l'état Awake.")]
    public int awakeHarmonicThreshold = 1;

    [Tooltip("Seuil minimal d'harmoniques avant de sortir d'Awake.")]
    public int dissonancePoint = 0;

    [Tooltip("Réserve d'harmoniques disponible au début d'un combat avant toute génération supplémentaire.")]
    public int baseHarmonicCharge = 1;

    [Header("Référence runtime")]
    [Tooltip("Composant possédant cette instance pour faciliter les recherches à chaud.")]
    public MonoBehaviour owner;

    #endregion

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

