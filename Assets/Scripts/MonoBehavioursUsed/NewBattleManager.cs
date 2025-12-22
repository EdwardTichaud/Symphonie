using UnityEngine;
using System;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.Cinemachine;
using UnityEngine.EventSystems;

#region BattleState
public enum BattleState
{
    None,
    Initialization,
    BattleIntro,
    NewTurn,
    EndTurn,

    // SquadUnit Turn
    SquadUnit_MainMenu,
    SquadUnit_SkillsMenu,
    SquadUnit_ItemsMenu,
    SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad,
    SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies,
    SquadUnit_TargetSelectionAmongSquadForSkill,
    SquadUnit_TargetSelectionAmongSquadForItem,
    SquadUnit_TargetSelectionAmongEnemiesForSkill,
    SquadUnit_TargetSelectionAmongEnemiesForItem,
    SquadUnit_TargetSelectionForBaseAttack,
    SquadUnit_PerformingMusicalMove,
    SquadUnit_PerformingBaseAttack,
    SquadUnit_Item_Prepare,
    SquadUnit_Item_Use,

    // EnemyUnit Turn
    EnemyUnit_Reflexion,
    EnemyUnit_PerformingMusicalMove,
    EnemyUnit_Item_Prepare,
    EnemyUnit_Item_Use,

    // Game Over
    VictoryScreen_Await,
    VictoryScreen_CanContinue,

    GameOverScreen_Await,
    GameOverScreen_CanContinue,
}
#endregion

/// <summary>
/// Représente l'issue d'un combat afin de déterminer la timeline post-combat à jouer.
/// </summary>
public enum BattleOutcome
{
    None,
    Victory,
    Defeat
}

// Le BattleManager est découpé en partial classes pour isoler les responsabilités
// (spawn, tours, etc.) et faciliter les futures évolutions sans surcharger un seul fichier.
public partial class NewBattleManager : MonoBehaviour
{
    public static NewBattleManager Instance { get; private set; }

    [Header("État du combat")]
    public BattleState currentBattleState;

    [Header("Apparition des SquadUnits")]
    public GameObject squadUnitRay;
    private List<Transform> playerSpawnPoints = new List<Transform>();
    [Tooltip("Assigné dans la scène pour éviter les recherches au runtime.")]
    [SerializeField] private Transform playerSpawnRoot;

    [Header("Apparition des ennemis")]
    public GameObject enemyUnitRay;
    private List<Transform> enemySpawnPoints = new List<Transform>();
    [Tooltip("Assigné dans la scène pour éviter les recherches via tag si possible.")]
    [SerializeField] private Transform enemySpawnRoot;
    public List<CharacterData> enemyTemplates = new List<CharacterData>();

    [Header("Points de visée pour les caméras contextuelles")]
    [Tooltip("Offset appliqué aux ancres CMVPoint_OverShoulder_CasterLookTarget générées pour chaque position de combat.")]
    [SerializeField] private Vector3 overShoulderCasterLookPointPosition_SquadUnit = new(-1f, 1.4f, -2f);
    [SerializeField] private Vector3 overShoulderCasterLookPointPosition_EnemyUnit = new(1f, 1.4f, 2f);
    private Vector3 overShoulderCasterLookOffset = new(0f, 0f, 0f);

    [Header("Listes des unités en combat en fonction de leur état")]
    public List<CharacterUnit> unitsInBattle = new(); // Toutes les unités du combat quelque soit leur état
    public List<CharacterUnit> activeCharacterUnits = new List<CharacterUnit>(); // Unités actives en combat (HP > 0)

    [Header("Début de combat")]
    [SerializeField] private GameObject firstStrikeEffect;

    [Tooltip("Durée maximale pendant laquelle l'introduction de combat bloque le démarrage des tours.")]
    [SerializeField] private float battleIntroLockDuration = 2f;

    /// <summary>
    /// Indique si la mise en scène d'introduction verrouille temporairement les menus.
    /// Ce drapeau est consulté par l'InputsManager pour ignorer toute interaction
    /// tant que les animations d'arrivée ne sont pas complètement terminées.
    /// </summary>
    private bool battleIntroMenusLocked = false;

    /// <summary>
    /// Expose l'état du verrou d'introduction afin que les autres systèmes puissent
    /// savoir s'il est pertinent d'accepter ou d'ignorer les entrées de menu.
    /// </summary>
    public bool AreMenusLockedByBattleIntro => battleIntroMenusLocked;

    // Paramètres du ralentissement appliqué lors de l'introduction.
    [Tooltip("Facteur de ralentissement au tout début du combat.")]
    public float equipSlowMotionScale = 1f;

    [Header("Fin de combat")]
    public GameObject victoryScreen;
    public GameObject gameOverScreen;
    public RenderTexture VictoryScreenImage;
    public RenderTexture GameOverScreenImage;

    [Header("Défaite")]
    [Tooltip("Si vrai, une defaite quitte directement le combat (respawn au checkpoint) sans ecran Game Over.")]
    public bool gameOverOnDefeat = false;

    [HideInInspector] public bool respawnAtCheckpointOnExit = false;
    public bool ShouldRespawnAtCheckpoint => respawnAtCheckpointOnExit;

    [Header("Timelines post-combat")]
    [Tooltip("Timeline jouée automatiquement après une victoire, si définie.")]
    public TimelineAsset victoryTimeline; // Définie via TimelineBattleConfigSO

    [Tooltip("Timeline jouée après une défaite lorsqu'il ne s'agit pas d'un Game Over.")]
    public TimelineAsset defeatTimeline; // Null = Game Over

    [HideInInspector] public BattleOutcome lastBattleOutcome = BattleOutcome.None; // Stocke l'issue du combat

    [Header("Récompenses")]
    public List<ItemData> rewardItems = new();
    public int rewardXP = 0;

    private float battleStartTime = 0f;
    private int currentTurnDamage = 0;
    private int maxTurnDamage = 0;
    private CharacterUnit mvpUnit;
    private Dictionary<CharacterUnit, int> totalDamageDealt = new();

    [Header("Timeline d'objets")]
    public TimelineAsset itemPreparingTimeline;
    private bool itemMenuTimelineActive = false;

    /// <summary>
    /// Empêche le lancement multiple de la séquence de victoire.
    /// Ce drapeau garantit que la capture, le switch caméra et l'affichage UI
    /// restent synchronisés même si <see cref="HandleEndOfBattle"/> est évalué
    /// plusieurs fois sur des frames consécutives.
    /// </summary>
    private bool victorySequenceInProgress = false;

    [Header("Références scène")]
    [Tooltip("Racine du rig BattleCamera pour éviter les recherches GameObject.Find.")]
    [SerializeField] private Transform battleCameraRig;

    /// <summary>Nom de la Cinemachine dédiée au menu principal et aux variations associées.</summary>
    private const string MainMenuCameraName = "CMV_MainMenu";
    /// <summary>Nom de la Cinemachine utilisée pour le menu des compétences.</summary>
    private const string SkillsMenuCameraName = "CMV_SkillsMenu";
    /// <summary>Nom explicite de la caméra de mise en scène pour l'écran de victoire.</summary>
    private const string VictoryCameraName = "CMV_Victory";
    /// <summary>Nom de la Cinemachine dédiée au menu des objets.</summary>
    private const string ItemsMenuCameraName = "CMV_ItemsMenu";
    /// <summary>Nom de la Cinemachine utilisée durant les phases de sélection de cible.</summary>
    private const string TargetSelectionCameraName = "CMV_OverShoulder_CasterLookTarget";
    /// <summary>Nom de la Cinemachine prioritaire lors du ciblage d'un objet.</summary>
    private const string ItemTargetSelectionCameraName = "CMV_OrbitAroundUnit";
    /// <summary>
    /// Style de transition privilégié pour les menus. Il est directement récupéré auprès du
    /// <see cref="BattleCameraManager"/> pour rester parfaitement aligné avec la configuration globale du
    /// <see cref="CinemachineBlendSwitcher"/> (smooth obligatoire).
    /// </summary>
    private CinemachineBlendDefinition.Styles MenuCameraBlendStyle =>
        BattleCameraManager.Instance ? BattleCameraManager.Instance.SmoothBlendStyle : CinemachineBlendSwitcher.ResolveSmoothBlendStyle();

    /// <summary>
    /// Durée par défaut des transitions de menu. Toutes les transitions du jeu étant désormais figées à
    /// 0,5 seconde, on utilise la propriété exposée par le gestionnaire caméra pour éviter toute divergence.
    /// </summary>
    private float MenuCameraBlendDuration =>
        BattleCameraManager.Instance ? BattleCameraManager.Instance.SmoothBlendDuration : CinemachineBlendSwitcher.GlobalSmoothBlendDurationSeconds;

    /// <summary>
    /// Durée appliquée aux caméras contextuelles (ciblage, actions). Identique à celle des menus afin de
    /// conserver une expérience fluide et prévisible pour le joueur.
    /// </summary>
    private float ContextCameraBlendDuration => MenuCameraBlendDuration;

    /// <summary>
    /// Style de transition privilégié pour les caméras contextuelles. Même valeur que pour les menus : un
    /// unique réglage smooth garantit la cohérence visuelle quel que soit le rôle de la caméra active.
    /// </summary>
    private CinemachineBlendDefinition.Styles ContextCameraBlendStyle => MenuCameraBlendStyle;
    /// <summary>Hash du state Animator "Item_Prepare" pour lancer rapidement l'animation correspondante.</summary>
    private static readonly int AnimatorStateItemPrepare = Animator.StringToHash("Item_Prepare");
    /// <summary>Hash du state Animator "Idle_Battle" afin de revenir proprement à la pose neutre.</summary>
    private static readonly int AnimatorStateIdleBattle = Animator.StringToHash("Idle_Battle");

    private CharacterUnit previousUnit; // Champ de classe, pas une variable locale
    [HideInInspector] public CharacterUnit currentCharacterUnit;
    private bool isTurnResolving = false;
    private bool interceptionSucceeded = false;
    // File d'attente de timelines à jouer entre deux tours
    /// <summary>
    /// File d'attente des timelines à jouer en fin de tour.
    /// Chaque entrée décrit l'asset à lancer et l'objet servant de référence pour les bindings.
    /// La caméra n'étant plus animée via timeline, aucun tag n'est désormais nécessaire.
    /// </summary>
    private readonly Queue<PendingTimeline> pendingTimelines = new();

    /// <summary>
    /// Structure stockant les informations nécessaires pour jouer une timeline.
    /// Seul le lanceur est requis : la piste caméra est ignorée.
    /// </summary>
    private struct PendingTimeline
    {
        public TimelineAsset asset;
        public CharacterUnit unit;
        public GameObject casterBinding;

        public PendingTimeline(TimelineAsset asset, CharacterUnit unit, GameObject casterBinding)
        {
            // Référence vers l'asset de timeline à exécuter
            this.asset = asset;
            // Unité propriétaire de la timeline (et donc du PlayableDirector utilisé)
            this.unit = unit;
            // GameObject jouant la piste "Caster" de la timeline
            this.casterBinding = casterBinding;
        }
    }

    private const float ATB_THRESHOLD = 100f;
    // Délai appliqué avant qu'un ennemi n'exécute réellement son attaque
    private const float ENEMY_MOVE_DELAY = 1f;

    [Header("Sprites des touches")]
    [SerializeField] private Sprite inputSprite1;
    [SerializeField] private Sprite inputSprite2;
    [SerializeField] private Sprite inputSprite3;
    [SerializeField] private Sprite inputSprite4;

    [Header("Gestion du curseur de cible")]
    public GameObject targetCursorPrefab;
    [Tooltip("Prefab affichant la fenêtre d'interception")] public GameObject interceptionSignalPrefab;
    [HideInInspector] public GameObject targetCursor;
    [HideInInspector] public List<GameObject> multiTargetCursors = new List<GameObject>();

    [Tooltip("Canvas monde piloté par la caméra de combat. L'indicateur y est instancié pour bénéficier des mêmes réglages de rendu.")]
    [SerializeField] private Transform battleCameraCanvasTransform;
    
    private List<CharacterUnit> filteredUnits = new();
    // Liste temporaire réutilisée pour éviter des allocations lors du ciblage multiple
    private readonly List<CharacterUnit> multiTargetUnits = new();
    /// <summary>
    /// Mémorise la dernière unité suivie par la Cinemachine de ciblage afin
    /// d'éviter les rafraîchissements inutiles à chaque frame.
    /// </summary>
    private CharacterUnit lastTargetCursorCameraTarget;
    /// <summary>
    /// Conserve l'ancre utilisée pour guider le regard de la Cinemachine sur
    /// la cible sélectionnée. Cette information est essentielle pour savoir si
    /// l'on doit redemander une mise à jour du rig caméra.
    /// </summary>
    private Transform lastTargetCursorAnchor;
    /// <summary>
    /// Indique si la Cinemachine dédiée au suivi du curseur est actuellement
    /// configurée. Cela permet de libérer proprement les overrides lorsque
    /// l'on quitte une phase de ciblage.
    /// </summary>
    private bool isTargetCursorCinemachineActive;
    private int currentTargetIndex = 0;
    private float navigationCooldown = 0.3f;
    private float lastNavTime = 0f;
    private CharacterUnit _currentTargetCharacter;
    public CharacterUnit currentTargetCharacter
    {
        get => _currentTargetCharacter;
        set
        {
            _currentTargetCharacter = value;
            // On met à jour immédiatement le comportement caméra
            UpdateCameraBehaviour(currentBattleState);

            // Pendant la phase de ciblage, on ne souhaite plus orienter le lanceur
            if (!IsTargetSelectionState(currentBattleState)
                && currentCharacterUnit != null && _currentTargetCharacter != null)
            {
                OrientUnitTowardTarget(currentCharacterUnit, _currentTargetCharacter);
            }

            // A chaque mise à jour du ciblage, on relance systématiquement l'animation de
            // préparation pour souligner visuellement que l'unité est sous la menace d'une
            // attaque imminente. Même si la cible reste identique, relancer l'animation
            // permet de redonner du dynamisme à la scène et d'améliorer la lisibilité pour
            // le joueur, notamment lorsque plusieurs actions consécutives visent la même cible.
            if (_currentTargetCharacter != null)
            {
                // 🎯 Pendant les phases de sélection on relance toujours l'animation
                // de préparation pour souligner la menace immédiate. En revanche,
                // lorsque la séquence du MusicalMove est en cours et que la
                // timeline de préparation du lanceur doit garder la priorité,
                // on s'abstient de relancer l'animation pour éviter un doublon
                // avant que RhythmQTEManager ne le fasse lui-même à la fin de
                // ladite timeline.
                bool isSelectionState = IsTargetSelectionState(currentBattleState);
                bool shouldDelayDueToMove = RhythmQTEManager.Instance != null
                    && RhythmQTEManager.Instance.ShouldDelayTargetPreparationAnimation;

                if (isSelectionState || !shouldDelayDueToMove)
                {
                    _currentTargetCharacter.PlayPrepareToUndergoAnimation();
                }
            }
        }
    }
    //-------------------------------------------------------------------------------------

    // Caméra
    [Header("Caméra de combat")]
    // On mémorise l'objet caméra pour éviter de le rechercher à chaque frame
    [SerializeField] private GameObject battleCamera;
    public float cameraSmoothSpeed = 5f;
    private BattleState lastCameraEvaluatedState = BattleState.None;

    [Header("Cinématique de début de tour joueur")]
    [SerializeField, Tooltip("Durée du travelling inspiré de l'introduction de combat (en secondes).")]
    private float firstTurnRailDuration = 2.75f;
    [SerializeField, Tooltip("Distance en mètres séparant le point de départ du rail et l'ancre finale du joueur.")]
    private float firstTurnRailDepartureDistance = 6.5f;
    [SerializeField, Tooltip("Décalage latéral appliqué au rail pour rappeler le léger travelling sur rail.")]
    private float firstTurnRailLateralOffset = 1.75f;
    [SerializeField, Tooltip("Rehaussement vertical appliqué au rail afin de dominer légèrement la scène.")]
    private float firstTurnRailHeightOffset = 1.35f;
    [SerializeField, Tooltip("Distance de freinage avant l'arrivée sur l'ancre finale (en mètres).")]
    private float firstTurnRailApproachOffset = 2.1f;
    [SerializeField, Tooltip("Hauteur du point focal regardé pendant le travelling (en mètres).")]
    private float firstTurnRailFocusHeight = 1.7f;
    [SerializeField, Tooltip("Influence de la position actuelle de la caméra lors du calcul du point de départ. 0 = ignore la position actuelle, 1 = conserve la position actuelle.")]
    [Range(0f, 1f)] private float firstTurnRailCurrentPositionBlend = 0.35f;
    [SerializeField, Tooltip("Courbe d'asservissement utilisée pour lisser l'accélération et la décélération du rail.")]
    private AnimationCurve firstTurnRailEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // États runtime du travelling d'introduction
    private bool hasPlayedFirstTurnCameraRail = false;
    private bool isFirstTurnRailActive = false;
    private float firstTurnRailTimer = 0f;
    private Vector3 firstTurnRailStartPos;
    private Vector3 firstTurnRailEndPos;
    private Vector3 firstTurnRailControlPointA;
    private Vector3 firstTurnRailControlPointB;
    private Quaternion firstTurnRailStartRot;
    private Quaternion firstTurnRailEndRot;
    private Vector3 firstTurnRailFocusStart;
    private Vector3 firstTurnRailFocusEnd;

