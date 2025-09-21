using UnityEngine;
using UnityEngine.Timeline;
using System.Collections.Generic;
// Les directives UnityEditor sont réservées à l'éditeur et ne doivent pas
// être incluses dans le build du joueur. Elles ont été retirées pour éviter
// les erreurs de compilation lors de l'export.
#if UNITY_EDITOR
using UnityEditor; // Utilisé uniquement pour charger les assets de référence.
#endif

[CreateAssetMenu(fileName = "NewMusicalMove", menuName = "Symphonie/Musical Move")]
public class MusicalMoveSO : ScriptableObject
{
    [Header("Identité et Catégorie")]
    [Tooltip("Nom de l'action affiché dans les menus et interfaces.")]
    public string moveName;
    [Tooltip("Catégorie générale permettant de regrouper les moves pour les joueurs novices comme expérimentés.")]
    public MoveType moveType = MoveType.Attack;
    [Tooltip("Vrai si l'attaque n'est disponible qu'en état Awake")]
    public bool onlyAwake = false;
    [Tooltip("Icône utilisée dans les interfaces pour représenter le move.")]
    public Sprite moveIcon;
    [TextArea, Tooltip("Description textuelle détaillant l'effet et l'utilisation du move.")]
    public string description;
    // L'animation de ciblage est désormais pilotée par la Timeline de préparation
    [Tooltip("Si vrai, le lanceur garde constamment la cible en ligne de mire lors de la préparation.")]
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
    [Tooltip("Suite des notes à jouer pour réussir le QTE du move.")]
    public List<NoteData> notes = new();

    [Header("Ressources")]
    [Tooltip("Coût en fatigue pour exécuter ce move.")]
    public float fatigueCost = 1f;
    [Tooltip("Coût en harmonie pour exécuter ce move.")]
    public int harmonicCost = 1;
    [Tooltip("Gain d'harmonie généré lors de l'utilisation du move.")]
    public int harmonicGeneration = 0;

    [Header("Puissance et Effet")]
    [Tooltip("Puissance de base de l'action, utilisée dans les calculs de dégâts ou de soin.")]
    public float power = 0;
    [Tooltip("Effet principal appliqué à la cible lors de l'utilisation du move.")]
    public MusicalEffectType effectType = MusicalEffectType.Damage;
    [Tooltip("Valeur numérique associée à l'effet principal.")]
    public int effectValue = 10;

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

    [Header("Conditions de ciblage")]
    [Tooltip("Détermine si le move est utilisable lorsque la cible est au sol, en l'air ou dans les deux cas")]
    public AltitudeCondition altitudeCondition = AltitudeCondition.GroundOrAir; // Par défaut: aucune restriction
    [Tooltip("Type de cible actuellement sélectionné pour ce move")]
    public TargetType targetType = TargetType.SingleEnemy;
    [Tooltip("Type de cible attribué par défaut lors de l'initialisation")]
    public TargetType defaultTargetType = TargetType.SingleEnemy;
    [Tooltip("Liste de tous les ciblages possibles pour ce move")]
    public List<TargetType> targetTypes = new List<TargetType>() { TargetType.SingleEnemy };

    [Header("Déplacement")]
    // La gestion des effets visuels est désormais confiée à la timeline,
    // seul le déplacement reste paramétrable dans ce ScriptableObject.
    [Tooltip("Vitesse de déplacement du lanceur lors de l'exécution du move")]
    public float moveSpeed = 20f;
    [Tooltip("Distance maximale de lancement lorsque le move le nécessite")]
    public float castDistance;
    [Tooltip("Si vrai, le lanceur doit se déplacer ou se téléporter pour exécuter ce move")]
    public bool requiresMovement = true;
    // Temps entre la disparition et la réapparition lors d'une téléportation
    // Permet de laisser les effets visuels/sonores se jouer avant le déplacement
    [Tooltip("Délai en secondes entre la disparition et la réapparition (0 = instantané)")]
    public float teleportDelay = 0.2f;
    [Tooltip("Si vrai, le lanceur reste à la position cible en fin de move")]
    public bool stayInPlace = false;
    [Tooltip("Si faux, le move ne peut pas être intercepté")]
    public bool interceptable = true;
    [Header("Placement autour de la cible")]
    [Tooltip("Position relative prise par le lanceur autour de la cible lors de l'exécution")]
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
    // ⏱️ Délai ajouté avant toute timeline de préparation.
    //    Permet d'insérer un temps mort contrôlé (par exemple pour une
    //    annonce ou un effet visuel) avant que le move ne commence
    //    réellement.
    [Tooltip("Temps en secondes à attendre AVANT de lancer la timeline de préparation du move")]
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