    // Compétences et items disponibles pour l’unité qui joue
    // Garder en public
    [HideInInspector] public List<MusicalMoveSO> skillChoices = new List<MusicalMoveSO>();
    // Mouvement spécial actuel (affiché dans le 4e slot du SkillsMenu)
    [HideInInspector] public MusicalMoveSO specialMoveChoice;
    /// <summary>
    ///     Mouvement d'attaque basique affiché dans le premier slot du SkillsMenu.
    ///     Ce champ reste public (en hide) pour simplifier la consultation dans l'inspecteur
    ///     tout en garantissant un point d'accès unique pour l'UI et les inputs.
    /// </summary>
    [HideInInspector] public MusicalMoveSO basicAttackMoveChoice;
    [HideInInspector] public List<ItemData> itemChoices = new List<ItemData>();
    [HideInInspector] public MusicalMoveSO currentMove;
    [HideInInspector] public ItemData currentItem;
    [HideInInspector] public TargetType currentItemTargetType;
    public int currentMenuIndex;
    // Index de la page actuellement affichée dans le SkillsMenu.
    // Chaque page contient autant de compétences que de slots disponibles.
    [HideInInspector] public int currentSkillPageIndex = 0;
    // Sélection nulle pour remplir les emplacements vides
    public MusicalMoveSO emptyMove;
    [Header("Attaque basique")]
    [Tooltip("Move utilisé lorsque l'unité ne possède pas d'attaque basique explicite dans sa liste personnelle.")]
    [SerializeField] private MusicalMoveSO defaultBasicAttackMove;
    /// <summary>
    /// Indicateur runtime permettant de savoir si la compétence courante est l'attaque basique.
    /// Ce drapeau nous aide à appliquer les règles historiques (fin de tour immédiate, etc.).
    /// </summary>
    private bool currentMoveIsBasicAttack = false;

    // Menus personnalisés pour l’unité qui joue
    public GameObject currentMainMenuContainer;
    public List<Transform> currentMainMenuSlots;

    public GameObject currentSkillsMenuContainer;
    public List<Transform> currentSkillsMenuSlots;
    private readonly Dictionary<RectTransform, float> baseMenuSlotHeights = new();
    private readonly Dictionary<TextMeshProUGUI, float> baseMenuTextHeights = new();
    private const float MinMenuFontSize = 40f;
    private const float MenuSlotVerticalPadding = 30f;

    public GameObject currentItemsMenuContainer;
    public List<Transform> currentItemsMenuSlots;

    [Header("Effets sonores de navigation en combat")]
    [SerializeField, Tooltip("Clip joué lorsque le menu principal s'affiche ou lorsque l'on y retourne.")]
    private AudioClipSO mainMenuOpenClip;
    [SerializeField, Tooltip("Clip joué dès que le joueur ouvre le menu des compétences.")]
    private AudioClipSO skillsMenuOpenClip;
    [SerializeField, Tooltip("Clip joué lors de l'accès au menu des objets.")]
    private AudioClipSO itemsMenuOpenClip;
    [SerializeField, Tooltip("Clip déclenché lorsque l'on passe en mode sélection de cible (compétence ou objet).")]
    private AudioClipSO targetSelectionClip;
    [SerializeField, Tooltip("Clip joué à chaque changement de cible pendant la navigation.")]
    private AudioClipSO targetChangeClip;
    [SerializeField, Tooltip("Clip joué lorsqu'une nouvelle page de compétences est affichée.")]
    private AudioClipSO skillPageChangeClip;
    [SerializeField, Tooltip("Effet utilisé lorsque l'unité active ne possède pas de clip personnalisé pour signaler sa sélection.")]
    private AudioClipSO defaultCharacterSelectionClip;

    // -----------------------------------------------------------------------------------