    [Header("Caméras par phase")]
    [Tooltip("Plan cinématique utilisé durant la préparation (None = conserver la caméra courante, OverShoulderCasterLookTarget = ancre Camera_Shoulder_Stay).")]
    public BattleCameraRole preparingCameraRole = BattleCameraRole.WideEstablish;
    [Tooltip("Plan cinématique utilisé durant l'exécution (None = conserver la caméra précédente).")]
    public BattleCameraRole performingCameraRole = BattleCameraRole.OverShoulderCasterToTarget;
    [Tooltip("Plan cinématique utilisé durant le repli (None = conserver la caméra précédente).")]
    public BattleCameraRole retreatCameraRole = BattleCameraRole.ClosePushCaster;

#if UNITY_EDITOR
    // ------------------------------------------------------------------
    // Références par défaut
    // ------------------------------------------------------------------
    // Lorsque l'on crée un nouveau MusicalMove, les trois champs de caméra
    // doivent automatiquement reprendre les valeurs de l'exemple
    // "MusicalMove_Rhapsodie" afin d'assurer une cohérence visuelle dans
    // tout le projet. Cette méthode est uniquement exécutée dans l'éditeur
    // afin d'éviter toute dépendance aux fichiers lors du build final.
    private const string RHAPSODIE_PATH =
        "Assets/MusicalMoves/MusicalMove_Rhapsodie/MusicalMove_Rhapsodie.asset";

    private void OnValidate()
    {
        // Charge le ScriptableObject de référence. Si le fichier est déplacé
        // ou supprimé, aucune action n'est effectuée pour ne pas provoquer
        // d'erreur dans l'éditeur.
        var reference = AssetDatabase.LoadAssetAtPath<MusicalMoveSO>(RHAPSODIE_PATH);
        if (reference == null)
            return;

        // Pour chaque champ, si aucune valeur n'est renseignée, on copie
        // celle de "MusicalMove_Rhapsodie". Les concepteurs peuvent ensuite
        // remplacer ces valeurs manuellement selon les besoins spécifiques
        // du nouveau move.
        if (preparingCameraRole == BattleCameraRole.None)
            preparingCameraRole = reference.preparingCameraRole;
        if (performingCameraRole == BattleCameraRole.None)
            performingCameraRole = reference.performingCameraRole;
        if (retreatCameraRole == BattleCameraRole.None)
            retreatCameraRole = reference.retreatCameraRole;
    }
#endif

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
        // Les effets visuels comme la création ou la suppression de sol sont
        // désormais entièrement gérés par la timeline, aucune instanciation
        // de prefab n'est nécessaire ici.
        if (caster != null && caster.Data.gameplayType == GameplayType.Fatigue)
        {
            caster.GetComponent<FatigueSystem>()?.OnActionPerformed(fatigueToApply);
        }
    }
}

/// <summary>
/// Catégorie générale permettant de classer les <see cref="MusicalMoveSO"/>.
/// Utile pour organiser les actions et guider le joueur dans ses choix.
/// </summary>
public enum MoveType
{
    Empty,      // Move factice utilisé comme espace réservé.
    Attack,     // Mouvement offensif infligeant des dégâts directs.
    Buff,       // Action accordant un effet bénéfique à un allié.
    Debuff,     // Action affaiblissant un adversaire.
    Alteration  // Modifie le terrain ou l'état d'une cible sans être purement offensif ou défensif.
}

public enum MusicalEffectType { Damage, Heal, Buff, Debuff, Sleep, WakeUpAll, LoyaltyMark, LinkMark }

public enum RelativePosition { Front, Back, Left, Right , NC}

/// <summary>
/// Définit dans quel contexte d'altitude un <see cref="MusicalMoveSO"/> est réalisable.
/// </summary>
public enum AltitudeCondition { GroundOnly, AirOnly, GroundOrAir }