    #region Awake/Start/Update()
    /// <summary>
    /// Initialise le singleton et persiste à travers les scènes.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 🗑️ Les expérimentations de tours lents ayant été supprimées, le BattleManager
        //     fonctionne désormais exclusivement avec la boucle classique.
        //     Nous profitons de l'Awake pour le rappeler explicitement aux futurs mainteneurs.
    }

    /// <summary>
    /// Instancie le curseur de cible au lancement de la scène de combat.
    /// </summary>
    private void Start()
    {
        EnsureTargetCursor();
        // Recherche initiale de la caméra de combat pour éviter de la chercher à chaque frame
        EnsureBattleCamera();
    }

    /// <summary>
    ///     Calcule la position d'apparition finale d'un personnage en fonction de son type.
    ///     Les unités aériennes reçoivent une translation verticale additionnelle afin
    ///     de rester en sustentation au-dessus du sol dès l'instanciation.
    /// </summary>
    /// <param name="data">Données du personnage à instancier.</param>
    /// <param name="spawnPoint">Point d'apparition défini dans la scène.</param>
    /// <returns>Position monde ajustée respectant la hauteur requise.</returns>
    private Vector3 ComputeSpawnPosition(CharacterData data, Transform spawnPoint)
    {
        // 🚨 Sécurise l'appel : si le point de spawn n'est pas disponible, on renvoie
        //     une position nulle afin d'éviter toute NullReference lors de l'instanciation.
        if (spawnPoint == null)
            return Vector3.zero;

        // Base : on part de la position exacte du point défini par le level design.
        //         Cette position correspond explicitement au pivot de PlayerPosition_X
        //         ou EnemyPosition_X. Il est indispensable de conserver cette
        //         référence plutôt que de se baser sur le centre géométrique
        //         d'un éventuel volume (MeshRenderer, Collider...) afin de
        //         garantir que les artistes puissent ajuster librement leurs
        //         pivots dans l'éditeur.
        var finalPosition = spawnPoint.position;

        // ✈️ Si l'unité est aérienne, on applique directement la distance prévue par le Game Design.
        if (data != null && data.isAirUnit)
        {
            // Clamp à zéro pour éviter les valeurs négatives involontaires tout en permettant
            // aux designers d'ajuster librement la hauteur des ennemis volants.
            var positiveOffset = Mathf.Max(0f, data.distanceFromGround);
            // 🧭 Utilise explicitement l'axe "Up" du point d'apparition afin de rester
            //     parfaitement aligné sur le pivot défini dans la scène. En pratique,
            //     cela signifie que l'offset sera toujours appliqué relativement au
            //     Transform PlayerPosition_X / EnemyPosition_X, même si ce dernier est
            //     incliné ou animé par rapport au monde. On évite ainsi les glissements
            //     visuels observés lorsque l'on additionnait directement sur l'axe Y
            //     global (qui correspondait au centre du monde et non au pivot local).
            finalPosition += spawnPoint.up * positiveOffset;
        }

        return finalPosition;
    }

    /// <summary>
    /// Gère les sélections de cible pendant le combat.
    /// </summary>
    private void Update()
    {
        HandleTargetCursor();
        HandleTargetNavigation();
    }
    #endregion

    #region Mise en scène de la scène de bataille
    /// <summary>
    /// Lance les timelines d'introduction pour chaque unité en appliquant
    /// un effet de ralenti global. Toutes les timelines démarrent en parallèle
    /// et la coroutine attend leur terminaison avant de poursuivre.
    /// </summary>
    private IEnumerator PlayIntroTimelinesWithSlowTime()
    {
        // Sauvegarde des paramètres temporels actuels pour les restaurer ensuite
        float initialTimeScale = Time.timeScale;
        float initialFixedDelta = Time.fixedDeltaTime;

        // 🕵️‍♂️ Identifie les unités disposant réellement d'une timeline d'introduction
        // pour éviter d'appliquer un délai inutile lorsqu'aucune scène n'est configurée.
        var unitsWithIntro = unitsInBattle
            .Where(u => u.Data.introTimeline != null)
            .ToList();

        // 🚀 Si aucune timeline n'est définie, on quitte immédiatement sans ralentir le jeu.
        if (unitsWithIntro.Count == 0)
            yield break;

        // Application du facteur de ralenti défini dans l'inspecteur uniquement
        // lorsque la mise en scène doit effectivement se jouer.
        Time.timeScale = equipSlowMotionScale;
        Time.fixedDeltaTime = initialFixedDelta * Time.timeScale;

        // Liste des PlayableDirector actifs pour surveiller la fin des timelines
        List<PlayableDirector> activeDirectors = new();

        // Déclenche les timelines d'introduction sur chaque unité concernée
        foreach (var unit in unitsWithIntro)
        {
            var timeline = unit.Data.introTimeline;

            // Demande à la CharacterUnit de préparer les bindings "Root" et "Model" de sa timeline d'intro.
            PlayableDirector director = unit.PrepareIntroTimeline(timeline);

            // Si la préparation échoue (timeline incomplète, aucun PlayableDirector, etc.), on ignore simplement cette unité.
            if (director == null)
                continue;

            // Lance la lecture de la timeline correctement configurée et l'ajoute à la liste de suivi.
            director.Play();
            activeDirectors.Add(director);
        }

        // ⏱️ Calcule la durée d'attente maximale en temps réel. L'utilisation de l'unscaled delta
        //     permet de respecter la durée configurée même si le timeScale a été réduit.
        float waitDuration = Mathf.Max(0f, battleIntroLockDuration);
        float elapsedIntroTime = 0f;

        // ⛔ On attend exactement la durée configurée, même si les timelines se terminent plus tôt.
        //    Ainsi, les mises en scène longues n'empêchent plus le combat de démarrer : passé ce délai
        //    les tours commencent, tandis que les introductions continuent leur lecture en arrière-plan.
        while (elapsedIntroTime < waitDuration)
        {
            yield return null;
            elapsedIntroTime += Time.unscaledDeltaTime;
        }

        // 🧷 Toutefois, si une timeline poursuit encore sa lecture une fois le délai minimal écoulé,
        //    on maintient le verrouillage des menus jusqu'à ce que toutes les mises en scène soient
        //    effectivement terminées. Sans cette sécurité supplémentaire, les joueurs pouvaient
        //    sélectionner des compétences ou des objets pendant les dernières secondes des animations.
        while (IsAnyIntroDirectorStillPlaying(activeDirectors))
        {
            yield return null;
        }

        // Restaure les valeurs temporelles initiales une fois le délai écoulé pour que le gameplay
        // retrouve immédiatement sa vitesse normale lorsque le premier tour débute.
        Time.timeScale = initialTimeScale;
        Time.fixedDeltaTime = initialFixedDelta;

        // 📌 Aucun yield supplémentaire n'est nécessaire : la boucle de tours peut démarrer dès
        //     maintenant, les PlayableDirector poursuivent leur animation sans bloquer le gameplay.

        // La gestion du changement de caméra est réalisée en dehors de cette
        // coroutine afin de laisser la main à l'appelant sur la transition.
    }

    /// <summary>
    /// Détermine si au moins une des timelines d'introduction suit encore sa lecture.
    /// </summary>
    /// <param name="directors">Liste des directeurs surveillés pendant l'introduction.</param>
    /// <returns><c>true</c> tant qu'une timeline est en cours, <c>false</c> lorsque toutes sont stoppées.</returns>
    private static bool IsAnyIntroDirectorStillPlaying(List<PlayableDirector> directors)
    {
        if (directors == null || directors.Count == 0)
            return false;

        // Parcours l'ensemble des directeurs actifs pour vérifier leur état de lecture.
        foreach (var director in directors)
        {
            // Un directeur peut avoir été détruit en cours d'introduction (changement de scène, skip, etc.).
            // On considère alors que cette timeline est naturellement terminée.
            if (director == null)
                continue;

            // Tant que le PlayableDirector reste en lecture, la cinématique est toujours en cours.
            if (director.state == PlayState.Playing)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gère la caméra d'introduction en priorité orbitale, attend la fin des
    /// déplacements puis lance les timelines d'introduction des unités.
    /// </summary>
    private IEnumerator PlayIntroCameraSequence()
    {
        // 🎬 Lance les timelines d'introduction en mode ralenti et attend leur terminaison.
        yield return PlayIntroTimelinesWithSlowTime();

        // 📷 Dès que les mises en scène sont terminées, on bascule vers la caméra dédiée au menu principal.
        const float introToMenuBlendDuration = 0.5f;
        BattleCameraManager.Instance?.SwitchToCamera(MainMenuCameraName, introToMenuBlendDuration);

        // 🎞️ On laisse le travelling d'intro se terminer proprement, puis on replace la caméra à son point d'origine.
        BattleCameraManager.Instance?.StopBattleIntroCameraTravel(introToMenuBlendDuration);
    }
    #endregion

    #region Démarrage du combat
    public IEnumerator StartBattle()
    {
        Debug.Log("[BattleTurnManager] Démarrage du combat");

        // 🎮 Au lancement du combat, on limite les entrées au seul bouton de validation.
        //     Les menus resteront donc parfaitement figés pendant les animations
        //     d'introduction des unités, conformément à la demande de game design.
        InputsManager.Instance?.RestrictInputsToBattleConfirm();

        // 🛡️ Assure que les filtres d'écran sont totalement invisibles au lancement du combat.
        //    Sans cette étape, un résidu de fondu noir ou blanc pourrait persister d'une timeline précédente.
        var fader = FadeChildrenOpacity.Instance;
        if (fader != null)
        {
            // Indice 0 = BlackScreen, indice 1 = WhiteScreen
            // La durée est fixée à 0 pour appliquer la transparence immédiatement.
            fader.EnsureTransparency(0, 0f);
            fader.EnsureTransparency(1, 0f);
        }

        GameManager.Instance?.ResetEnemiesDefeatedCount();

        battleStartTime = Time.time;
        currentTurnDamage = 0;
        maxTurnDamage = 0;
        mvpUnit = null;
        totalDamageDealt.Clear();

        //0 Liste "unitsInBattle" construite avec SpawnAll

        //1 Filtrer pour ne garder que les unités dont les HP sont > 0
        activeCharacterUnits = ReturnActiveUnits();

        // Réinitialise les compteurs d'utilisation des moves et items pour le combat
        foreach (var unit in activeCharacterUnits)
            unit.ResetBattleMoveUsage();
        InventoryManager.Instance?.ResetBattleItemUsage();

        foreach (var unit in activeCharacterUnits.Where(u => u.Data.isPlayerControlled))
        {
            if (!totalDamageDealt.ContainsKey(unit))
                totalDamageDealt[unit] = 0;
        }

        //2 Initialise l’UI de la timeline de combat via le gestionnaire dédié
        BattleTimelineUIManager.Instance?.Initialize(unitsInBattle);
        // Cache l'UI de timeline jusqu'au premier tour du joueur via le gestionnaire centralisé
        BattleTimelineUIManager.Instance?.SetVisible(false);

        //3 Affecter currentTarget au premier ennemi de la liste
        SetDefaultCurrentTarget();

        // S'assure que le curseur de cible existe pour ce nouveau combat
        EnsureTargetCursor();

        //4 Réinitialise les ATB
        ResetAllATB();

        // ⚡ Identifie au plus tôt l'unité qui ouvrira le bal afin d'orienter le menu sur le bon personnage.
        CharacterUnit upcomingFirstPlayer = ReturnFirstStrikeCharacter();
        PrimeMainMenuCameraForFirstUnit(upcomingFirstPlayer);

        //6 Intro Camera
        // Lance la séquence d'introduction des caméras avant de débuter les tours.
        // On verrouille explicitement les menus pendant toute la durée de la cinématique
        // afin d'éviter que le joueur ne sélectionne une action avant la fin des animations.
        battleIntroMenusLocked = true;
        yield return PlayIntroCameraSequence();
        // Les introductions sont terminées : les menus peuvent à présent accepter les entrées.
        battleIntroMenusLocked = false;

        // ✅ Les actions de combat redeviennent disponibles une fois la cinématique bouclée.
        //    Cette remise à zéro intervient immédiatement après le déverrouillage logique des menus.
        InputsManager.Instance?.RestoreBattleInputsAfterIntro();

        //7 Démarre la boucle de tours
        //    Depuis la suppression du mode lent, nous invoquons systématiquement la boucle classique.
        StartCoroutine(TurnLoop());

        //// Change l’état du jeu
        //GameManager.Instance.ChangeGameState(GameState.StartBattle);

        yield break;
    }

    //1 Filtrer pour ne garder que les unités dont les HP sont > 0
    private List<CharacterUnit> ReturnActiveUnits()
    {
        List<CharacterUnit> activeCharacterUnits = unitsInBattle.Where(c => c.currentHP > 0).ToList();
        return activeCharacterUnits;
    }

    //3 Affecter currentTarget au premier ennemi de la liste
    private void SetDefaultCurrentTarget()
    {
        currentTargetCharacter = activeCharacterUnits.FirstOrDefault(u => !u.Data.isPlayerControlled && u.currentHP > 0);

        if (currentTargetCharacter == null)
        {
            Debug.LogWarning("[BattleTurnManager] Aucun ennemi actif trouvé pour currentTargetCharacter.");
        }
    }

    //4 Réinitialise les ATB
    private void ResetAllATB()
    {
        foreach (var unit in activeCharacterUnits)
        {
            unit.currentATB = 0f;
        }
    }

    //5 Détermine quel joueur joue en premier
    public CharacterUnit ReturnFirstStrikeCharacter()
    {
        CharacterUnit firstPlayer = activeCharacterUnits
            .Where(u => u.Data.isPlayerControlled)
            .OrderByDescending(u => u.currentInitiative)
            .FirstOrDefault();

        return firstPlayer;
    }

    /// <summary>
    /// Pré-positionne la caméra de menu sur l'ancre de l'unité qui agira en premier.
    /// </summary>
    /// <param name="candidate">Unité pressentie pour jouer en ouverture.</param>
    private void PrimeMainMenuCameraForFirstUnit(CharacterUnit candidate)
    {
        if (candidate == null)
        {
            Debug.LogWarning("[BattleTurnManager] Aucun combattant joueur n'est disponible pour préparer la caméra du menu.");
            return;
        }

        var manager = BattleCameraManager.Instance;
        if (manager == null)
            return;

        manager.SetTurnOwner(candidate);
        manager.SetCurrentTarget(null);
        manager.SwitchToCamera(MainMenuCameraName, MenuCameraBlendDuration, MenuCameraBlendStyle);
    }

    //7 Démarre la boucle de tours
    private IEnumerator TurnLoop()
    {
        while (true)
        {
            if (unitsInBattle.All(u => u.currentHP <= 0))
            {
                Debug.LogWarning("[BattleTurnManager] Tous les combattants sont hors combat.");
                yield break;
            }

            // Avant de démarrer le prochain tour, on joue toutes les timelines en attente
            if (pendingTimelines.Count > 0)
                yield return PlayPendingTimelines();

            yield return ExecuteTurn(CalculateNextUnit());
            // Utilisation du temps non affecté par le timeScale pour ne pas bloquer
            // la boucle si le jeu est mis en pause (fin de combat par exemple)
            yield return new WaitForSecondsRealtime(0.2f);
        }
    }

    /// <summary>
    /// Ajoute une timeline à jouer avant le prochain tour.
    /// </summary>
    /// <param name="asset">Timeline à exécuter.</param>
    /// <param name="casterUnit">Unité propriétaire du PlayableDirector utilisé.</param>
    /// <param name="casterBinding">Objet de référence pour les bindings (Animator par exemple).</param>
    /// La caméra n'est plus liée à la timeline : elle sera simplement repositionnée sur le lanceur.
    public void QueueConditionalTimeline(TimelineAsset asset, CharacterUnit casterUnit, GameObject casterBinding = null)
    {
        if (asset == null || casterUnit == null)
            return;

        // Détermine automatiquement l'objet d'animation si aucun binding spécifique n'est fourni.
        // Sélection automatique de l'Animator enfant du lanceur pour respecter la règle de binding.
        GameObject binding = casterBinding ?? casterUnit.GetCasterBindingTarget();

        // Stocke la timeline à jouer et le lanceur associé
        pendingTimelines.Enqueue(new PendingTimeline(asset, casterUnit, binding));
    }

    /// <summary>
    /// Joue séquentiellement toutes les timelines en attente.
    /// </summary>
    private IEnumerator PlayPendingTimelines()
    {
        while (pendingTimelines.Count > 0)
        {
            var data = pendingTimelines.Dequeue();
            BattleTransitionManager.Instance?.HideBattleUI();

            // Aligne la caméra de combat sur l'unité concernée avant la cinématique
            if (data.casterBinding != null)
                BattleTimelineManager.Instance?.AlignCameraToTarget(data.casterBinding, "BattleCamera");

            bool timelinePlayed = false;

            if (BattleTimelineManager.Instance != null && data.unit != null)
            {
                BattleTimelineManager.Instance.PlayCasterTimeline(data.asset, data.unit, data.casterBinding);
                timelinePlayed = true;

                // Attente de la fin de la timeline avant de poursuivre
                while (BattleTimelineManager.Instance.IsCasterTimelinePlaying(data.unit))
                    yield return null;
            }

            // Fallback pour les situations de test où le BattleTimelineManager n'est pas présent
            if (!timelinePlayed && TimelineManager.Instance != null)
            {
                TimelineManager.Instance.PlayTimeline(data.asset, data.casterBinding, null);
                while (TimelineManager.Instance.IsTimelineActive)
                    yield return null;
            }

            BattleTransitionManager.Instance?.ShowBattleUIIfNeeded();
            // Petite pause pour éviter les enchaînements brusques
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }
    #endregion


    #region Gestion de l'orientation des unités
    public void OrientAllUnitsTowardCenter(CharacterUnit activeUnit)
    {
        foreach (var unit in activeCharacterUnits)
        {
            if (unit == null || unit == activeUnit)
                continue;

            // Calcul de la direction vers le centre (0,0,0)
            Vector3 dir = (Vector3.zero - unit.transform.position).normalized;
            if (dir == Vector3.zero)
                continue;

            // Angle entre la direction actuelle de l’unité (forward) et la nouvelle direction
            float angle = Vector3.Angle(unit.transform.forward, dir);
            if (angle > 90f)
            {
                // Si l’angle est > 90°, on déclenche le trigger "isTurning" sur l’Animator enfant
                Animator anim = unit.GetCasterAnimator();
                if (anim != null)
                {
                    anim.SetTrigger("isTurning");
                }
            }

            // On oriente instantanément l’unité vers la nouvelle direction (seulement sur l’axe Y)
            unit.transform.rotation = Quaternion.Euler(0, Quaternion.LookRotation(dir).eulerAngles.y, 0);
        }
    }

    public void OrientAllUnitsTowardEnemyGroupSmooth(float rotationSpeed = 360f)
    {
        foreach (var unit in activeCharacterUnits)
        {
            if (unit == null || unit.currentHP <= 0)
                continue;

            bool isPlayer = unit.Data.isPlayerControlled;

            // Trouve toutes les unités ennemies vivantes
            var enemies = unitsInBattle
                .Where(u => u != null && u.currentHP > 0 && u.Data.isPlayerControlled != isPlayer)
                .ToList();

            if (enemies.Count == 0)
                continue;

            // Calcul du barycentre des ennemis
            Vector3 averagePosition = Vector3.zero;
            foreach (var enemy in enemies)
                averagePosition += enemy.transform.position;
            averagePosition /= enemies.Count;

            // Direction vers le barycentre
            Vector3 direction = averagePosition - unit.transform.position;
            direction.y = 0f; // On ignore la hauteur pour une rotation horizontale.
            if (direction == Vector3.zero)
                continue;

            // Calcul de l’angle entre l’orientation actuelle (horizontal) et la cible
            Vector3 forward = unit.transform.forward;
            forward.y = 0f;
            float angle = Vector3.Angle(forward, direction);
            if (angle > 90f)
            {
                // Si > 90°, déclenche "isTurning" sur l’Animator enfant
                Animator anim = unit.GetCasterAnimator();
                if (anim != null)
                {
                    anim.SetTrigger("isTurning");
                }
            }

            // Normalise le vecteur horizontal obtenu.
            direction = direction.normalized;
            // Calcule une rotation ne contenant que l'angle autour de l'axe Y.
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            targetRotation = Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f);
            // Lance la rotation en douceur vers la rotation filtrée.
            StartCoroutine(RotateUnitSmoothly(unit, targetRotation, rotationSpeed));
        }
    }

    public void OrientTransformTowardEnemyGroupSmoothXY(Transform targetTransform, float rotationSpeed = 360f)
    {
        if (activeCharacterUnits == null || activeCharacterUnits.Count == 0)
            return;

        // Trouve tous les ennemis vivants (ou l'inverse si la cible est un ennemi)
        var enemies = activeCharacterUnits.Where(u => u != null && !u.Data.isPlayerControlled).ToList();

        if (enemies.Count == 0)
            return;

        // Calcul du barycentre du groupe d'ennemis
        Vector3 averagePosition = Vector3.zero;
        foreach (var enemy in enemies)
            averagePosition += enemy.transform.position;
        averagePosition /= enemies.Count;

        // Direction vers le barycentre
        Vector3 direction = (averagePosition - targetTransform.position).normalized;
        if (direction == Vector3.zero)
            return;

        // Calcul de la rotation visée : LookRotation sans roll
        Quaternion targetRotation = Quaternion.LookRotation(direction);

        // Filtrer pour ne garder que la rotation Yaw (Y) et Pitch (X)
        Vector3 euler = targetRotation.eulerAngles;
        Quaternion filteredRotation = Quaternion.Euler(euler.x, euler.y, 0f);

        // Lancer la coroutine pour tourner en douceur
        StartCoroutine(RotateTransformSmoothlyXY(targetTransform, filteredRotation, rotationSpeed));
    }

    public void OrientTransformTowardUnitSmoothXY(Transform targetTransform, CharacterUnit unit, float rotationSpeed = 360f)
    {
        if (targetTransform == null || unit == null)
            return;

        Vector3 direction = (unit.transform.position - targetTransform.position).normalized;
        if (direction == Vector3.zero)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        Vector3 euler = targetRotation.eulerAngles;
        Quaternion filteredRotation = Quaternion.Euler(euler.x, euler.y, 0f);

        StartCoroutine(RotateTransformSmoothlyXY(targetTransform, filteredRotation, rotationSpeed));
    }

    public void OrientUnitTowardTarget(CharacterUnit unit, CharacterUnit target, float rotationSpeed = 360f)
    {
        if (unit == null || target == null || unit.currentHP <= 0 || target.currentHP <= 0)
            return;

        // Calcule la direction vers la cible en ignorant la hauteur pour éviter toute inclinaison.
        Vector3 direction = target.transform.position - unit.transform.position;
        direction.y = 0f; // Rotation uniquement sur Y.
        if (direction == Vector3.zero)
            return;
        direction = direction.normalized;

        // Génère une rotation horizontale filtrée sur l'axe Y.
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        targetRotation = Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f);
        StartCoroutine(RotateUnitSmoothly(unit, targetRotation, rotationSpeed));
    }

    public void OrientUnitTowardClosestOpponent(CharacterUnit unit, float rotationSpeed = 360f)
    {
        if (unit == null || unit.Data == null || unit.currentHP <= 0)
            return;

        CharacterUnit targetUnit = null;

        if (unit == currentCharacterUnit && currentTargetCharacter != null && currentTargetCharacter.currentHP > 0)
        {
            targetUnit = currentTargetCharacter;
        }
        else
        {
            var enemies = unitsInBattle
                .Where(u => u != null && u.currentHP > 0 && u.Data.isPlayerControlled != unit.Data.isPlayerControlled)
                .OrderBy(u => Vector3.Distance(unit.transform.position, u.transform.position));

            targetUnit = enemies.FirstOrDefault();
        }

        if (targetUnit == null)
            return;

        Vector3 direction = targetUnit.transform.position - unit.transform.position;
        if (direction == Vector3.zero)
            return;

        // Supprime la composante verticale pour garantir une rotation horizontale.
        direction.y = 0f;
        direction = direction.normalized;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        targetRotation = Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f);
        StartCoroutine(RotateUnitSmoothly(unit, targetRotation, rotationSpeed));
    }

    public void OrientAllUnitsTowardClosestOpponent(float rotationSpeed = 360f)
    {
        // Angle minimum exprimé en pourcentage de 180° (écart maximal utile entre deux orientations).
        // 15 % correspond ici à ~27° : en dessous de ce seuil, on considère que la rotation est
        // imperceptible et qu'il vaut mieux éviter de lancer l'animation « Turn_90 » pour ne pas
        // perturber la lisibilité.
        const float minimalAngleRatio = 0.15f;

        foreach (var unit in activeCharacterUnits)
        {
            if (unit == null || unit.Data == null || unit.currentHP <= 0)
                continue;

            // Recherche de l'adversaire le plus proche en réutilisant la logique de ciblage.
            CharacterUnit targetUnit = null;

            if (unit == currentCharacterUnit && currentTargetCharacter != null && currentTargetCharacter.currentHP > 0)
            {
                targetUnit = currentTargetCharacter;
            }
            else
            {
                targetUnit = unitsInBattle
                    .Where(u => u != null && u.currentHP > 0 && u.Data.isPlayerControlled != unit.Data.isPlayerControlled)
                    .OrderBy(u => Vector3.Distance(unit.transform.position, u.transform.position))
                    .FirstOrDefault();
            }

            if (targetUnit == null)
                continue;

            // Calcul de l'angle entre l'orientation actuelle (filtrée sur Y) et la direction vers l'adversaire.
            Vector3 direction = targetUnit.transform.position - unit.transform.position;
            direction.y = 0f;

            if (direction == Vector3.zero)
                continue;

            direction = direction.normalized;
            Quaternion currentRotation = Quaternion.Euler(0f, unit.transform.eulerAngles.y, 0f);
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            targetRotation = Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f);

            float angleToTarget = Quaternion.Angle(currentRotation, targetRotation);

            // Si l'écart est inférieur à 15 % de 180°, on évite toute réorientation pour ne pas
            // déclencher une animation inutile.
            if (angleToTarget < minimalAngleRatio * 180f)
                continue;

            OrientUnitTowardClosestOpponent(unit, rotationSpeed);
        }
    }

    private IEnumerator RotateTransformSmoothlyXY(Transform target, Quaternion targetRotation, float speed)
    {
        while (Quaternion.Angle(target.rotation, targetRotation) > 0.1f)
        {
            // Interpolation à vitesse constante
            target.rotation = Quaternion.RotateTowards(target.rotation, targetRotation, speed * Time.deltaTime);

            // En option : forcer Z à 0 à chaque frame pour éviter le roll parasite
            Vector3 euler = target.rotation.eulerAngles;
            target.rotation = Quaternion.Euler(euler.x, euler.y, 0f);

            yield return null;
        }

        // Force la rotation finale propre
        Vector3 finalEuler = targetRotation.eulerAngles;
        target.rotation = Quaternion.Euler(finalEuler.x, finalEuler.y, 0f);
    }


    private IEnumerator RotateUnitSmoothly(CharacterUnit unit, Quaternion targetRotation, float rotationSpeed)
    {
        Animator anim = unit.GetCasterAnimator();
        if (anim != null)
        {
            // Joue l'animation de rotation en boucle pendant la rotation
            anim.Play("Turn_90");
        }

        // Verrouille la rotation initiale sur l'axe Y pour éviter tout tilt.
        unit.transform.rotation = Quaternion.Euler(0f, unit.transform.eulerAngles.y, 0f);

        // Tant que l’angle entre la rotation actuelle et la cible reste significatif
        while (Quaternion.Angle(unit.transform.rotation, targetRotation) > 0.5f)
        {
            // Applique une rotation progressive uniquement sur l'axe Y
            Quaternion current = Quaternion.Euler(0f, unit.transform.eulerAngles.y, 0f);
            unit.transform.rotation = Quaternion.RotateTowards(
                current,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
            yield return null;
        }

        // Orientation finale propre (uniquement sur l’axe Y)
        unit.transform.rotation = Quaternion.Euler(0, targetRotation.eulerAngles.y, 0);

        // Lecture instantanée de l'animation "Idle_Battle" une fois la rotation terminée
        if (anim != null)
        {
            anim.Play("Idle_Battle");
        }
    }
    #endregion

    #region Gestion de la fin du combat
    private void HandleEndOfBattle()
    {
        if (currentBattleState == BattleState.None
            || currentBattleState == BattleState.VictoryScreen_Await
            || currentBattleState == BattleState.VictoryScreen_CanContinue
            || currentBattleState == BattleState.GameOverScreen_Await
            || currentBattleState == BattleState.GameOverScreen_CanContinue)
        {
            return;
        }

        bool allEnemiesDead = unitsInBattle
            .Where(u => u != null)
            .Where(u => u.Data.characterType == CharacterType.EnemyUnit)
            .All(u => u.currentHP <= 0);

        bool allSquadDead = unitsInBattle
            .Where(u => u != null)
            .Where(u => u.Data.characterType == CharacterType.SquadUnit)
            .All(u => u.currentHP <= 0);

        if (allEnemiesDead
            && !victorySequenceInProgress
            && currentBattleState != BattleState.VictoryScreen_Await
            && currentBattleState != BattleState.VictoryScreen_CanContinue)
        {
            Debug.Log("[BattleTurnManager] 🎉 Tous les ennemis sont vaincus !");
            lastBattleOutcome = BattleOutcome.Victory; // Enregistre l'issue du combat
            StartCoroutine(ReduceTimeAndShowVictoryPanel());
        }
        else if (allSquadDead)
        {
            Debug.Log("[BattleTurnManager] 💀 Tous les alliés sont morts...");
            lastBattleOutcome = BattleOutcome.Defeat;

            if (defeatTimeline != null)
            {
                respawnAtCheckpointOnExit = false;
                // Aucune interface de Game Over, on quitte directement le combat
                if (BattleTransitionManager.Instance != null)
                    BattleTransitionManager.Instance.StartCoroutine(
                        BattleTransitionManager.Instance.ExitVictoryScreenAndBattle());
                else
                    Debug.LogWarning("[BattleTurnManager] BattleTransitionManager introuvable pour quitter le combat.");
            }
            else if (gameOverOnDefeat)
            {
                // Passage direct a l'exploration avec respawn au checkpoint
                respawnAtCheckpointOnExit = true;
                if (BattleTransitionManager.Instance != null)
                    BattleTransitionManager.Instance.StartCoroutine(
                        BattleTransitionManager.Instance.ExitVictoryScreenAndBattle());
                else
                    Debug.LogWarning("[BattleTurnManager] BattleTransitionManager introuvable pour quitter le combat.");
            }
            else
            {
                // Affichage du panneau Game Over permettant de continuer la partie
                respawnAtCheckpointOnExit = true;
                ChangeBattleState(BattleState.GameOverScreen_Await);
                StartCoroutine(ShowGameOverPanel());
            }
        }
    }

    /// <summary>
    /// Coroutine en charge de déclencher la capture une fois que le rendu HDRP
    /// courant est totalement terminé. On évite ainsi d'invoquer Camera.Render()
    /// en plein enregistrement du RenderGraph (source des exceptions rencontrées).
    /// </summary>
    private Coroutine victoryScreenshotCoroutine;

    /// <summary>
    /// Prépare la capture d'écran de la caméra de combat sans forcer un nouveau
    /// rendu immédiat. La capture est effectuée à la fin du frame en cours pour
    /// rester compatible avec le RenderGraph de l'HDRP.
    /// </summary>
    public void TakeVictoryScreenshot()
    {
        if (VictoryScreenImage == null)
        {
            Debug.LogError("VictoryScreenImage n'est pas assigné !");
            return;
        }

        // Empêche le lancement de plusieurs captures simultanées ; seule la plus
        // récente est conservée pour ne pas multiplier les attentes d'une frame.
        if (victoryScreenshotCoroutine != null)
        {
            StopCoroutine(victoryScreenshotCoroutine);
            victoryScreenshotCoroutine = null;
        }

        victoryScreenshotCoroutine = StartCoroutine(CaptureVictoryScreenshotAtFrameEnd());
    }

    /// <summary>
    /// Attend la fin du frame courant puis laisse l'HDRP dessiner naturellement la
    /// caméra dans la RenderTexture désirée. En procédant ainsi, aucune commande
    /// de rendu imbriquée n'est exécutée et l'exception RenderGraph disparaît.
    /// </summary>
    private IEnumerator CaptureVictoryScreenshotAtFrameEnd()
    {
        // Vérifie que le GameObject est actif pour éviter les captures alors que
        // le manager serait désactivé (par exemple lors des transitions de scène).
        if (!isActiveAndEnabled)
        {
            victoryScreenshotCoroutine = null;
            yield break;
        }

        // Utilise la caméra mémorisée ; la recherche est effectuée ponctuellement si nécessaire.
        EnsureBattleCamera();
        Camera screenshotCamera = battleCamera?.GetComponent<Camera>();
        if (screenshotCamera == null)
        {
            Debug.LogError("Aucune caméra trouvée pour la capture !");
            victoryScreenshotCoroutine = null;
            yield break;
        }

        // On redirige la sortie de la caméra vers la RenderTexture demandée.
        if (!VictoryScreenImage.IsCreated())
            VictoryScreenImage.Create();

        RenderTexture previousTarget = screenshotCamera.targetTexture;
        screenshotCamera.targetTexture = VictoryScreenImage;

        // Attendre la fin du frame courant garantit que la caméra sera rendue
        // naturellement par le pipeline HDRP sans appel imbriqué de RenderGraph.
        yield return new WaitForEndOfFrame();

        // Une fois la frame terminée, on restaure la cible initiale pour éviter
        // d'altérer le comportement normal de la caméra dans les frames suivantes.
        screenshotCamera.targetTexture = previousTarget;

        Debug.Log("Screenshot de victoire capturé sans appel direct à Camera.Render().");

        victoryScreenshotCoroutine = null;
    }

    private IEnumerator ReduceTimeAndShowVictoryPanel()
    {
        victorySequenceInProgress = true; // 🔒 Bloque tout nouveau déclenchement pendant la séquence.

        // 1️⃣ Capture la dernière image du combat au moment de la victoire.
        yield return CaptureVictoryScreenshotBeforeCameraSwap();

        // 2️⃣ Ralentit progressivement le temps jusqu'à l'arrêt complet pour figer la scène.
        if (BattleTransitionManager.Instance != null)
        {
            yield return BattleTransitionManager.Instance.StartCoroutine(
                BattleTransitionManager.Instance.SlowTimeScale(0f, 0.5f));
        }
        else
        {
            Time.timeScale = 0f;
        }

        Time.fixedDeltaTime = 0f; // Les physiques sont également gelées.

        // 3️⃣ Demande officiellement la caméra de victoire afin de lancer le travelling.
        ChangeBattleState(BattleState.VictoryScreen_Await);
        battleIntroMenusLocked = false;

        // 4️⃣ Patiente jusqu'à la fin du blend Cinemachine pour que le plan soit parfaitement cadré.
        yield return WaitForVictoryCameraToSettle();

        // 5️⃣ L'interface de victoire peut maintenant se superposer au plan stabilisé.
        victoryScreen.SetActive(true);
        ForceUnscaledAnimators(victoryScreen.transform);
        InputsManager.Instance?.ForceDynamicInputUpdate();
        Transform victoryPanel = victoryScreen.transform.GetChild(0);
        Animator victoryAnim = victoryPanel.GetComponent<Animator>();
        if (victoryAnim != null)
        {
            victoryAnim.updateMode = AnimatorUpdateMode.UnscaledTime;
        }
        victoryPanel.gameObject.SetActive(true);

        InputsManager.Instance?.playerInputs?.Battle.Confirm.Enable();

        // 6️⃣ Distribution des récompenses avant de les afficher sur le panneau.
        GameManager.Instance?.AddXPToSquad(rewardXP);
        GameManager.Instance?.AddItemsToInventory(rewardItems);

        var panel = victoryScreen.GetComponentInChildren<VictoryPanelManager>();

        float duration = Time.time - battleStartTime;
        int totalEnemies = GameManager.Instance != null ? GameManager.Instance.gameData.enemiesDefeatedCount : 0;
        panel?.DisplayVictory(rewardXP, rewardItems, totalEnemies, duration, mvpUnit, maxTurnDamage);

        // 7️⃣ Applique la capture sur le panneau de victoire.
        ApplyVictoryScreenTexture(VictoryScreenImage);

        Transform continueButtonTransform = FindChildRecursive(victoryScreen.transform.GetChild(0), "BattleScene_UI_VictoryPanel_Continue");

        if (continueButtonTransform == null)
        {
            // Avertit immédiatement si la hiérarchie UI ne contient plus le bouton attendu :
            // sans cette vérification, un NullReferenceException interrompait la coroutine et
            // empêchait la transition vers l'état VictoryScreen_CanContinue, bloquant ainsi
            // la touche Confirm.
            Debug.LogWarning("[BattleTurnManager] Bouton 'Continue' introuvable dans le panneau de victoire.");
        }
        else
        {
            GameObject continueButton = continueButtonTransform.gameObject;

            // ➡️ Branche un listener explicite sur le bouton Continue pour permettre la sortie via la souris/manette
            Button uiButton = continueButton.GetComponent<Button>();
            if (uiButton != null)
            {
                // On s'assure de ne pas empiler les callbacks si le panneau est affiché plusieurs fois d'affilée
                uiButton.onClick.RemoveListener(HandleVictoryContinueRequested);
                uiButton.onClick.AddListener(HandleVictoryContinueRequested);
            }

            // Sélectionne le bouton dans l'EventSystem afin que la manette/le clavier puisse valider immédiatement
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(continueButton);
            }
        }

        // Le champ de bataille doit disparaitre des l'affichage de la victoire.
        ChangeBattleState(BattleState.VictoryScreen_CanContinue);

        HideBattlefieldVisualsOnVictory();

        victorySequenceInProgress = false;
    }

    /// <summary>
    /// Lance la capture asynchrone du screenshot de victoire puis attend sa complétion.
    /// Cette étape est effectuée avant la transition caméra pour figer exactement le
    /// dernier instant du combat.
    /// </summary>
    private IEnumerator CaptureVictoryScreenshotBeforeCameraSwap()
    {
        // Déclenche la capture si la RenderTexture est correctement configurée.
        TakeVictoryScreenshot();

        // Si aucune coroutine n'a démarré (RenderTexture manquante, manager désactivé...)
        // on attend tout de même une frame pour rester déterministe, puis on abandonne.
        if (victoryScreenshotCoroutine == null)
        {
            yield return null;
            yield break;
        }

        // Boucle d'attente non bloquante : la capture se termine dès la fin du frame courant.
        while (victoryScreenshotCoroutine != null)
        {
            yield return null;
        }
    }

    /// <summary>
    /// Patiente jusqu'à ce que la caméra Cinemachine « CMV_Victory » soit pleinement active
    /// et que son blend lissé soit achevé. On évite ainsi d'afficher l'UI tant que le plan
    /// n'est pas parfaitement cadré.
    /// </summary>
    private IEnumerator WaitForVictoryCameraToSettle()
    {
        var cameraManager = BattleCameraManager.Instance;
        if (cameraManager == null)
        {
            yield break; // Sécurité : aucune caméra gérée, on évite de bloquer la coroutine.
        }

        // Première étape : s'assurer que la caméra de victoire est bien la cible actuelle.
        float ensureTimer = 0f;
        const float ensureTimeout = 1f; // marge suffisante pour couvrir un frame de retard.
        while (!string.Equals(cameraManager.CurrentCinemachineCameraName, VictoryCameraName, StringComparison.OrdinalIgnoreCase)
            && ensureTimer < ensureTimeout)
        {
            ensureTimer += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!string.Equals(cameraManager.CurrentCinemachineCameraName, VictoryCameraName, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("[BattleTurnManager] Impossible de confirmer la caméra de victoire active.");
            yield break;
        }

        // Deuxième étape : attendre la fin effective du blend Smooth imposé (0,5 s).
        float blendDuration = Mathf.Max(cameraManager.SmoothBlendDuration, 0f);
        float elapsed = 0f;
        // Ajoute une petite marge pour éviter les erreurs d'arrondi et garantir un plan stabilisé.
        float paddedDuration = blendDuration + 0.05f;
        while (elapsed < paddedDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static void ForceUnscaledAnimators(Transform root)
    {
        if (root == null)
            return;

        var animators = root.GetComponentsInChildren<Animator>(true);
        foreach (var animator in animators)
        {
            if (animator != null)
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        var pulses = root.GetComponentsInChildren<Pulse>(true);
        foreach (var pulse in pulses)
        {
            if (pulse != null)
                pulse.forceUnscaledTime = true;
        }
    }

    private void ApplyVictoryScreenTexture(RenderTexture texture)
    {
        if (texture == null || victoryScreen == null)
            return;

        Transform panel = FindChildRecursive(victoryScreen.transform, "BattleScene_UI_VictoryScreen_Panel");
        RawImage img = panel != null ? panel.GetComponent<RawImage>() : null;

        if (img == null)
            img = victoryScreen.GetComponentInChildren<RawImage>(true);

        if (img != null)
            img.texture = texture;
        else
            Debug.LogWarning("[BattleTurnManager] RawImage introuvable pour le fond de l'ecran de victoire.");
    }

    private void HideBattlefieldVisualsOnVictory()
    {
        BattlefieldManager.Instance?.SetBattlefieldVisible(false);

        foreach (var unit in unitsInBattle)
        {
            if (unit == null)
                continue;

            ToggleUnitRenderers(unit, false);
        }
    }

    private static void ToggleUnitRenderers(CharacterUnit unit, bool visible)
    {
        if (unit == null)
            return;

        var renderers = unit.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer != null)
                renderer.enabled = visible;
        }
    }

    /// <summary>
    /// Callback déclenché par le bouton "Continuer" de l'écran de victoire.
    /// Permet d'appliquer exactement la même logique que la touche de validation.
    /// </summary>
    private void HandleVictoryContinueRequested()
    {
        // Évite les validations intempestives si l'état du combat n'est pas prêt.
        if (currentBattleState != BattleState.VictoryScreen_CanContinue
            && currentBattleState != BattleState.GameOverScreen_CanContinue)
            return;

        ChangeBattleState(BattleState.None);

        // Lancement sécurisé de la transition de sortie de combat
        var transitionManager = BattleTransitionManager.Instance;
        if (transitionManager != null)
        {
            // La coroutine interne est désormais protégée contre les doubles lancements,
            // mais on centralise tout de même l'appel pour éviter toute NullReference.
            transitionManager.StartCoroutine(transitionManager.ExitVictoryScreenAndBattle());
        }
        else
        {
            Debug.LogWarning("[NewBattleManager] BattleTransitionManager introuvable lors de la demande de sortie de combat.");
        }
    }

    IEnumerator ShowGameOverPanel()
    {
        // Ralentit le temps jusqu'à l'arrêt complet avant d'afficher le panneau
        if (BattleTransitionManager.Instance != null)
            yield return BattleTransitionManager.Instance.StartCoroutine(
                BattleTransitionManager.Instance.SlowTimeScale(0f, 0.5f));
        else
            Time.timeScale = 0f;

        Time.fixedDeltaTime = 0f;

        // Activation du panneau GameOver (animation en temps réel)
        Transform panel = gameOverScreen.transform.GetChild(0);
        Animator anim = panel.GetComponent<Animator>();
        if (anim != null)
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;
        panel.gameObject.SetActive(true);
        ForceUnscaledAnimators(gameOverScreen.transform);
        InputsManager.Instance?.ForceDynamicInputUpdate();
        battleIntroMenusLocked = false;
        InputsManager.Instance?.playerInputs?.Battle.Confirm.Enable();

        CleanupAllSpawnedUnits();

        ChangeBattleState(BattleState.GameOverScreen_CanContinue);
    }

    private void CleanupAllSpawnedUnits()
    {
        foreach (var unit in unitsInBattle)
            if (unit != null)
                Destroy(unit.gameObject);

        unitsInBattle.Clear();
        // Évite de conserver des références obsolètes qui empêcheraient un nouveau spawn
        activeCharacterUnits.Clear();
    }
    #endregion

    #region Gestion de l’ouverture des menus

    private Transform ResolveBattleCameraRig()
    {
        if (battleCameraRig != null)
            return battleCameraRig;

        if (battleCamera != null)
        {
            Camera[] cameras = battleCamera.GetComponentsInChildren<Camera>(true);
            foreach (Camera cam in cameras)
            {
                if (cam != null && cam.name == "BattleCamera_Cam")
                {
                    battleCameraRig = cam.transform;
                    return battleCameraRig;
                }
            }
        }

        var cameraGO = GameObject.Find("BattleCamera_Cam");
        if (cameraGO == null)
        {
            Debug.LogWarning("[NewBattleManager] Impossible de localiser 'BattleCamera_Cam' dans la scène.");
            return null;
        }

        battleCameraRig = cameraGO.transform;
        return battleCameraRig;
    }

    private void SetupCurrentUnitMenus()
    {
        // 1) Essaye de récupérer la BattleCamera par tag
        // Utilise la caméra de combat mémorisée
        Transform battleCamCam = ResolveBattleCameraRig();
        if (battleCamCam == null)
        {
            Debug.LogWarning("[SetupCurrentUnitMenus] BattleCamera introuvable.");
            return;
        }

        Transform mainPanel = FindChildRecursive(battleCamCam, "MainMenu_Panel");
        if (mainPanel == null)
        {
            Debug.LogWarning("[SetupCurrentUnitMenus] 'MainMenu_Panel' introuvable sous la BattleCamera.");
            currentMainMenuContainer = null;
            currentMainMenuSlots = new List<Transform>();
        }
        else
        {
            currentMainMenuContainer = mainPanel.gameObject;
            // On recherche ensuite le container « Menu » à l’intérieur du MainMenu_Panel
            Transform mainSlotsParent = FindChildRecursive(mainPanel, "Menu");
            currentMainMenuSlots = (mainSlotsParent != null)
                ? mainSlotsParent.Cast<Transform>().ToList()
                : new List<Transform>();
        }

        // 3) Panneau SkillsMenu_Panel
        Transform skillsPanel = FindChildRecursive(battleCamCam, "SkillsMenu_Panel");
        if (skillsPanel == null)
        {
            Debug.LogWarning("[SetupCurrentUnitMenus] 'SkillsMenu_Panel' introuvable sous la BattleCamera.");
            currentSkillsMenuContainer = null;
            currentSkillsMenuSlots = new List<Transform>();
        }
        else
        {
            currentSkillsMenuContainer = skillsPanel.gameObject;
            Transform skillsSlotsParent = FindChildRecursive(skillsPanel, "Menu");
            currentSkillsMenuSlots = (skillsSlotsParent != null)
                ? skillsSlotsParent.Cast<Transform>().ToList()
                : new List<Transform>();

            // S'assure de disposer d'au moins 5 emplacements :
            //  - Slot 0 : texte / action d'attaque de base
            //  - Slots 1 à 3 : attaques musicales
            //  - Slot 4 : move spécial
            // L'objectif est d'éviter tout débordement lorsque nous injectons dynamiquement
            // les attaques de la SquadUnit courante. On privilégie un clonage du premier slot
            // (structure déjà configurée dans l'éditeur) pour conserver les bons réglages d'UI.
            if (currentSkillsMenuSlots.Count > 0)
            {
                while (currentSkillsMenuSlots.Count < 5)
                {
                    // Clone le premier slot pour compléter la liste si besoin et préserver
                    // l'homogénéité visuelle avec les éléments déjà configurés dans la scène.
                    Transform clone = Instantiate(currentSkillsMenuSlots[0], currentSkillsMenuSlots[0].parent);
                    currentSkillsMenuSlots.Add(clone);
                }
            }
            else
            {
                // Aucun slot n'a été trouvé : on enregistre un avertissement explicite pour faciliter
                // le diagnostic (probable régression de la hiérarchie UI) tout en évitant un crash.
                Debug.LogWarning("[SetupCurrentUnitMenus] Aucun slot de compétence détecté ; impossible de garantir les 5 emplacements requis.");
            }
        }

        // 4) Panneau ItemsMenu_Panel
        Transform itemsPanel = FindChildRecursive(battleCamCam, "ItemsMenu_Panel");
        if (itemsPanel == null)
        {
            Debug.LogWarning("[SetupCurrentUnitMenus] 'ItemsMenu_Panel' introuvable sous la BattleCamera.");
            currentItemsMenuContainer = null;
            currentItemsMenuSlots = new List<Transform>();
        }
        else
        {
            currentItemsMenuContainer = itemsPanel.gameObject;
            Transform itemsSlotsParent = FindChildRecursive(itemsPanel, "Menu");
            currentItemsMenuSlots = (itemsSlotsParent != null)
                ? itemsSlotsParent.Cast<Transform>().ToList()
                : new List<Transform>();
        }
    }

    /// <summary>
    /// Vérifie qu'une unité est bien renseignée pour les menus et essaie de la récupérer sinon.
    /// Cette méthode est indispensable pour garantir que les caméras Cinemachine (et notamment
    /// « CMV_ItemsMenu ») disposent toujours d'une ancre valide lorsque l'on ouvre un menu.
    /// </summary>
    /// <param name="callerContext">Nom de la méthode appelante (utilisé dans les logs).</param>
    private bool EnsureCurrentCharacterUnitForMenus(string callerContext)
    {
        if (currentCharacterUnit != null)
            return true; // Cas nominal : rien à corriger.

        CharacterUnit resolvedUnit = null;

        // 1) Premier réflexe : demander au gestionnaire de caméras quelle unité il suit encore.
        BattleCameraManager cameraManager = BattleCameraManager.Instance;
        if (cameraManager != null)
            resolvedUnit = cameraManager.CurrentTurnOwner;

        // 2) Si la caméra n'a plus l'information, on tente de détecter l'unité dont la jauge ATB
        //    a atteint le seuil. C'est normalement la seule à pouvoir ouvrir les menus de tour.
        if (resolvedUnit == null)
        {
            resolvedUnit = activeCharacterUnits.FirstOrDefault(u =>
                u != null &&
                u.Data != null &&
                u.Data.isPlayerControlled &&
                u.currentHP > 0 &&
                u.currentATB >= ATB_THRESHOLD);
        }

        // 3) Fallback supplémentaire : choisir la première unité jouable côté joueur encore en vie.
        if (resolvedUnit == null)
        {
            resolvedUnit = activeCharacterUnits.FirstOrDefault(u =>
                u != null &&
                u.Data != null &&
                u.Data.isPlayerControlled &&
                u.currentHP > 0);
        }

        if (resolvedUnit == null)
        {
            Debug.LogError($"[{callerContext}] Impossible d'identifier l'unité active avant l'ouverture d'un menu. " +
                           "La caméra ne peut donc pas se caler sur le CMVPoint approprié.");
            return false;
        }

        // On mémorise la nouvelle unité pour que tout le pipeline (menus, caméra, timelines) reste cohérent.
        ChangeCurrentCharacterUnit(resolvedUnit);

        // Le gestionnaire caméra doit également être synchronisé afin de replacer immédiatement les rigs.
        if (cameraManager != null && cameraManager.CurrentTurnOwner != resolvedUnit)
            cameraManager.SetTurnOwner(resolvedUnit);

        return true;
    }

    public void ShowMainMenu()
    {
        if (!EnsureCurrentCharacterUnitForMenus(nameof(ShowMainMenu)))
            return;

        // Réaffiche la jauge de passage de tour si elle existe.
        PassTurnUI.Instance?.Show();
        ActionUIDisplayManager.Instance.DisplayInstruction_SelectItemSkillOrPass();
        ChangeBattleState(BattleState.SquadUnit_MainMenu);
        ToggleMenuContainers(true, false, false);
        // Feedback audio systématique lorsque le menu principal devient visible.
        PlayMenuClip(mainMenuOpenClip);

        // S'assure que l'action "Confirm" est bien active.
        // Elle peut avoir été désactivée à la fin d'un QTE.
        InputsManager.Instance?.playerInputs.Battle.Confirm.Enable();

        // Arrête la Timeline d'attente si elle était en cours
        StopItemPreparingTimeline();

        // Réinitialise les actions sélectionnées afin d'éviter des conflits
        currentMove = null;
        currentItem = null;

        Debug.Log($"[ShowMainMenu] Nombre de slots = {currentMainMenuSlots.Count}");
        for (int i = 0; i < currentMainMenuSlots.Count; i++)
            Debug.Log($"Slot {i} = {currentMainMenuSlots[i].name}, enfants : {currentMainMenuSlots[i].childCount}");

        // Vérifie que la liste contient au moins deux slots avant de tenter de les utiliser
        if (currentMainMenuSlots == null || currentMainMenuSlots.Count < 2)
        {
            Debug.LogWarning("[ShowMainMenu] Impossible d'afficher le menu principal : nombre de slots insuffisant.");
            return; // Évite un ArgumentOutOfRangeException
        }

        // Renseigne les boutons du menu principal
        UpdateButton(currentMainMenuSlots[0], "Compétences", null);
        UpdateButton(currentMainMenuSlots[1], "Objet", null);
    }

    public void OpenSkillsMenu()
    {
        if (!EnsureCurrentCharacterUnitForMenus(nameof(OpenSkillsMenu)))
            return;

        // Masque la jauge lorsque l'on ouvre le menu des compétences.
        PassTurnUI.Instance?.Hide();
        ActionUIDisplayManager.Instance.DisplayInstruction_SelectSkill();
        ChangeBattleState(BattleState.SquadUnit_SkillsMenu);
        // S'assure qu'aucun item n'est en cours de sélection
        currentItem = null;
        ToggleMenuContainers(false, true, false);
        // Son distinct pour signaler l'entrée dans le menu des compétences.
        PlayMenuClip(skillsMenuOpenClip);
        currentMenuIndex = 0;

        // Réinitialise la page affichée
        currentSkillPageIndex = 0;

        // Récupère toutes les attaques musicales disponibles (hors move spécial)
        skillChoices = currentCharacterUnit.Data.musicalAttacks
            .Where(m => !m.onlyAwake || currentCharacterUnit.IsAwake)
            .Where(m => !m.enterAwake || !currentCharacterUnit.IsAwake)
            .Where(m => currentCharacterUnit.CanUseMove(m))
            .ToList();

        // Réordonne les attaques selon le set sélectionné afin de mettre en avant les favoris.
        skillChoices = currentCharacterUnit.OrderMovesForCurrentSet(skillChoices);

        // Identifie l'attaque basique dédiée au premier slot du SkillsMenu.
        // Même si cette attaque n'apparaît pas dans la liste (fallback global, limites d'utilisation...),
        // on force son affichage fixe pour respecter les attentes des joueurs.
        basicAttackMoveChoice = ResolveBasicAttackMove(currentCharacterUnit);
        if (basicAttackMoveChoice != null)
        {
            // On retire l'attaque basique de la liste paginée afin qu'elle ne se décale jamais
            // lorsqu'on feuillette les autres compétences musicales. La comparaison est volontairement
            // plus souple (référence + noms) pour couvrir les cas où le ScriptableObject serait dupliqué
            // dans un set tout en pointant vers la même action de base.
            skillChoices.RemoveAll(move => IsEquivalentToBasicAttack(move, basicAttackMoveChoice));
        }

        // Détermine le mouvement spécial autorisé, qui sera affiché dans le 4e slot
        specialMoveChoice = (currentCharacterUnit.Data.specialMusicalMove != null &&
            currentCharacterUnit.CanUseMove(currentCharacterUnit.Data.specialMusicalMove))
            ? currentCharacterUnit.Data.specialMusicalMove
            : null;

        // Affiche la première page de compétences
        RefreshSkillsMenuDisplay();
    }

    /// <summary>
    /// Rafraîchit l'affichage des compétences selon la page courante.
    /// </summary>
    public void RefreshSkillsMenuDisplay()
    {
        // Sécurise l'accès à la liste des slots de compétences
        if (currentSkillsMenuSlots == null || currentSkillsMenuSlots.Count == 0)
        {
            Debug.LogWarning("[RefreshSkillsMenuDisplay] Aucun slot de compétences disponible.");
            return;
        }

        // Le dernier slot du SkillsMenu est réservé au Special Musical Move
        int specialSlotIndex = currentSkillsMenuSlots.Count - 1;
        if (specialSlotIndex < 0)
        {
            Debug.LogWarning("[RefreshSkillsMenuDisplay] Index de slot spécial invalide.");
            return;
        }

        // 0) Assure l'affichage fixe de l'attaque basique dans le tout premier slot
        if (currentSkillsMenuSlots.Count > 0)
        {
            Transform basicSlot = currentSkillsMenuSlots[0];
            if (basicAttackMoveChoice != null && currentCharacterUnit != null && currentCharacterUnit.Data != null)
            {
                UpdateButton(basicSlot, basicAttackMoveChoice.moveName, basicAttackMoveChoice.moveIcon, basicAttackMoveChoice.description);

                bool enoughHarmonic = currentCharacterUnit.GetHarmonicCount(basicAttackMoveChoice.consumedHarmonicType) >= basicAttackMoveChoice.harmonicCost;
                bool resonanceOk = !basicAttackMoveChoice.enterAwake || currentCharacterUnit.GetHarmonicCount(currentCharacterUnit.Data.harmonicType) >= currentCharacterUnit.Data.awakeHarmonicThreshold;
                bool usageOk = currentCharacterUnit.CanUseMove(basicAttackMoveChoice);
                bool cooldownOk = !currentCharacterUnit.IsMoveOnCooldown(basicAttackMoveChoice);
                bool available = enoughHarmonic && resonanceOk && usageOk && cooldownOk;
                SetButtonAvailability(basicSlot, available, false);
            }
            else
            {
                // Fallback visuel en cas d'absence d'attaque basique (situation exceptionnelle)
                if (emptyMove != null)
                    UpdateButton(basicSlot, emptyMove.moveName, emptyMove.moveIcon);
                else
                    UpdateButton(basicSlot, "Indisponible", null);
                SetButtonAvailability(basicSlot, false, false);
            }
        }

        // On récupère dynamiquement les indices réellement disponibles pour les attaques musicales standard. En cas de
        // modification de la hiérarchie (slot manquant, renommé, etc.), la liste s'ajustera automatiquement plutôt que
        // d'écraser les emplacements réservés à l'attaque de base ou au mouvement spécial.
        List<int> paginatedSlotIndices = BuildPaginatedSkillSlotIndices();
        int pageSize = paginatedSlotIndices.Count;

        if (pageSize > 0)
        {
            int maxPage = Mathf.Max(0, (skillChoices.Count - 1) / pageSize);
            currentSkillPageIndex = Mathf.Clamp(currentSkillPageIndex, 0, maxPage);
        }
        else
        {
            currentSkillPageIndex = 0;
        }

        int startIndex = pageSize > 0 ? currentSkillPageIndex * pageSize : 0;

        // 1) Affiche les attaques musicales paginées (hors attaque basique et move spécial)
        for (int i = 0; i < pageSize; i++)
        {
            int slotIndex = paginatedSlotIndices[i];
            Transform slot = currentSkillsMenuSlots[slotIndex];
            int globalIndex = startIndex + i;
            if (globalIndex < skillChoices.Count)
            {
                var move = skillChoices[globalIndex];
                UpdateButton(slot, move.moveName, move.moveIcon, move.description);

                bool enoughHarmonic = currentCharacterUnit.GetHarmonicCount(move.consumedHarmonicType) >= move.harmonicCost;
                bool resonanceOk = !move.enterAwake || currentCharacterUnit.GetHarmonicCount(currentCharacterUnit.Data.harmonicType) >= currentCharacterUnit.Data.awakeHarmonicThreshold;
                bool usageOk = currentCharacterUnit.CanUseMove(move);
                bool available = enoughHarmonic && resonanceOk && usageOk;
                SetButtonAvailability(slot, available, false);
            }
            else
            {
                // Slot vide ou hors de portée
                if (emptyMove != null)
                    UpdateButton(slot, emptyMove.moveName, emptyMove.moveIcon);
                else
                    UpdateButton(slot, "Indisponible", null);
                SetButtonAvailability(slot, false, false);
            }
        }

        // 2) Place le mouvement spécial dans le dernier slot
        if (specialMoveChoice != null)
        {
            UpdateButton(currentSkillsMenuSlots[specialSlotIndex], specialMoveChoice.moveName, specialMoveChoice.moveIcon, specialMoveChoice.description);

            bool enoughHarmonic = currentCharacterUnit.GetHarmonicCount(specialMoveChoice.consumedHarmonicType) >= specialMoveChoice.harmonicCost;
            bool resonanceOk = !specialMoveChoice.enterAwake || currentCharacterUnit.GetHarmonicCount(currentCharacterUnit.Data.harmonicType) >= currentCharacterUnit.Data.awakeHarmonicThreshold;
            bool usageOk = currentCharacterUnit.CanUseMove(specialMoveChoice);
            bool available = enoughHarmonic && resonanceOk && usageOk;
            SetButtonAvailability(currentSkillsMenuSlots[specialSlotIndex], available, available);
        }
        else
        {
            if (emptyMove != null)
                UpdateButton(currentSkillsMenuSlots[specialSlotIndex], emptyMove.moveName, emptyMove.moveIcon);
            else
                UpdateButton(currentSkillsMenuSlots[specialSlotIndex], "Indisponible", null);
            SetButtonAvailability(currentSkillsMenuSlots[specialSlotIndex], false, false);
        }
    }

    /// <summary>
    /// Passe à la page suivante des compétences si possible.
    /// </summary>
    public void NextSkillPage()
    {
        // Calcule le nombre de slots paginés (hors attaque basique et move spécial)
        int pageSize = GetPaginatedSkillSlotCount();
        if (pageSize <= 0)
        {
            // Évite une division par zéro si aucun slot paginé n'est disponible
            Debug.LogWarning("[NextSkillPage] Impossible de changer de page : aucun slot paginé.");
            return;
        }

        int maxPage = Mathf.Max(0, (skillChoices.Count - 1) / pageSize);
        if (currentSkillPageIndex < maxPage)
        {
            currentSkillPageIndex++;
            // Retour audio pour confirmer le changement de page.
            PlayMenuClip(skillPageChangeClip);
            RefreshSkillsMenuDisplay();
        }
    }

    /// <summary>
    /// Revient à la page précédente des compétences si possible.
    /// </summary>
    public void PreviousSkillPage()
    {
        // Empêche toute interaction si aucun slot paginé n'est disponible (par exemple lorsque seul
        // l'attaque de base et le move spécial sont configurés dans l'UI).
        if (GetPaginatedSkillSlotCount() <= 0)
            return;

        // Vérifie qu'il existe réellement des pages avant de revenir en arrière
        if (currentSkillPageIndex > 0)
        {
            currentSkillPageIndex--;
            // Même feedback lors d'un retour vers une page précédente.
            PlayMenuClip(skillPageChangeClip);
            RefreshSkillsMenuDisplay();
        }
    }

    public void OpenItemMenu()
    {
        if (!EnsureCurrentCharacterUnitForMenus(nameof(OpenItemMenu)))
            return;

        ActionUIDisplayManager.Instance.DisplayInstruction_SelectItem();
        ChangeBattleState(BattleState.SquadUnit_ItemsMenu);
        // S'assure qu'aucune compétence n'est en cours de sélection
        currentMove = null;
        ToggleMenuContainers(false, false, true);
        // Clip spécifique pour différencier l'accès à l'inventaire.
        PlayMenuClip(itemsMenuOpenClip);
        currentMenuIndex = 0;

        // Démarre la Timeline d'attente de sélection d'objet
        StartItemPreparingTimeline();

        itemChoices = InventoryManager.Instance.GetUsableItems(currentCharacterUnit);

        // 6) Création des boutons d’items
        for (int i = 0; i < itemChoices.Count && i < currentItemsMenuSlots.Count; i++)
        {
            var item = itemChoices[i];
            UpdateButton(currentItemsMenuSlots[i], item.itemName, item.itemIcon, item.description);
        }

        // Indique les emplacements vides
        for (int j = itemChoices.Count; j < currentItemsMenuSlots.Count; j++)
        {
            if (emptyMove != null)
                UpdateButton(currentItemsMenuSlots[j], emptyMove.moveName, emptyMove.moveIcon);
            else
                UpdateButton(currentItemsMenuSlots[j], "Indisponible", null);
        }
    }

    public void ToggleMenuContainers(bool showMain, bool showSkills, bool showItems)
    {
        // Sécurité supplémentaire : on vérifie que l'unité courante et ses données sont valides
        if (currentCharacterUnit == null || currentCharacterUnit.Data == null)
        {
            Debug.LogWarning("[ToggleMenuContainers] currentCharacterUnit ou ses données sont null.");
            return;
        }

        // Si les références aux menus ne sont pas initialisées, on interrompt l'action
        if (currentMainMenuContainer == null || currentSkillsMenuContainer == null || currentItemsMenuContainer == null)
        {
            Debug.LogWarning("[ToggleMenuContainers] Menus non initialisés correctement.");
            return;
        }

        // Activation/désactivation des différents menus selon les paramètres
        currentMainMenuContainer.SetActive(showMain);
        currentSkillsMenuContainer.SetActive(showSkills);
        currentItemsMenuContainer.SetActive(showItems);
    }

    private void UpdateButton(Transform slot, string label, Sprite icon, string description = null)
    {
        if (slot == null || slot.childCount == 0)
        {
            Debug.LogWarning($"[UpdateButton] Slot invalide ou vide : {slot?.name}");
            return;
        }

        if (slot == null)
        {
            Debug.LogWarning($"[UpdateButton] L’enfant du slot {slot.name} est null.");
            return;
        }

        var txt = slot.GetComponentInChildren<TextMeshProUGUI>();
        var slotRect = slot as RectTransform;
        var img = slot.childCount > 3 ? slot.GetChild(3).GetComponent<Image>() : null;

        if (txt != null)
        {
            ConfigureMenuTextAutosizing(txt);

            if (!string.IsNullOrWhiteSpace(description))
                txt.text = $"{label}\n<color=#CFCFCF>{description}</color>";
            else
                txt.text = label;

            AdjustMenuSlotToText(txt, slotRect);
        }
        if (img != null) img.sprite = icon;
    }

    private void ConfigureMenuTextAutosizing(TextMeshProUGUI txt)
    {
        txt.enableAutoSizing = true;
        txt.fontSizeMin = MinMenuFontSize;
        if (txt.fontSizeMax < txt.fontSize)
            txt.fontSizeMax = txt.fontSize;
    }

    private void AdjustMenuSlotToText(TextMeshProUGUI txt, RectTransform slotRect)
    {
        if (txt == null || slotRect == null)
            return;

        RectTransform textRect = txt.rectTransform;

        if (!baseMenuTextHeights.ContainsKey(txt))
            baseMenuTextHeights[txt] = textRect.sizeDelta.y;

        if (!baseMenuSlotHeights.ContainsKey(slotRect))
            baseMenuSlotHeights[slotRect] = slotRect.sizeDelta.y;

        // Met à jour les métriques après application du nouveau contenu.
        txt.ForceMeshUpdate(true, true);

        float baseTextHeight = baseMenuTextHeights[txt];
        float desiredTextHeight = baseTextHeight;
        bool atMinimumSize = txt.enableAutoSizing && txt.fontSize <= MinMenuFontSize + 0.5f;

        // Si le texte est déjà à la taille minimale et déborde encore, on étend la zone.
        if (atMinimumSize && (txt.isTextOverflowing || txt.preferredHeight > baseTextHeight))
            desiredTextHeight = Mathf.Max(baseTextHeight, txt.preferredHeight);

        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, desiredTextHeight);

        float targetSlotHeight = Mathf.Max(baseMenuSlotHeights[slotRect], desiredTextHeight + MenuSlotVerticalPadding);
        slotRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetSlotHeight);
    }

    private void PlayTurnStartVoice(CharacterUnit unit)
    {
        PlayTurnVoice(unit, unit?.Data?.turnStartVoiceline);
    }

    private void PlayTurnEndVoice(CharacterUnit unit)
    {
        PlayTurnVoice(unit, unit?.Data?.turnEndVoiceline);
    }

    private void PlayBaseAttackVoice(CharacterUnit unit)
    {
        PlayTurnVoice(unit, unit?.Data?.baseAttackVoiceline);
    }

    private void PlayTurnVoice(CharacterUnit unit, AudioClipSO clip)
    {
        if (unit == null || unit.Data == null || clip == null)
            return;

        AudioManager.Instance?.PlayVoice(clip);
    }

    private void SetButtonAvailability(Transform slot, bool available, bool highlight = false)
    {
        if (slot == null)
            return;

        var txt = slot.GetComponentInChildren<TextMeshProUGUI>();
        var img = slot.childCount > 3 ? slot.GetChild(3).GetComponent<Image>() : null;

        Color color = available ? (highlight ? Color.yellow : Color.white) : Color.gray;
        if (txt != null) txt.color = color;
        if (img != null) img.color = color;
    }

    /// <summary>
    /// Démarre la séquence d'attente liée au menu d'objets.
    /// - Active la Cinemachine dédiée pour donner la priorité à l'angle "Item_Preparing".
    /// - Lance l'animation "Item_Prepare" du lanceur pour un retour visuel clair.
    /// - Joue la timeline (si disponible) afin de conserver les effets annexes prévus.
    /// </summary>
    private void StartItemPreparingTimeline()
    {
        // Évite de relancer la séquence plusieurs fois si l'on reste dans le menu d'objets.
        if (itemMenuTimelineActive)
            return;

        if (currentCharacterUnit == null)
        {
            Debug.LogWarning("[StartItemPreparingTimeline] Impossible de lancer la séquence : aucune unité courante n'est définie.");
            return;
        }

        // Récupère l'Animator enfant du lanceur via l'utilitaire centralisé pour garantir un binding correct.
        Animator casterAnimator = currentCharacterUnit.GetCasterAnimator();

        if (casterAnimator != null)
        {
            // CrossFade permet d'éviter un cut sec lorsque l'on ouvre le menu après une autre animation.
            if (casterAnimator.HasState(0, AnimatorStateItemPrepare))
                casterAnimator.CrossFade(AnimatorStateItemPrepare, 0.1f, 0, 0f);
            else
                casterAnimator.Play("Item_Prepare"); // Fallback si le hash n'est pas disponible dans le contrôleur.
        }
        else
        {
            Debug.LogWarning("[StartItemPreparingTimeline] Aucun Animator trouvé pour le lanceur : l'animation Item_Prepare ne sera pas jouée.");
        }

        // Confie la priorité caméra à la Cinemachine dédiée à l'attente d'objet.
        Transform casterCameraAnchor = currentCharacterUnit != null
            ? currentCharacterUnit.GetDefaultCameraAnchor(CharacterUnit.CameraAnchorPurpose.Caster)
            : null;

        BattleCameraManager.Instance?.ConfigureActionTargets(
            currentCharacterUnit,
            null,
            null,
            casterCameraAnchor,
            null);
        // 🛠️ Important : on force explicitement la Cinemachine du menu d'objets. Auparavant, la timeline
        // d'attente rappelait la caméra "MainMenu" via un rôle générique (MainMenuIdle), ce qui volait la
        // priorité à « CMV_ItemsMenu » juste après le changement d'état. En réutilisant le même pipeline
        // que le système de menus (RequestCamera), on garantit que l'angle spécialisé des objets reste actif
        // tant que le joueur consulte l'inventaire.
        RequestCamera(ItemsMenuCameraName, MenuCameraBlendDuration, MenuCameraBlendStyle);

        // L'objet d'animation sert de point d'ancrage pour la timeline et l'alignement caméra.
        GameObject animGO = casterAnimator != null ? casterAnimator.gameObject : currentCharacterUnit.GetCasterBindingTarget();

        // Même si la timeline est optionnelle, on garde l'alignement pour replacer la BattleCamera sur le lanceur.
        if (BattleTimelineManager.Instance != null && animGO != null)
        {
            BattleTimelineManager.Instance.AlignCameraToTarget(animGO, "BattleCamera");

            // La timeline reste utile pour jouer les éventuels signaux ou effets complémentaires.
            if (itemPreparingTimeline != null)
                BattleTimelineManager.Instance.PlayCasterTimeline(itemPreparingTimeline, currentCharacterUnit, animGO);
        }

        itemMenuTimelineActive = true;
    }

    /// <summary>
    /// Arrête la séquence de préparation d'objet.
    /// - Rend la main à la caméra principale.
    /// - Replace le lanceur sur sa pose d'attente.
    /// - Coupe la timeline d'attente si elle était lancée.
    /// </summary>
    private void StopItemPreparingTimeline()
    {
        if (!itemMenuTimelineActive)
            return;

        // On redonne la priorité à la BattleCamera classique pour la suite des actions.
        BattleCameraManager.Instance?.SwitchToCamera(BattleCameraRole.None);
        BattleCameraManager.Instance?.ClearRigTargets();

        if (currentCharacterUnit != null)
        {
            // Même logique que dans Start : priorité au cache, puis recherche de secours.
            Animator casterAnimator = currentCharacterUnit.GetCasterAnimator();
            if (casterAnimator != null)
            {
                if (casterAnimator.HasState(0, AnimatorStateIdleBattle))
                    casterAnimator.CrossFade(AnimatorStateIdleBattle, 0.1f, 0, 0f);
                else
                    casterAnimator.Play("Idle_Battle");
            }
        }

        // Arrête la timeline d'attente lancée sur le caster si elle existe.
        BattleTimelineManager.Instance?.StopCasterTimeline(currentCharacterUnit);
        itemMenuTimelineActive = false;
    }
    #endregion

    #region Gestion de la navigation dans les menus
    private void HandleTargetNavigation()
    {
        // Si les menus sont verrouillés par la BattleIntro, aucune navigation ne doit être traitée.
        if (battleIntroMenusLocked)
            return;

        bool isSkillTargeting = currentBattleState == BattleState.SquadUnit_TargetSelectionAmongEnemiesForSkill ||
                                currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadForSkill ||
                                (currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad && currentMove != null) ||
                                (currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies && currentMove != null);

        bool isItemTargeting = currentBattleState == BattleState.SquadUnit_TargetSelectionAmongEnemiesForItem ||
                               currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadForItem ||
                               (currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad && currentItem != null) ||
                               (currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies && currentItem != null);

        if (!isSkillTargeting && !isItemTargeting)
            return;

        TargetType type = isSkillTargeting ? currentMove.targetType : currentItemTargetType;

        if (type == TargetType.Self)
        {
            currentTargetCharacter = currentCharacterUnit;
            return;
        }

        bool targetEnemies = type == TargetType.SingleEnemy || type == TargetType.AllEnemies;
        CharacterType requiredType = targetEnemies ? CharacterType.EnemyUnit : CharacterType.SquadUnit;

        // On reconstruit la liste filtrée manuellement pour éviter les allocations
        // liées à l'utilisation de LINQ chaque frame, ce qui générait des pics de GC
        // responsables de chutes de FPS.
        filteredUnits.Clear();
        foreach (var unit in activeCharacterUnits)
        {
            if (unit.characterType == requiredType && unit.currentHP > 0)
            {
                filteredUnits.Add(unit);
            }
        }

        if (filteredUnits.Count == 0)
        {
            return;
        }

        Vector2 input = InputsManager.Instance.playerInputs.Battle.HorizontalNav.ReadValue<Vector2>();
        if (Time.time - lastNavTime < navigationCooldown)
        {
            return;
        }

        int direction = 0;

        if (input.x > 0.5f) direction = 1;
        else if (input.x < -0.5f) direction = -1;

        if (direction == 0) return;

        lastNavTime = Time.time;

        int count = filteredUnits.Count;
        currentTargetIndex = (currentTargetIndex + direction + count) % count;
        CharacterUnit previousTarget = currentTargetCharacter;
        currentTargetCharacter = filteredUnits[currentTargetIndex];

        if (currentTargetCharacter != previousTarget)
        {
            // Joué à chaque bascule de cible pour informer le joueur du changement de focus.
            PlayMenuClip(targetChangeClip);
        }
    }
    #endregion

    #region Gestion de la navigation parmi les unités en combat

    private void HandleTargetCursor()
    {
        // Protection identique pour le curseur de cible : tant que l'introduction se déroule,
        // les éléments d'interface restent complètement inactifs.
        if (battleIntroMenusLocked)
            return;

        bool isSkillTargeting =
            currentBattleState == BattleState.SquadUnit_TargetSelectionAmongEnemiesForSkill ||
            currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadForSkill ||
            (currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad && currentMove != null) ||
            (currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies && currentMove != null);

        bool isItemTargeting =
            currentBattleState == BattleState.SquadUnit_TargetSelectionAmongEnemiesForItem ||
            currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadForItem ||
            (currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad && currentItem != null) ||
            (currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies && currentItem != null);

        if (!(isSkillTargeting || isItemTargeting))
        {
            if (targetCursor != null)
            {
                targetCursor.transform.position = Vector3.zero;
                targetCursor.SetActive(false);
            }
            HideMultiTargetCursors();
            UpdateTargetCursorColor(true);
            DeactivateTargetCursorCinemachine();
            return;
        }

        TargetType type = isSkillTargeting ? currentMove.targetType : currentItemTargetType;

        if (type == TargetType.AllEnemies || type == TargetType.AllAllies || type == TargetType.All)
        {
            targetCursor?.SetActive(false);

            // Filtre manuel des cibles pour éviter les allocations LINQ à chaque frame
            multiTargetUnits.Clear();
            foreach (var unit in activeCharacterUnits)
            {
                if (unit.currentHP <= 0) continue; // On ignore les unités KO

                // On conserve uniquement les types d'unités attendus
                if (type == TargetType.AllEnemies && unit.characterType != CharacterType.EnemyUnit)
                    continue;
                if (type == TargetType.AllAllies && unit.characterType != CharacterType.SquadUnit)
                    continue;

                multiTargetUnits.Add(unit);
            }

            // S'assure qu'il existe assez de curseurs pour toutes les cibles
            EnsureMultiTargetCursors(multiTargetUnits.Count);

            // Positionne chaque curseur sur l'unité correspondante
            for (int i = 0; i < multiTargetCursors.Count; i++)
            {
                if (i < multiTargetUnits.Count)
                {
                    multiTargetCursors[i].SetActive(true);
                    multiTargetCursors[i].transform.position = multiTargetUnits[i].transform.position;
                }
                else
                {
                    multiTargetCursors[i].SetActive(false);
                }
            }

            DeactivateTargetCursorCinemachine();
        }
        else
        {
            HideMultiTargetCursors();

            if (targetCursor != null && currentTargetCharacter != null)
            {
                targetCursor.SetActive(true);

                if (isSkillTargeting && currentMove != null)
                {
                    // Les attaques spéciales utilisent un décalage en fonction
                    // de la position relative et de la distance de lancement.
                    Vector3 offsetDir = currentTargetCharacter.transform.forward;
                    switch (currentMove.relativePosition)
                    {
                        case RelativePosition.Back:
                            offsetDir = -currentTargetCharacter.transform.forward;
                            break;
                        case RelativePosition.Left:
                            offsetDir = -currentTargetCharacter.transform.right;
                            break;
                        case RelativePosition.Right:
                            offsetDir = currentTargetCharacter.transform.right;
                            break;
                    }

                    Vector3 cursorPos = currentTargetCharacter.transform.position +
                                       offsetDir * currentMove.castDistance;
                    targetCursor.transform.position = cursorPos;

                    float distance = Vector3.Distance(currentCharacterUnit.transform.position,
                        currentTargetCharacter.transform.position);
                    float maxReach = currentCharacterUnit.currentRange + currentMove.castDistance;
                    bool inRange = distance <= maxReach;
                    // Vérifie que la position relative est libre avant d'autoriser l'action.
                    bool hasSpace = HasSpaceForMove(currentCharacterUnit, currentTargetCharacter, currentMove);
                    // Vérifie si la cible possède l'altitude adéquate pour ce mouvement.
                    bool altitudeValid = IsTargetAltitudeValid(currentTargetCharacter, currentMove);
                    // La couleur du curseur devient noire si l'une des conditions n'est pas remplie :
                    // distance, espace disponible ou altitude.
                    UpdateTargetCursorColor(inRange && hasSpace && altitudeValid);
                }
                else if (isItemTargeting)
                {
                    // Les items n'utilisent aucun décalage : le curseur se place
                    // directement sur la cible.
                    targetCursor.transform.position = currentTargetCharacter.transform.position;
                    float distance = Vector3.Distance(currentCharacterUnit.transform.position,
                        currentTargetCharacter.transform.position);
                    float maxReach = currentCharacterUnit.currentRange + currentItem.castDistance;
                    bool inRange = distance <= maxReach;
                    UpdateTargetCursorColor(inRange);
                }
                else
                {
                    targetCursor.transform.position = currentTargetCharacter.transform.position;
                    UpdateTargetCursorColor(true);
                }

                UpdateTargetCursorCinemachine(currentTargetCharacter);
            }
        }
    }

    public void HandleTargetSelection(MusicalMoveSO move, bool isBasicAttack = false)
    {
        currentMove = move;
        currentItem = null; // on annule la sélection d'item précédente
        currentMoveIsBasicAttack = isBasicAttack;
        move.targetType = move.defaultTargetType;
        // Détermine s'il est possible de changer de groupe de cibles
        bool canTargetEnemies = move.targetTypes.Contains(TargetType.SingleEnemy)
                               || move.targetTypes.Contains(TargetType.AllEnemies)
                               || move.targetTypes.Contains(TargetType.All);
        bool canTargetAllies = move.targetTypes.Contains(TargetType.SingleAlly)
                              || move.targetTypes.Contains(TargetType.AllAllies)
                              || move.targetTypes.Contains(TargetType.All);
        bool allowGroupSwitch = canTargetEnemies && canTargetAllies;
        // Feedback audio unique pour signifier le passage en mode ciblage.
        PlayMenuClip(targetSelectionClip);
        switch (move.defaultTargetType)
        {
            case TargetType.Self:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadForSkill);
                currentTargetCharacter = currentCharacterUnit;
                break;

            case TargetType.SingleEnemy:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongEnemiesForSkill);
                currentTargetCharacter = activeCharacterUnits
                    .FirstOrDefault(u => u.characterType == CharacterType.EnemyUnit && u.currentHP > 0);
                break;

            case TargetType.AllEnemies:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies);
                currentTargetCharacter = activeCharacterUnits
                    .FirstOrDefault(u => u.characterType == CharacterType.EnemyUnit && u.currentHP > 0);
                break;

            case TargetType.SingleAlly:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadForSkill);
                currentTargetCharacter = activeCharacterUnits
                    .FirstOrDefault(u => u.characterType == CharacterType.SquadUnit && u.currentHP > 0);
                break;

            case TargetType.AllAllies:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad);
                currentTargetCharacter = activeCharacterUnits
                    .FirstOrDefault(u => u.characterType == CharacterType.SquadUnit && u.currentHP > 0);
                break;

            case TargetType.All:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies);
                currentTargetCharacter = activeCharacterUnits.FirstOrDefault(u => u.currentHP > 0);
                break;

            default:
                Debug.LogWarning($"[BattleTurnManager] Type de cible par défaut non géré : {move.defaultTargetType}");
                return;
        }
        currentTargetIndex = 0;

        // Affiche l'instruction adaptée selon qu'on peut ou non changer de groupe
        if (allowGroupSwitch)
            ActionUIDisplayManager.Instance.DisplayInstruction_SelectGroup();
        else
            ActionUIDisplayManager.Instance.DisplayInstruction_SelectTarget();

        // Dès que le joueur bascule en mode ciblage, on synchronise l'Animator
        // du lanceur avec l'animation de préparation définie sur la compétence.
        // Ce feedback immédiat renforce la lisibilité pour les débutants tout en
        // soulignant l'intention tactique du move pour les joueurs chevronnés.
        if (currentCharacterUnit != null)
        {
            currentCharacterUnit.PlayPreparingAnimation(move?.preparingAnimation);
        }
    }

    public void HandleTargetSelection(ItemData item)
    {
        // Contrairement à l'ancien comportement nous ne coupons plus la timeline de préparation ici :
        // cela permet de conserver l'animation « Item_Prepare » (et les éventuels effets annexes)
        // pendant toute la phase de sélection de cible. La timeline sera arrêtée uniquement lorsque
        // le joueur quittera le contexte de l'objet (validation, retour au menu, etc.).
        currentItem = item;
        currentMove = null; // on annule la sélection de compétence précédente
        currentMoveIsBasicAttack = false; // Sélection d'objet : aucune logique spécifique à l'attaque basique.
        currentItemTargetType = item.defaultTargetType;

        bool canTargetEnemies = item.targetTypes.Contains(TargetType.SingleEnemy) ||
                               item.targetTypes.Contains(TargetType.AllEnemies) ||
                               item.targetTypes.Contains(TargetType.All);
        bool canTargetAllies = item.targetTypes.Contains(TargetType.SingleAlly) ||
                              item.targetTypes.Contains(TargetType.AllAllies) ||
                              item.targetTypes.Contains(TargetType.All);
        bool allowGroupSwitch = canTargetEnemies && canTargetAllies;
        // Même signal sonore que pour les compétences afin de rester cohérent.
        PlayMenuClip(targetSelectionClip);

        switch (item.defaultTargetType)
        {
            case TargetType.Self:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadForItem);
                currentTargetCharacter = currentCharacterUnit;
                break;

            case TargetType.SingleEnemy:
                if (allowGroupSwitch)
                    ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies);
                else
                    ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongEnemiesForItem);
                currentTargetCharacter = activeCharacterUnits
                    .FirstOrDefault(u => u.characterType == CharacterType.EnemyUnit && u.currentHP > 0);
                break;

            case TargetType.AllEnemies:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies);
                currentTargetCharacter = activeCharacterUnits
                    .FirstOrDefault(u => u.characterType == CharacterType.EnemyUnit && u.currentHP > 0);
                break;

            case TargetType.SingleAlly:
                if (allowGroupSwitch)
                    ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad);
                else
                    ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadForItem);
                currentTargetCharacter = activeCharacterUnits
                    .FirstOrDefault(u => u.characterType == CharacterType.SquadUnit && u.currentHP > 0);
                break;

            case TargetType.AllAllies:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad);
                currentTargetCharacter = activeCharacterUnits
                    .FirstOrDefault(u => u.characterType == CharacterType.SquadUnit && u.currentHP > 0);
                break;

            case TargetType.All:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies);
                currentTargetCharacter = activeCharacterUnits.FirstOrDefault(u => u.currentHP > 0);
                break;

            default:
                Debug.LogWarning($"[BattleTurnManager] Type de cible par défaut non géré : {item.defaultTargetType}");
                return;
        }
        currentTargetIndex = 0;

        if (allowGroupSwitch)
            ActionUIDisplayManager.Instance.DisplayInstruction_SelectGroup();
        else
            ActionUIDisplayManager.Instance.DisplayInstruction_SelectTarget();

        // Force immédiatement la synchronisation de la Cinemachine orbitale avec la cible présélectionnée.
        // Sans ce rappel explicite, la priorité de « CMV_OrbitAroundUnit » n'était effective qu'après la
        // prochaine Update, ce qui pouvait laisser la caméra précédente active une frame de trop. En
        // centralisant le rafraîchissement ici, on garantit que la caméra se cale instantanément sur
        // « CMVPoint_OrbitAroundUnit » tout en laissant la timeline de préparation continuer à se jouer.
        UpdateTargetCursorCinemachine(currentTargetCharacter);
    }

    /// <summary>
    ///     Prépare la sélection d'une cible pour une attaque de base déclenchée via l'input dédié.
    ///     En pratique, on masque les menus et l'on présélectionne le premier ennemi valide pour
    ///     offrir un flux identique à celui d'une compétence classique.
    /// </summary>
    /// <returns>Retourne <c>true</c> si une cible initiale a été trouvée.</returns>
    public bool TryStartBaseAttackSelection()
    {
        if (!EnsureCurrentCharacterUnitForMenus(nameof(TryStartBaseAttackSelection)))
            return false;

        CharacterUnit caster = currentCharacterUnit;
        if (caster == null)
            return false;

        if (caster.Data == null)
        {
            Debug.LogWarning("[TryStartBaseAttackSelection] CharacterData introuvable pour l'unité courante.");
            return false;
        }

        // Résout l'attaque basique à utiliser pour ce personnage (fallback global compris).
        MusicalMoveSO basicMove = ResolveBasicAttackMove(caster);
        if (basicMove == null)
        {
            ActionUIDisplayManager.Instance?.DisplayInstruction("Attaque basique indisponible");
            return false;
        }

        // Vérifie les coûts et limitations avant de masquer les menus.
        if (caster.GetHarmonicCount(basicMove.consumedHarmonicType) < basicMove.harmonicCost)
        {
            ActionUIDisplayManager.Instance.DisplayInstruction_NotEnoughHarmonics();
            return false;
        }

        if (basicMove.enterAwake &&
            caster.GetHarmonicCount(caster.Data.harmonicType) < caster.Data.awakeHarmonicThreshold)
        {
            ActionUIDisplayManager.Instance.DisplayInstruction_NotEnoughHarmonics();
            return false;
        }

        if (caster.IsMoveOnCooldown(basicMove))
        {
            ActionUIDisplayManager.Instance.DisplayInstruction_MoveOnCooldown();
            return false;
        }

        if (!caster.CanUseMove(basicMove))
        {
            ActionUIDisplayManager.Instance.DisplayInstruction("Limite d'utilisation atteinte");
            return false;
        }

        // Tout est prêt : on masque les menus pour se comporter comme une sélection classique de compétence.
        ToggleMenuContainers(false, false, false);
        HandleTargetSelection(basicMove, isBasicAttack: true);

        if (currentTargetCharacter == null)
        {
            ActionUIDisplayManager.Instance?.DisplayInstruction("Aucun ennemi valide");
            OpenSkillsMenu();
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Valide la sélection de cible et applique les dégâts de l'attaque basique du personnage actif.
    ///     Un message utilisateur est affiché en cas d'échec (hors portée, données manquantes...).
    /// </summary>
    /// <returns><c>true</c> si l'attaque a été exécutée.</returns>
    public bool ConfirmBaseAttack()
    {
        CharacterUnit caster = currentCharacterUnit;
        CharacterUnit target = currentTargetCharacter;

        if (caster == null || target == null)
        {
            Debug.LogWarning("[ConfirmBaseAttack] Lanceur ou cible manquant : réouverture du SkillsMenu.");
            OpenSkillsMenu();
            return false;
        }

        if (caster.Data == null)
        {
            Debug.LogWarning("[ConfirmBaseAttack] CharacterData introuvable sur le lanceur.");
            OpenSkillsMenu();
            return false;
        }

        float distance = Vector3.Distance(caster.transform.position, target.transform.position);
        float maxReach = caster.currentRange;
        if (distance > maxReach)
        {
            ActionUIDisplayManager.Instance?.DisplayInstruction_TargetTooFar();
            return false;
        }

        ChangeBattleState(BattleState.SquadUnit_PerformingBaseAttack);
        ToggleMenuContainers(false, false, false);

        bool success = ExecuteBaseAttack(caster, target, displayErrors: true);
        if (!success)
        {
            OpenSkillsMenu();
            return false;
        }

        // L'attaque basique représente l'action principale du tour : on clôt donc immédiatement celui-ci.
        EndTurn();
        return true;
    }

    /// <summary>
    ///     Calcule et applique les dégâts de l'attaque de base d'une unité.
    /// </summary>
    /// <param name="attacker">Lanceur de l'attaque.</param>
    /// <param name="target">Cible qui reçoit les dégâts.</param>
    /// <param name="displayErrors">Affiche les messages d'erreur utilisateur si nécessaire.</param>
    /// <param name="registerStats">Enregistre les dégâts pour le suivi du tour et des statistiques.</param>
    /// <param name="applyFatigue">Déclenche le système de fatigue associé, utile pour Thalia.</param>
    /// <returns><c>true</c> si l'attaque a été effectuée.</returns>
    public bool ExecuteBaseAttack(CharacterUnit attacker, CharacterUnit target, bool displayErrors,
        bool registerStats = true, bool applyFatigue = true)
    {
        if (attacker == null || target == null)
        {
            if (displayErrors)
                Debug.LogWarning("[ExecuteBaseAttack] Lanceur ou cible manquant.");
            return false;
        }

        if (attacker.Data == null)
        {
            if (displayErrors)
                Debug.LogWarning("[ExecuteBaseAttack] CharacterData absent sur le lanceur.");
            return false;
        }

        if (target.currentHP <= 0f)
        {
            if (displayErrors)
                Debug.LogWarning("[ExecuteBaseAttack] La cible est déjà hors combat.");
            return false;
        }

        MusicalMoveSO basicMove = ResolveBasicAttackMove(attacker);
        if (basicMove == null)
        {
            if (displayErrors)
                Debug.LogWarning("[ExecuteBaseAttack] Aucun MusicalMove d'attaque basique n'est disponible.");
            return false;
        }

        float distance = Vector3.Distance(attacker.transform.position, target.transform.position);
        float maxReach = attacker.currentRange + basicMove.castDistance;
        if (distance > maxReach)
        {
            if (displayErrors)
                ActionUIDisplayManager.Instance?.DisplayInstruction_TargetTooFar();
            return false;
        }

        OrientUnitTowardTarget(attacker, target);

        // La résolution passe désormais par le MusicalMove afin de profiter des timelines,
        // effets secondaires et registres de dégâts déjà implémentés. On transmet toutefois
        // les options historiques (fatigue, statistiques) pour conserver le même contrat public.
        PlayBaseAttackVoice(attacker); // doit précéder la timeline Performing du move
        bool ignoreFatigue = !applyFatigue;
        bool skipDamageRegistration = !registerStats;
        MusicalMoveExecutor.ApplyEffect(basicMove, attacker, target, false, ignoreFatigue, skipDamageRegistration);

        return true;
    }
    #endregion

    #region Gestion des mouvements de la caméra de combat
    /// <summary>
    /// Met à jour le contexte courant pour que <see cref="BattleCameraManager"/> repositionne correctement
    /// les Cinemachine.
    /// </summary>
    private void UpdateCameraContext(CharacterUnit caster, CharacterUnit target)
    {
        var manager = BattleCameraManager.Instance;
        if (manager == null)
            return;

        if (caster != null)
            manager.SetTurnOwner(caster);

        manager.SetCurrentTarget(target);
    }

    /// <summary>
    /// Active une caméra nommée uniquement si elle n'est pas déjà prioritaire.
    /// </summary>
    private void RequestCamera(string cameraName, float blendDuration, CinemachineBlendDefinition.Styles blendStyle)
    {
        var manager = BattleCameraManager.Instance;
        if (manager == null)
            return;

        if (manager.CurrentCinemachineCameraName == cameraName)
            return;

        manager.SwitchToCamera(cameraName, blendDuration, blendStyle);
    }

    /// <summary>
    /// Demande un rôle caméra tout en évitant les transitions redondantes.
    /// </summary>
    private void RequestCamera(BattleCameraRole role, float blendDuration, CinemachineBlendDefinition.Styles blendStyle)
    {
        var manager = BattleCameraManager.Instance;
        if (manager == null)
            return;

        if (role == BattleCameraRole.None && manager.CurrentCinemachineCameraName == null)
            return;

        manager.SwitchToCamera(role, blendDuration, blendStyle);
    }

    public void UpdateCameraBehaviour(BattleState newState)
    {
        var manager = BattleCameraManager.Instance;
        if (manager == null)
            return;

        bool stateChanged = newState != lastCameraEvaluatedState;
        lastCameraEvaluatedState = newState;

        CharacterUnit caster = currentCharacterUnit;
        CharacterUnit target = currentTargetCharacter;

        UpdateCameraContext(caster, target);

        switch (newState)
        {
            case BattleState.SquadUnit_MainMenu:
                if (stateChanged)
                {
                    manager.SetCurrentTarget(null);
                    RequestCamera(MainMenuCameraName, MenuCameraBlendDuration, MenuCameraBlendStyle);
                }
                break;

            case BattleState.SquadUnit_SkillsMenu:
                if (stateChanged)
                {
                    manager.SetCurrentTarget(null);
                    RequestCamera(SkillsMenuCameraName, MenuCameraBlendDuration, MenuCameraBlendStyle);
                }
                break;

            case BattleState.SquadUnit_ItemsMenu:
            case BattleState.SquadUnit_Item_Prepare:
                if (stateChanged)
                {
                    manager.SetCurrentTarget(null);
                    RequestCamera(ItemsMenuCameraName, MenuCameraBlendDuration, MenuCameraBlendStyle);
                }
                break;

            case BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad:
            case BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies:
            case BattleState.SquadUnit_TargetSelectionAmongSquadForSkill:
            case BattleState.SquadUnit_TargetSelectionAmongSquadForItem:
            case BattleState.SquadUnit_TargetSelectionAmongEnemiesForSkill:
            case BattleState.SquadUnit_TargetSelectionAmongEnemiesForItem:
            case BattleState.SquadUnit_TargetSelectionForBaseAttack:
                if (stateChanged)
                {
                    // On active la Cinemachine appropriée en distinguant les cibles d'objet
                    // (vue orbitale) des attaques classiques (vue épaule existante).
                    string cameraName = ResolveTargetSelectionCameraName(newState);
                    RequestCamera(cameraName, ContextCameraBlendDuration, ContextCameraBlendStyle);
                }
                break;

            case BattleState.SquadUnit_PerformingMusicalMove:
            case BattleState.SquadUnit_PerformingBaseAttack:
            case BattleState.SquadUnit_Item_Use:
            case BattleState.EnemyUnit_PerformingMusicalMove:
            case BattleState.EnemyUnit_Item_Use:
                manager.ConfigureActionTargets(caster, target);
                break;

            case BattleState.EnemyUnit_Reflexion:
                if (stateChanged)
                {
                    manager.SetCurrentTarget(target);
                    RequestCamera(MainMenuCameraName, MenuCameraBlendDuration, MenuCameraBlendStyle);
                }
                break;

            case BattleState.EnemyUnit_Item_Prepare:
                if (stateChanged)
                {
                    manager.SetCurrentTarget(null);
                    RequestCamera(ItemsMenuCameraName, MenuCameraBlendDuration, MenuCameraBlendStyle);
                }
                break;

            case BattleState.VictoryScreen_Await:
                if (stateChanged)
                    RequestCamera(BattleCameraRole.Victory, MenuCameraBlendDuration, MenuCameraBlendStyle);
                break;

            default:
                if (stateChanged)
                    RequestCamera(BattleCameraRole.None, ContextCameraBlendDuration, ContextCameraBlendStyle);
                break;
        }
    }

    /// <summary>
    /// Prépare un travelling lent rappelant l'introduction de Clair Obscur avant le tout premier tour joueur.
    /// Cette séquence met en valeur l'unité active via un mouvement sur rail progressif.
    /// </summary>
    /// <param name="unit">Unité qui s'apprête à jouer.</param>
    private void TryLaunchFirstTurnCameraRail(CharacterUnit unit)
    {
        if (hasPlayedFirstTurnCameraRail)
            return; // Le travelling ne doit être joué qu'une seule fois par combat.

        if (unit == null || !unit.Data.isPlayerControlled)
            return; // On ne déclenche l'effet que pour le premier tour du joueur.

        EnsureBattleCamera();
        if (battleCamera == null)
            return; // Impossible d'animer une caméra inexistante.

        Transform anchor = unit.GetCameraAnchor("CMVPoint_MainMenu");
        if (anchor == null)
        {
            Debug.LogWarning("[BattleCameraManager] Aucun point 'CMVPoint_MainMenu' trouvé pour lancer le travelling introductif.");
            hasPlayedFirstTurnCameraRail = true; // On évite de répéter l'avertissement si la configuration est manquante.
            return;
        }

        hasPlayedFirstTurnCameraRail = true;

        Transform camTransform = battleCamera.transform;

        // On dérive la base du rail à partir de l'orientation de l'ancre finale afin de reproduire le mouvement "sur rail".
        Vector3 anchorForward = anchor.forward.sqrMagnitude > 0.0001f
            ? anchor.forward.normalized
            : (unit.transform.forward.sqrMagnitude > 0.0001f ? unit.transform.forward.normalized : Vector3.forward);
        Vector3 anchorRight = Vector3.Cross(Vector3.up, anchorForward).normalized;
        if (anchorRight.sqrMagnitude < 0.0001f)
            anchorRight = Vector3.Cross(anchorForward, Vector3.forward).normalized;
        if (anchorRight.sqrMagnitude < 0.0001f)
            anchorRight = Vector3.right;

        Vector3 computedStart = anchor.position
                                 - anchorForward * firstTurnRailDepartureDistance
                                 + anchorRight * firstTurnRailLateralOffset
                                 + Vector3.up * firstTurnRailHeightOffset;

        // Mélange entre la position actuelle et le point de départ théorique pour éviter un cut trop brutal.
        Vector3 startPos = Vector3.Lerp(computedStart, camTransform.position, Mathf.Clamp01(firstTurnRailCurrentPositionBlend));
        Vector3 focusPoint = unit.transform.position + Vector3.up * firstTurnRailFocusHeight;

        Vector3 directionToEnd = anchor.position - startPos;
        if (directionToEnd.sqrMagnitude < 0.0001f)
            directionToEnd = anchorForward;
        Vector3 startForward = directionToEnd.normalized;
        Vector3 startRight = Vector3.Cross(Vector3.up, startForward).normalized;
        if (startRight.sqrMagnitude < 0.0001f)
            startRight = Vector3.Cross(startForward, Vector3.forward).normalized;
        if (startRight.sqrMagnitude < 0.0001f)
            startRight = Vector3.right;

        firstTurnRailStartPos = startPos;
        firstTurnRailEndPos = anchor.position;
        firstTurnRailControlPointA = startPos
                                     + startForward * Mathf.Max(1f, firstTurnRailDepartureDistance * 0.5f)
                                     + Vector3.up * (firstTurnRailHeightOffset * 0.5f)
                                     + startRight * (firstTurnRailLateralOffset * 0.35f);
        firstTurnRailControlPointB = anchor.position
                                     - anchorForward * firstTurnRailApproachOffset
                                     + anchorRight * firstTurnRailLateralOffset
                                     + Vector3.up * (firstTurnRailHeightOffset * 0.8f);

        firstTurnRailFocusStart = Vector3.Lerp(focusPoint + anchorRight * (firstTurnRailLateralOffset * 0.5f), focusPoint, 0.25f);
        firstTurnRailFocusEnd = focusPoint;

        Vector3 startLook = firstTurnRailFocusStart - startPos;
        if (startLook.sqrMagnitude < 0.0001f)
            startLook = startForward;
        Vector3 endLook = firstTurnRailFocusEnd - firstTurnRailEndPos;
        if (endLook.sqrMagnitude < 0.0001f)
            endLook = anchorForward;

        firstTurnRailStartRot = Quaternion.LookRotation(startLook.normalized, Vector3.up);
        firstTurnRailEndRot = Quaternion.LookRotation(endLook.normalized, Vector3.up);

        camTransform.SetPositionAndRotation(firstTurnRailStartPos, firstTurnRailStartRot);

        firstTurnRailTimer = 0f;
        isFirstTurnRailActive = firstTurnRailDuration > 0.0001f;

        if (!isFirstTurnRailActive)
        {
            // En cas de durée nulle, on se place directement sur l'ancre finale.
            camTransform.SetPositionAndRotation(firstTurnRailEndPos, firstTurnRailEndRot);
        }
    }

    /// <summary>
    /// Met à jour l'interpolation du travelling introductif à chaque frame.
    /// </summary>
    /// <param name="camTransform">Transform de la caméra de combat.</param>
    private void UpdateFirstTurnCameraRail(Transform camTransform)
    {
        if (!isFirstTurnRailActive)
            return;

        if (firstTurnRailDuration <= 0.0001f)
        {
            camTransform.SetPositionAndRotation(firstTurnRailEndPos, firstTurnRailEndRot);
            isFirstTurnRailActive = false;
            return;
        }

        firstTurnRailTimer += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(firstTurnRailTimer / firstTurnRailDuration);
        float easedT = firstTurnRailEase != null ? firstTurnRailEase.Evaluate(normalizedTime) : normalizedTime;

        Vector3 newPos = EvaluateCubicBezier(firstTurnRailStartPos, firstTurnRailControlPointA, firstTurnRailControlPointB, firstTurnRailEndPos, easedT);
        camTransform.position = newPos;

        Vector3 focus = Vector3.Lerp(firstTurnRailFocusStart, firstTurnRailFocusEnd, easedT);
        Vector3 forward = focus - newPos;
        if (forward.sqrMagnitude < 0.0001f)
            forward = firstTurnRailEndRot * Vector3.forward;

        Quaternion targetRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        camTransform.rotation = Quaternion.Slerp(firstTurnRailStartRot, targetRotation, easedT);

        if (normalizedTime >= 0.999f)
        {
            camTransform.SetPositionAndRotation(firstTurnRailEndPos, firstTurnRailEndRot);
            isFirstTurnRailActive = false;
        }
    }

    /// <summary>
    /// Évalue une courbe de Bézier cubique utilisée pour générer le chemin du travelling.
    /// </summary>
    private static Vector3 EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        t = Mathf.Clamp01(t);
        float u = 1f - t;
        float uu = u * u;
        float uuu = uu * u;
        float tt = t * t;
        float ttt = tt * t;

        return uuu * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + ttt * p3;
    }

    private void LateUpdate()
    {
        if (battleCamera == null)
        {
            return;
        }

        var manager = BattleCameraManager.Instance;
        if (manager != null && manager.HasActiveCinemachineCamera)
            return; // Cinemachine contrôle actuellement la vue.

        // Si une Timeline globale ou une cinématique pilote la caméra, on ne la perturbe pas.
        if (TimelineManager.Instance != null && TimelineManager.Instance.IsTimelinePlaying)
            return;

        Transform camTransform = battleCamera.transform;

        if (isFirstTurnRailActive)
        {
            UpdateFirstTurnCameraRail(camTransform);
            return;
        }

        if (itemMenuTimelineActive)
            return;

        bool targetCursorVisible = targetCursor != null && targetCursor.activeInHierarchy;
        if (targetCursorVisible)
        {
            Vector3 toCursor = targetCursor.transform.position - camTransform.position;
            if (toCursor.sqrMagnitude > 0.0001f)
            {
                Quaternion cursorRotation = Quaternion.LookRotation(toCursor.normalized, Vector3.up);
                camTransform.rotation = Quaternion.Slerp(
                    camTransform.rotation,
                    cursorRotation,
                    Time.deltaTime * cameraSmoothSpeed
                );
            }
        }
    }

    #endregion

    #region Méthodes utilitaires

    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent.name == targetName)
            return parent;

        foreach (Transform child in parent)
        {
            Transform result = FindChildRecursive(child, targetName);
            if (result != null)
                return result;
        }
        return null;
    }

    /// <summary>
    /// Tente de récupérer l'ancre "Camera_TargetedPoint" d'une unité afin
    /// d'offrir un point de vue stable durant la sélection de cible.
    /// </summary>
    /// <param name="unit">Unité pré-ciblée actuellement suivie par l'UI.</param>
    /// <returns>
    /// L'ancre dédiée si elle existe, sinon le transform racine de l'unité
    /// pour garantir un focus caméra cohérent.
    /// </returns>
    private Transform GetTargetedAnchorOrFallback(CharacterUnit unit)
    {
        if (unit == null)
        {
            // Sécurité : sans unité, on ne peut pas proposer d'ancre valide.
            return null;
        }

        // Recherche prioritaire de l'ancre spécifique prévue par les artistes.
        // Lors d'un ciblage d'objet, on tente d'abord de récupérer le point orbital dédié
        // afin que la Cinemachine se cale exactement sur "CMVPoint_OrbitAroundUnit".
        if (IsItemTargetSelectionState(currentBattleState))
        {
            Transform orbitAnchor = unit.GetCameraAnchor("CMVPoint_OrbitAroundUnit");
            if (orbitAnchor != null)
                return orbitAnchor;
        }

        Transform targetedAnchor = unit.GetCameraAnchor("CMVPoint_TargetReaction");
        if (targetedAnchor == null)
            targetedAnchor = FindChildRecursive(unit.transform, "Camera_TargetedPoint");
        if (targetedAnchor != null)
        {
            return targetedAnchor;
        }

        // Fallback : on renvoie la racine de l'unité pour conserver un focus
        // immédiat. Sans cela, la caméra resterait sur sa position précédente
        // jusqu'à ce que le joueur navigue manuellement entre les cibles.
        return unit.transform;
    }

    private Sprite GetInputSprite(int index)
    {
        return index switch
        {
            0 => inputSprite1,
            1 => inputSprite2,
            2 => inputSprite3,
            _ => null,
        };
    }

    /// <summary>
    /// Calcule dynamiquement une position de caméra afin que le lanceur et la cible
    /// soient visibles simultanément avec une marge de sécurité.
    /// </summary>
    /// <param name="position">Position désirée pour la caméra.</param>
    /// <param name="rotation">Rotation correspondante à appliquer.</param>
    /// <param name="caster">Transform du lanceur de l'action.</param>
    /// <param name="target">Transform de la cible sélectionnée.</param>
    private void ComputeTargetSelectionCamera(out Vector3 position, out Quaternion rotation, Transform caster, Transform target)
    {
        if (caster == null || target == null)
        {
            // Sécurité : on garde la position actuelle si une référence manque
            position = battleCamera.transform.position;
            rotation = battleCamera.transform.rotation;
            return;
        }

        // Cas particulier : le lanceur se cible lui-même
        if (caster == target)
        {
            // On recule la caméra dans l'axe opposé au regard pour ne pas être collé
            float selfDistance = 3f;        // Distance d'éloignement par défaut
            Vector3 backward = -target.forward * selfDistance;
            Vector3 lateralOffset = target.right * 1.5f; // Décalage léger pour éviter d'être pile dessus
            Vector3 heightOffset = Vector3.up * 2f;       // Légère prise de hauteur

            position = target.position + backward + lateralOffset + heightOffset;
            rotation = Quaternion.LookRotation(target.position - position, Vector3.up);
            return;
        }

        // Point central entre le lanceur et la cible
        Vector3 mid = (caster.position + target.position) * 0.5f;
        Vector3 toTarget = target.position - caster.position;

        // Direction latérale perpendiculaire pour éviter que l'un ne masque l'autre
        Vector3 side = Vector3.Cross(Vector3.up, toTarget);
        if (side == Vector3.zero)
            side = Vector3.right; // Valeur par défaut si les deux sont alignés verticalement
        side.Normalize();

        float distance = toTarget.magnitude;
        float distMultiplier = 1.2f;    // Marges pour ne pas coller aux bords
        float heightMultiplier = 0.5f;  // Légère hauteur pour la lisibilité

        // Position désirée à une distance proportionnelle à l'écart entre les deux
        position = mid + side * distance * distMultiplier + Vector3.up * Mathf.Clamp(distance * heightMultiplier, 1f, 5f);

        // Orientation vers le point central
        rotation = Quaternion.LookRotation(mid - position, Vector3.up);
    }

    /// <summary>
    /// Joue un clip lié aux menus de combat via l'AudioManager.
    /// Les garde-fous évitent toute exception lorsque l'audio n'est pas encore initialisé
    /// (ex : scènes de test sans gestionnaire global).
    /// </summary>
    /// <param name="clip">Clip à jouer immédiatement.</param>
    private void PlayMenuClip(AudioClipSO clip)
    {
        if (clip == null)
            return; // Aucun son configuré pour cet évènement.

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager == null)
            return; // Le gestionnaire peut être absent dans certaines scènes d'édition.

        audioManager.PlaySfx(clip);
    }

    /// <summary>
    /// Déclenche l'éventuel clip personnalisé associé au personnage actuellement sélectionné.
    /// Chaque unité peut fournir son propre son via <see cref="CharacterData.menuSelectionClip"/> ;
    /// un fallback global est utilisé si aucun clip n'est défini pour garantir un feedback sonore.
    /// </summary>
    /// <param name="unit">Unité qui vient d'être mise en avant dans les menus.</param>
    private void PlayCharacterSelectionClip(CharacterUnit unit)
    {
        if (unit == null)
            return; // Sécurité : aucun personnage actif, donc aucun son à jouer.

        CharacterData data = unit.Data;
        AudioClipSO clipToPlay = data != null && data.menuSelectionClip != null
            ? data.menuSelectionClip
            : defaultCharacterSelectionClip;

        PlayMenuClip(clipToPlay);
    }

    public void ChangeBattleState(BattleState newState)
    {
        currentBattleState = newState;
        Debug.Log("Nouvel état de combat: " + newState);
        UpdateCameraBehaviour(newState);
    }

    /// <summary>
    /// Rend public le changement d'unité active pour permettre aux autres systèmes (UI, caméras, timelines)
    /// de réagir immédiatement à la nouvelle sélection.
    /// </summary>
    public void ChangeCurrentCharacterUnit(CharacterUnit newCurrentCharacterUnit)
    {
        bool unitChanged = currentCharacterUnit != newCurrentCharacterUnit;

        if (!unitChanged)
            return; // Aucun changement : on évite les répétitions sonores inutiles.

        currentCharacterUnit = newCurrentCharacterUnit;

        // Informe immédiatement le statut Presto pour savoir si le lanceur vient de rejouer.
        PrestoForcedAttackSystem.HandleActiveUnitChanged(currentCharacterUnit);

        if (currentCharacterUnit == null)
            return; // Parfois utilisé lors des resets de combat : aucune unité active.

        PlayCharacterSelectionClip(currentCharacterUnit);
    }

    private void EnsureBattleCamera()
    {
        if (battleCamera == null)
        {
            battleCamera = GameObject.FindGameObjectWithTag("BattleCamera");
        }
    }

    /// <summary>
    /// Récupère le canvas utilisé par la caméra de combat afin d'y ancrer les éléments de feedback.
    /// On privilégie une référence configurée dans l'inspecteur mais on prévoit un fallback automatique
    /// pour préserver la compatibilité avec les scènes existantes.
    /// </summary>
    private void EnsureBattleCameraCanvas()
    {
        if (battleCameraCanvasTransform != null)
        {
            return; // Une référence manuelle a peut-être été fournie dans l'inspecteur.
        }

        // Première tentative : on exploite la hiérarchie de la BattleCamera si elle est connue.
        if (battleCamera == null)
        {
            EnsureBattleCamera();
        }

        if (battleCamera != null)
        {
            Canvas[] canvases = battleCamera.GetComponentsInChildren<Canvas>(true);
            foreach (Canvas canvas in canvases)
            {
                if (canvas != null && canvas.name == "BattleCameraCanvas")
                {
                    battleCameraCanvasTransform = canvas.transform;
                    return;
                }
            }
        }

        // Deuxième tentative : recherche directe par nom, adaptée aux scènes déjà configurées.
        GameObject canvasGO = GameObject.Find("BattleCameraCanvas");
        if (canvasGO != null)
        {
            battleCameraCanvasTransform = canvasGO.transform;
            return;
        }

        Debug.LogWarning("[NewBattleManager] Impossible de localiser 'BattleCameraCanvas'. L'indicateur de portée ne sera pas instancié.");
    }

    void EnsureTargetCursor()
    {
        // Si un curseur existe déjà et n'a pas été détruit par ResetBattleInfos(),
        // on évite d'en instancier un doublon. Sans cette vérification, un clone
        // supplémentaire apparaissait au lancement du jeu puis du combat, car
        // EnsureTargetCursor() est appelé dans Start() puis à nouveau dans StartBattle().
        if (targetCursor != null)
        {
            return;
        }

        if (targetCursorPrefab != null)
        {
            // Instanciation « lazy » : on ne crée le visuel que lorsque c'est nécessaire.
            // L'objet est immédiatement désactivé car il ne sera affiché qu'une fois
            // la sélection de cible active.
            targetCursor = Instantiate(targetCursorPrefab, transform.position, Quaternion.identity);
            targetCursor.SetActive(false);
        }
    }

    void EnsureMultiTargetCursors(int count)
    {
        if (targetCursorPrefab == null) return;

        while (multiTargetCursors.Count < count)
        {
            GameObject cursor = Instantiate(targetCursorPrefab, transform.position, Quaternion.identity);
            cursor.SetActive(false);
            multiTargetCursors.Add(cursor);
        }

        for (int i = multiTargetCursors.Count - 1; i >= count; i--)
        {
            Destroy(multiTargetCursors[i]);
            multiTargetCursors.RemoveAt(i);
        }
    }

    void HideMultiTargetCursors()
    {
        foreach (var cursor in multiTargetCursors)
        {
            if (cursor != null)
            {
                cursor.SetActive(false);
            }
        }
    }

    void UpdateTargetCursorColor(bool inRange)
    {
        if (targetCursor == null) return;

        ParticleSystem[] systems = targetCursor.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in systems)
        {
            var main = ps.main;
            // Blanc lorsque la cible est valide, bleu lorsqu'elle ne peut être touchée
            // (hors de portée, mauvaise altitude, etc.).
            main.startColor = inRange ? Color.white : Color.blue;
        }
    }

    /// <summary>
    /// Libère les informations caméra dédiées au suivi du curseur de cible.
    /// On remet ainsi la main au gestionnaire général pour éviter que la
    /// Cinemachine ne reste verrouillée sur une cible périmée.
    /// </summary>
    private void DeactivateTargetCursorCinemachine()
    {
        var manager = BattleCameraManager.Instance;
        if (manager == null)
            return; // Aucun gestionnaire actif : aucune action requise.

        // Si aucune Cinemachine de ciblage n'était configurée, on évite un
        // rafraîchissement coûteux et on sort immédiatement.
        if (!isTargetCursorCinemachineActive && lastTargetCursorCameraTarget == null && lastTargetCursorAnchor == null)
            return;

        isTargetCursorCinemachineActive = false;
        lastTargetCursorCameraTarget = null;
        lastTargetCursorAnchor = null;

        // ConfigureActionTargets remettra la cible à null tout en conservant
        // le lanceur actuel comme référence. Cela garantit que les caméras de
        // menu continuent d'utiliser l'ancre du joueur actif.
        if (currentCharacterUnit != null)
            manager.ConfigureActionTargets(currentCharacterUnit, null);
        else
            manager.ClearRigTargets();

        // On notifie également explicitement que plus aucune cible n'est suivie
        // afin que les caméras dépendantes du LookAt puissent relâcher leur focus.
        manager.SetCurrentTarget(null);
    }

    /// <summary>
    /// Informe la <see cref="BattleCameraManager"/> de la cible suivie par le
    /// curseur afin que la Cinemachine adaptée prenne le relais et encadre la
    /// scène. Les multiples garde-fous évitent les appels redondants coûteux.
    /// </summary>
    /// <param name="target">Unité actuellement survolée par le curseur.</param>
    private void UpdateTargetCursorCinemachine(CharacterUnit target)
    {
        var manager = BattleCameraManager.Instance;
        if (manager == null)
            return; // Sans gestionnaire, on ne tente aucune synchronisation caméra.

        if (target == null)
        {
            // En absence de cible (ciblage multiple ou sortie du mode de sélection),
            // on relâche immédiatement la caméra de ciblage.
            DeactivateTargetCursorCinemachine();
            return;
        }

        Transform targetAnchor = GetTargetedAnchorOrFallback(target);

        // Si la Cinemachine suit déjà cette cible et que l'ancre n'a pas changé,
        // il est inutile de déclencher un nouveau rafraîchissement.
        // On identifie la caméra de ciblage qui devrait être active (item ou compétence).
        string expectedCameraName = ResolveTargetSelectionCameraName(currentBattleState);

        bool alreadyTracking = isTargetCursorCinemachineActive
                               && lastTargetCursorCameraTarget == target
                               && lastTargetCursorAnchor == targetAnchor
                               && string.Equals(manager.CurrentCinemachineCameraName, expectedCameraName, System.StringComparison.OrdinalIgnoreCase);
        if (alreadyTracking)
            return;

        lastTargetCursorCameraTarget = target;
        lastTargetCursorAnchor = targetAnchor;
        isTargetCursorCinemachineActive = true;

        // On délègue la configuration détaillée (caster, cible, éventuels overrides)
        // au gestionnaire central pour que toutes les Cinemachine restent cohérentes.
        manager.ConfigureActionTargets(
            currentCharacterUnit,
            target,
            null,
            null,
            targetAnchor);

        // Mise à jour de la cible suivie pour les caméras qui n'utilisent pas
        // les overrides directs mais se basent sur le contexte courant.
        manager.SetCurrentTarget(target);

        // On s'assure enfin que la Cinemachine dédiée à la sélection est prioritaire
        // durant les phases de ciblage afin d'offrir un retour visuel clair.
        if (IsTargetSelectionState(currentBattleState))
        {
            RequestCamera(expectedCameraName, ContextCameraBlendDuration, ContextCameraBlendStyle);
        }
    }

    /// <summary>
    /// Indique si l'état donné correspond à une phase de sélection de cible.
    /// Permet de conditionner certains comportements (orientation, caméra...).
    /// </summary>
    private bool IsTargetSelectionState(BattleState state)
    {
        return state == BattleState.SquadUnit_TargetSelectionAmongEnemiesForSkill
               || state == BattleState.SquadUnit_TargetSelectionAmongEnemiesForItem
               || state == BattleState.SquadUnit_TargetSelectionAmongSquadForSkill
               || state == BattleState.SquadUnit_TargetSelectionAmongSquadForItem
               || state == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad
               || state == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies;
    }

    /// <summary>
    /// Détermine si la sélection de cible actuelle est déclenchée par un objet.
    /// Cette information permet de prioriser la Cinemachine orbitale adaptée.
    /// </summary>
    private bool IsItemTargetSelectionState(BattleState state)
    {
        return state == BattleState.SquadUnit_TargetSelectionAmongEnemiesForItem
               || state == BattleState.SquadUnit_TargetSelectionAmongSquadForItem
               || ((state == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad
                    || state == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies)
                   && currentItem != null
                   && currentMove == null);
    }

    /// <summary>
    /// Choisit dynamiquement la Cinemachine de ciblage à activer en fonction du contexte.
    /// </summary>
    private string ResolveTargetSelectionCameraName(BattleState state)
    {
        return IsItemTargetSelectionState(state)
            ? ItemTargetSelectionCameraName
            : TargetSelectionCameraName;
    }

    public void SetCurrentTargetToFirst(CharacterType type)
    {
        currentTargetIndex = 0;
        currentTargetCharacter = activeCharacterUnits
            .FirstOrDefault(u => u.characterType == type && u.currentHP > 0);
    }

    public void ResetBattleInfos()
    {
        // Réinitialise l’état du combat
        ChangeBattleState(BattleState.None);

        // Supprime toute trace d'un éventuel verrou d'introduction pour la prochaine rencontre.
        battleIntroMenusLocked = false;

        // 🧹 Si la restriction d'entrées est encore active (ex : interruption prématurée),
        //     on s'assure de rétablir le mapping complet avant de quitter le combat.
        InputsManager.Instance?.RestoreBattleInputsAfterIntro();

        // Nettoie les références
        currentCharacterUnit = null;
        unitsInBattle.Clear();
        activeCharacterUnits.Clear();

        rewardItems.Clear();
        rewardXP = 0;

        battleStartTime = 0f;
        maxTurnDamage = 0;
        currentTurnDamage = 0;
        mvpUnit = null;

        // Réinitialise les informations liées au travelling d'introduction pour la prochaine confrontation.
        hasPlayedFirstTurnCameraRail = false;
        isFirstTurnRailActive = false;
        firstTurnRailTimer = 0f;

        // Réinitialisation de l'interface de timeline via le gestionnaire dédié
        BattleTimelineUIManager.Instance?.Clear();

        // Réinitialise les timelines et l'issue du combat
        victoryTimeline = null;
        defeatTimeline = null;
        lastBattleOutcome = BattleOutcome.None;
        respawnAtCheckpointOnExit = false;

        // Réinitialise le curseur cible si existant
        if (targetCursor != null)
        {
            Destroy(targetCursor);
            targetCursor = null;
        }
    }
    #endregion
}
