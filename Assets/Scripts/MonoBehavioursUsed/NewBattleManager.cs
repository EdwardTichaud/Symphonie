using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Playables;
using UnityEngine.Timeline;

#region TargetType
public enum TargetType
{
    Self,
    SingleEnemy,
    AllEnemies,
    SingleAlly,
    AllAllies,
    All
}
#endregion

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
    SquadUnit_PerformingMusicalMove,
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

public class NewBattleManager : MonoBehaviour
{
    public static NewBattleManager Instance { get; private set; }

    [Header("État du combat")]
    public BattleState currentBattleState;

    [Header("Apparition des SquadUnits")]
    public GameObject squadUnitRay;
    private List<Transform> playerSpawnPoints = new List<Transform>();

    [Header("Apparition des ennemis")]
    public GameObject enemyUnitRay;
    private List<Transform> enemySpawnPoints = new List<Transform>();
    public List<CharacterData> enemyTemplates = new List<CharacterData>();

    [Header("Listes des unités en combat en fonction de leur état")]
    public List<CharacterUnit> unitsInBattle = new(); // Toutes les unités du combat quelque soit leur état
    public List<CharacterUnit> activeCharacterUnits = new List<CharacterUnit>(); // Unités actives en combat (HP > 0)

    [Header("Début de combat")]
    [SerializeField] private GameObject firstStrikeEffect;

    // Paramètres du ralentissement appliqué lors de l'introduction.
    [Tooltip("Facteur de ralentissement au tout début du combat.")]
    public float equipSlowMotionScale = 1f;

    [Header("Fin de combat")]
    public GameObject victoryScreen;
    public GameObject gameOverScreen;
    public RenderTexture VictoryScreenImage;
    public RenderTexture GameOverScreenImage;

    [Header("Défaite")]
    [Tooltip("Si vrai, une défaite renvoie directement au menu principal.")]
    public bool gameOverOnDefeat = false;

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

    /// <summary>Rôle caméra utilisé lors de l'attente de sélection d'objet.</summary>
    private const BattleCameraRole ItemPreparingCameraRole = BattleCameraRole.MainMenuIdle;
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
    private List<CharacterUnit> filteredUnits = new();
    // Liste temporaire réutilisée pour éviter des allocations lors du ciblage multiple
    private readonly List<CharacterUnit> multiTargetUnits = new();
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
        }
    }
    //-------------------------------------------------------------------------------------

    // Caméra
    [Header("Caméra de combat")]
    // On mémorise l'objet caméra pour éviter de le rechercher à chaque frame
    private GameObject battleCamera;
    public float cameraSmoothSpeed = 5f;
    [HideInInspector] public Transform desiredTransform;
    private Vector3 desiredPosition;
    private Quaternion desiredRotation;
    public bool isFollowingCurrentTarget = false;
    // Indique si la caméra doit regarder le lanceur lors de la sélection de cible
    private bool lookAtCasterDuringTargetSelection = false;
    // Indique si la caméra doit se placer sur la cible et regarder le lanceur (tour ennemi)
    private bool lookAtCasterFromTargetPoint = false;
    private bool isOrbiting = false;
    private float currentOrbitAngle;
    private Transform orbitCenter;

    // Compétences et items disponibles pour l’unité qui joue
    // Garder en public
    [HideInInspector] public List<MusicalMoveSO> skillChoices = new List<MusicalMoveSO>();
    // Mouvement spécial actuel (affiché dans le 4e slot du SkillsMenu)
    [HideInInspector] public MusicalMoveSO specialMoveChoice;
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

    // Menus personnalisés pour l’unité qui joue
    public GameObject currentMainMenuContainer;
    public List<Transform> currentMainMenuSlots;

    public GameObject currentSkillsMenuContainer;
    public List<Transform> currentSkillsMenuSlots;

    public GameObject currentItemsMenuContainer;
    public List<Transform> currentItemsMenuSlots;

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
    /// Gère les sélections de cible pendant le combat.
    /// </summary>
    private void Update()
    {
        HandleTargetCursor();
        HandleTargetNavigation();
    }
    #endregion

    #region Initialisation du champs de bataille
    public void SpawnAll()
    {
        // Nettoie les références nulles pouvant persister après un précédent combat
        activeCharacterUnits.RemoveAll(u => u == null);
        unitsInBattle.RemoveAll(u => u == null);

        // Si des unités valides existent encore, on évite de doubler le spawn
        if (activeCharacterUnits.Count > 0 || unitsInBattle.Count > 0)
        {
            Debug.LogWarning("[NewBattleManager] SpawnAll déjà exécuté ou unités déjà présentes.");
            return;
        }

        // S'assure que les listes sont complètement vides avant d'instancier de nouvelles unités
        activeCharacterUnits.Clear();
        unitsInBattle.Clear();
        SpawnSquadUnits();
        SpawnEnemies();
    }

    private void SpawnSquadUnits()
    {
        playerSpawnPoints.Clear();
        var playerSpawnRoot = GameObject.FindGameObjectWithTag("PlayerSpawn").transform;

        for (int i = 0; i < playerSpawnRoot.childCount; i++)
        {
            var child = playerSpawnRoot.GetChild(i);
            if (child != null)
                playerSpawnPoints.Add(child);
        }

        // Seuls les trois premiers membres de la squad peuvent participer au combat
        var squad = SquadManager.Instance != null ? SquadManager.Instance.SquadCharacters : new List<CharacterData>();
        int maxSquadMembers = Mathf.Min(3, squad.Count);
        for (int i = 0; i < maxSquadMembers && i < playerSpawnPoints.Count; i++)
        {
            var pc = squad[i];
            var spawnPoint = playerSpawnPoints[i];

            if (pc.characterBattleModel == null)
            {
                Debug.LogWarning($"[SpawnPlayers] Aucun modèle défini pour {pc.characterName}, annulation du spawn.");
                continue;
            }

            // 🧍 Apparition immédiate de l'unité à sa position de combat.
            var unitGO = Instantiate(pc.characterBattleModel, spawnPoint.position, Quaternion.identity);
            unitGO.transform.SetParent(spawnPoint, worldPositionStays: true);
            unitGO.name = $"SquadUnit_{i}";

            // ✅ Génère l'effet visuel du rayon directement à l'emplacement final.
            if (squadUnitRay != null)
                Instantiate(squadUnitRay, spawnPoint.position, Quaternion.identity);

            var unit = unitGO.GetComponent<CharacterUnit>();
            unit.Initialize(pc);
            unitsInBattle.Add(unit);
        }
    }

    private void SpawnEnemies()
    {
        enemySpawnPoints.Clear();
        var enemySpawnRoot = GameObject.FindGameObjectWithTag("EnemySpawn").transform;

        for (int i = 0; i < enemySpawnRoot.childCount; i++)
        {
            var child = enemySpawnRoot.GetChild(i);
            if (child != null)
                enemySpawnPoints.Add(child);
        }

        for (int i = 0; i < enemyTemplates.Count && i < enemySpawnPoints.Count; i++)
        {
            var enemyData = Instantiate(enemyTemplates[i]);
            var spawnPoint = enemySpawnPoints[i];

            if (enemyData.characterBattleModel == null)
            {
                Debug.LogWarning($"[SpawnEnemies] Aucun modèle défini pour {enemyData.characterName}, annulation du spawn.");
                continue;
            }

            // 🧍 Apparition immédiate de l'ennemi à sa position de combat.
            var unitGO = Instantiate(enemyData.characterBattleModel, spawnPoint.position, Quaternion.Euler(0f, 180f, 0f));
            unitGO.transform.SetParent(spawnPoint, worldPositionStays: true);
            unitGO.name = $"EnemyUnit_{i}";

            // ✅ Génère l'effet du rayon directement à l'emplacement final.
            if (enemyUnitRay != null)
                Instantiate(enemyUnitRay, spawnPoint.position, Quaternion.identity);

            var eu = unitGO.GetComponent<CharacterUnit>();
            eu.Initialize(enemyData);
            unitsInBattle.Add(eu);
        }
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

        // Attend que toutes les timelines soient terminées sans imposer une frame
        // supplémentaire après la fin de la dernière animation.
        bool timelinesStillPlaying = true;
        while (timelinesStillPlaying)
        {
            timelinesStillPlaying = false;

            foreach (var director in activeDirectors)
            {
                if (director != null && director.state == PlayState.Playing)
                {
                    timelinesStillPlaying = true;
                    break; // Au moins une timeline est encore en cours, on attend la prochaine frame.
                }
            }

            if (timelinesStillPlaying)
                yield return null; // Patiente une frame avant de re-vérifier.
        }

        // Restaure les valeurs temporelles initiales une fois les timelines terminées
        Time.timeScale = initialTimeScale;
        Time.fixedDeltaTime = initialFixedDelta;

        // Patiente encore une seconde en temps réel après la fin des timelines
        // d'introduction pour laisser le temps au joueur d'apprécier la mise en scène.
        yield return new WaitForSecondsRealtime(0f);

        // La gestion du changement de caméra est réalisée en dehors de cette
        // coroutine afin de laisser la main à l'appelant sur la transition.
    }

    /// <summary>
    /// Gère la caméra d'introduction en priorité orbitale, attend la fin des
    /// déplacements puis lance les timelines d'introduction des unités.
    /// </summary>
    private IEnumerator PlayIntroCameraSequence()
    {
        var cameraManager = BattleCameraManager.Instance;

        // On s'assure que l'état du travelling est vierge au lancement d'un nouveau combat.
        cameraManager?.ResetIntroRail();

        // Sélectionne l'unité joueur supposée agir en premier pour préparer le travelling.
        CharacterUnit firstPlayerToAct = ReturnFirstStrikeCharacter();
        bool introRailLaunched = false;

        if (cameraManager != null && firstPlayerToAct != null)
        {
            // Le rig récupère les bonnes hauteurs/ancres du joueur pour la mise en scène.
            cameraManager.ConfigureActionTargets(firstPlayerToAct, null);
            introRailLaunched = cameraManager.TryPlayIntroRail(firstPlayerToAct);
        }

        if (!introRailLaunched)
        {
            // Fallback : plan large classique si la caméra dédiée n'est pas disponible.
            cameraManager?.SwitchToCamera(BattleCameraRole.WideEstablish, 0f);
        }

        // 🎬 Lance les timelines d'introduction en mode ralenti et attend leur terminaison.
        yield return PlayIntroTimelinesWithSlowTime();

        if (introRailLaunched)
        {
            // On attend la fin du travelling avant de redonner la main à la caméra standard.
            while (cameraManager != null && cameraManager.IsIntroRailRunning)
                yield return null;
        }

        // 📷 Retourne ensuite sur la caméra de combat standard avec un léger fondu.
        cameraManager?.SwitchToCamera(BattleCameraRole.None, 0.5f);
    }
    #endregion

    #region Démarrage du combat
    public IEnumerator StartBattle()
    {
        Debug.Log("[BattleTurnManager] Démarrage du combat");

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

        //6 Intro Camera
        // Lance la séquence d'introduction des caméras avant de débuter les tours.
        yield return PlayIntroCameraSequence();

        //7 Démarre la boucle de tours
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

    #region Gestion des tours de combat
    private CharacterUnit CalculateNextUnit()
    {
        while (true)
        {
            foreach (var unit in activeCharacterUnits)
            {
                unit.currentATB += unit.currentInitiative;
                if (unit.currentATB >= ATB_THRESHOLD)
                    return unit;
            }
        }
    }

    public void StartSquadUnitTurn(CharacterUnit characterUnit)
    {
        Debug.Log("Initialisation du menu de combat avec l'unité : " + characterUnit.Data.characterName);

        currentTurnDamage = 0;

        if (currentBattleState == BattleState.None
            || currentBattleState == BattleState.VictoryScreen_Await
            || currentBattleState == BattleState.VictoryScreen_CanContinue
            || currentBattleState == BattleState.GameOverScreen_Await
            || currentBattleState == BattleState.GameOverScreen_CanContinue)
        {
            return;
        }

        if (currentCharacterUnit != null)
            ToggleMenuContainers(false, false, false);

        // Affiche l'interface principale au premier tour du joueur
        BattleTransitionManager.Instance?.ShowBattleUIIfNeeded();
        // S'assure que la timeline devienne visible dès qu'un tour joueur commence
        BattleTimelineUIManager.Instance?.SetVisible(true);

        ChangeCurrentCharacterUnit(characterUnit);

        // Gain automatique d'une harmonique en début de tour
        characterUnit.AddHarmonic(characterUnit.Data.harmonicType);
        // Affiche un popup visuel pour indiquer le gain
        AddHarmonicPopupManager.Instance?.ShowAddHarmonic(characterUnit.transform, 1);

        if (characterUnit.Data.characterType == CharacterType.SquadUnit)
            ChangeBattleState(BattleState.SquadUnit_MainMenu);
        else if (characterUnit.Data.characterType == CharacterType.EnemyUnit)
            ChangeBattleState(BattleState.EnemyUnit_Reflexion);

        SetupCurrentUnitMenus(); // prépare les panels de l’unité
        ShowMainMenu(); // montre le menu principal
        if (characterUnit.Data.isPlayerControlled)
        {
            SetupCurrentUnitMenus(); // prépare les panels de l’unité
            ShowMainMenu(); // montre le menu principal
        }
        else
        {
            ToggleMenuContainers(false, false, false); // s'assure que les menus sont cachés
        }

        InputsManager.Instance.playerInputs.Battle.Enable();
        OrientAllUnitsTowardClosestOpponent();

        // Affiche l'interface de passage de tour si elle est disponible.
        PassTurnUI.Instance?.Show();
    }

    private IEnumerator ExecuteTurn(CharacterUnit unit)
    {
        if (currentBattleState != BattleState.VictoryScreen_Await && currentBattleState != BattleState.VictoryScreen_CanContinue && currentBattleState != BattleState.GameOverScreen_Await && currentBattleState != BattleState.GameOverScreen_CanContinue)
        {
            if (unit.TryGetComponent<SleepStatus>(out var sleep) && sleep.IsAsleep && unit.Data.gameplayType != GameplayType.Fatigue)
            {
                EndTurn();
                yield break;
            }
            if (unit.TryGetComponent<FatigueSystem>(out var fatigue) && fatigue.IsAsleep && unit.Data.gameplayType != GameplayType.Fatigue)
            {
                EndTurn();
                yield break;
            }

            unit.ReduceCooldowns();
            // Réinitialisation des limites par tour
            unit.ResetTurnMoveUsage();
            InventoryManager.Instance?.ResetTurnItemUsage();
            isTurnResolving = true;

            // 1) On stocke l’unité qui jouait juste avant (champ de classe)
            CharacterUnit oldUnit = previousUnit;

            // 2) Mise à jour de l’unité courante
            currentCharacterUnit = unit;
            // Mise à jour de la timeline visuelle via le gestionnaire centralisé
            BattleTimelineUIManager.Instance?.Refresh(unit);

            ChangeBattleState(BattleState.NewTurn);

            Debug.Log($"[BattleTurnManager] Tour de {unit.name} (ATB: {unit.currentATB})");
            OrientAllUnitsTowardClosestOpponent();
            unit.animator.Play("Idle_Battle");

            // Petite pause avant l'exécution du tour, indépendante du timeScale
            yield return new WaitForSecondsRealtime(0.5f);

            if (unit.Data.isPlayerControlled)
            {
                StartSquadUnitTurn(unit);
                yield return new WaitUntil(() => !isTurnResolving);
            }
            else
            {
                yield return EnemyTurnWithQTE(unit);
                EndTurn();
            }

            // 8) On mémorise unit comme précédente pour le prochain tour
            previousUnit = unit;
        }
        else
        {
            yield break;
        }
    }

    /// <summary>
    /// Retire une unité de la timeline visuelle lorsqu'elle quitte le combat.
    /// </summary>
    /// <param name="deadUnit">L'unité vaincue.</param>
    public void RemoveFromTimeline(CharacterUnit deadUnit)
    {
        // Mise à jour de la liste d'unités actives utilisée par la boucle de combat
        activeCharacterUnits.Remove(deadUnit);

        // Le gestionnaire d'UI supprime l'élément graphique correspondant
        BattleTimelineUIManager.Instance?.RemoveFromTimeline(deadUnit);
    }

    public void OnEnemyDefeated(CharacterUnit enemy)
    {
        rewardItems.AddRange(enemy.lootItems);
        rewardXP += enemy.experienceReward;
        HandleEndOfBattle();
    }

    public void RegisterDamage(CharacterUnit caster, float amount)
    {
        if (caster == null || caster.Data.characterType != CharacterType.SquadUnit)
            return;

        int dmg = Mathf.RoundToInt(amount);
        currentTurnDamage += dmg;

        if (!totalDamageDealt.ContainsKey(caster))
            totalDamageDealt[caster] = 0;
        totalDamageDealt[caster] += dmg;
    }

    public CharacterUnit GetTopDamageDealer()
    {
        if (totalDamageDealt.Count == 0)
            return null;

        int maxDamage = totalDamageDealt.Values.Max();
        var candidates = totalDamageDealt
            .Where(kvp => kvp.Value == maxDamage)
            .Select(kvp => kvp.Key)
            .Where(u => u != null && u.currentHP > 0)
            .ToList();

        if (candidates.Count == 0)
            return null;

        return candidates.OrderBy(u => u.Data.currentHP).First();
    }

    private IEnumerator EnemyTurnWithQTE(CharacterUnit enemy)
    {
        ChangeBattleState(BattleState.EnemyUnit_PerformingMusicalMove);

        // Choix de l'attaque et de la cible
        var move = enemy.GetRandomMusicalAttack();
        currentMove = move;
        var target = enemy.SelectTargetFromSquad();

        currentTargetCharacter = target;

        if (move == null || target == null)
        {
            Debug.LogWarning("[EnemyTurn] Aucune attaque ou cible valide !");
            yield break;
        }

        // Interception possible uniquement si le move et l'attaquant l'autorisent
        if (move.interceptable && enemy.Data.interceptable && !enemy.isInterceptionImmune)
        {
            yield return TryPlayerInterception(enemy, target, move);
            if (interceptionSucceeded)
                yield break;
        }

        bool alreadyKnown = MusicalCodexManager.Instance != null && MusicalCodexManager.Instance.IsMelodyKnown(move);
        // Affiche le move que l'ennemi prépare
        ActionUIDisplayManager.Instance.DisplayEnemyPreparation(enemy.Data.characterName, alreadyKnown ? move.moveName : null);

        // Joue un indice sonore associé à l'attaque pour prévenir le joueur
        // et calcule un délai suffisant pour laisser le clip se terminer
        float delay = ENEMY_MOVE_DELAY;
        if (move.warningClip != null)
        {
            // Affiche immédiatement la barre de QTE avec les notes
            RhythmQTEManager.Instance?.PrepareQTEBar(move.notes);
            // Les indices sonores sont joués via la nouvelle source dédiée
            AudioManager.Instance?.PlayWarningClip(move.warningClip);
            // On attend au moins la durée du clip pour conserver la cohérence musicale
            delay = Mathf.Max(delay, move.warningClip.length);
        }

        // Laisse un délai pour que le joueur prenne connaissance de l'action
        // On utilise ici un temps réel pour éviter tout blocage si le jeu est en pause
        yield return new WaitForSecondsRealtime(delay);
        // On arrête de forcer la caméra sur la cible avant d'exécuter l'attaque
        lookAtCasterFromTargetPoint = false;
        yield return RhythmQTEManager.Instance.MusicalMoveRoutine(move, enemy, target);

        // Ajoute le move au codex et affiche sa découverte si nécessaire
        if (!alreadyKnown && MusicalCodexManager.Instance != null && MusicalCodexManager.Instance.TryAddNewMelody(move))
        {
            ActionUIDisplayManager.Instance.DisplayMoveDiscovery(move.moveName);
        }
    }

    public IEnumerator ExecuteMoveOnTarget(MusicalMoveSO move, CharacterUnit caster, CharacterUnit target)
    {
        Debug.Log($"{caster} exécute le mouvement {move.moveName} sur {target}");
        ToggleMenuContainers(false, false, false);
        // Vérifie les limites d'utilisation avant de lancer le QTE
        if (!caster.CanUseMove(move))
        {
            ActionUIDisplayManager.Instance.DisplayInstruction("Limite d'utilisation atteinte");
            yield break;
        }
        if (!IsTargetInRange(caster, target, move))
        {
            ActionUIDisplayManager.Instance.DisplayInstruction_TargetTooFar();
            yield break;
        }
        if (!HasSpaceForMove(caster, target, move))
        {
            // Affiche un message utilisateur si la position relative est bloquée.
            ActionUIDisplayManager.Instance.DisplayInstruction_TargetPositionOccupied();
            Debug.LogWarning("[ExecuteMoveOnTarget] Pas assez d'espace pour executer le mouvement.");
            yield break;
        }

        if (!IsTargetAltitudeValid(target, move))
        {
            // Message spécifique selon la contrainte de hauteur définie sur le move.
            if (move.altitudeCondition == AltitudeCondition.AirOnly)
                ActionUIDisplayManager.Instance.DisplayInstruction("La cible doit être en l'air sans sol sous elle");
            else if (move.altitudeCondition == AltitudeCondition.GroundOnly)
                ActionUIDisplayManager.Instance.DisplayInstruction("La cible doit être au sol");
            yield break;
        }

        if (move.enterAwake && (caster.IsAwake ||
            caster.GetHarmonicCount(caster.Data.harmonicType) < caster.Data.resonancePoint))
        {
            Debug.LogWarning("[ExecuteMoveOnTarget] Conditions d'Awake non remplies.");
            yield break;
        }

        OrientUnitTowardTarget(caster, target);

        // Vérifie si le move et le lanceur peuvent être interceptés
        if (move.interceptable && caster.Data.interceptable && !caster.isInterceptionImmune)
        {
            var interceptor = CheckForInterception(caster, target, caster.Data.currentInterceptionRange);
            if (interceptor != null)
            {
                yield return InterceptRoutine(interceptor, caster);
                yield break;
            }
        }
        // Lecture d'un avertissement sonore si le mouvement en possède un
        if (move.warningClip != null)
        {
            RhythmQTEManager.Instance?.PrepareQTEBar(move.notes);
            // Les indices sonores sont joués via la nouvelle source dédiée
            AudioManager.Instance?.PlayWarningClip(move.warningClip);
            // On attend la fin du clip pour conserver la cohérence musicale
            yield return new WaitForSecondsRealtime(move.warningClip.length);
        }

        yield return RhythmQTEManager.Instance.MusicalMoveRoutine(move, caster, target);

        // Ajout du système de rage manuellement
        var rage = caster.GetComponent<RageSystem>();
        if (rage != null && move.effectType == MusicalEffectType.Damage)
        {
            float bonus = rage.CalculateBonusDamage();
            if (bonus > 0)
            {
                target.TakeDamage(bonus, caster.transform);
            }
            if (rage.IsEnraged)
                rage.ConsumeRage();
        }

        var concentration = caster.GetComponent<ConcentrationSystem>();
        if (concentration != null && move.effectType == MusicalEffectType.Damage)
        {
            float bonus = concentration.CalculateBonusDamage(move.effectValue + caster.currentPower);
            if (bonus > 0)
            {
                target.TakeDamage(bonus, caster.transform);
            }
        }

        // L'ATB n'est plus remis à zéro afin de permettre l'enchaînement de plusieurs
        // actions (compétence ou objet) dans le même tour tant que le joueur dispose des
        // ressources nécessaires.
        //currentCharacterUnit.currentATB = 0f;
    }

    public IEnumerator UseItemOnTarget(ItemData item, CharacterUnit caster, CharacterUnit target)
    {
        if (!InventoryManager.Instance.CanUseItem(item))
        {
            ActionUIDisplayManager.Instance.DisplayInstruction("Limite d'utilisation atteinte");
            yield break;
        }

        if (!IsTargetInRange(caster, target, item))
        {
            ActionUIDisplayManager.Instance.DisplayInstruction_TargetTooFar();
            yield break;
        }

        OrientUnitTowardTarget(caster, target);

        // Animation ou Timeline d'utilisation
        yield return RhythmQTEManager.Instance.ItemRoutine(item, caster, target);

        bool crit = RhythmQTEManager.Instance.LastItemSuccess;
        if (crit)
            ActionUIDisplayManager.Instance?.DisplayCriticalHit();
        InventoryManager.Instance.UseItem(item, caster, target, crit);

        // Calcule les dégâts totaux infligés par l'objet
        float dmgVal = 0f;
        if (item.effectType == ItemEffectType.Damage)
            dmgVal += item.effectValue;
        if (crit && item.useCriticalVariant && item.criticalEffectType == ItemEffectType.Damage)
            dmgVal += item.criticalEffectValue;

        if (dmgVal > 0f)
        {
            RegisterDamage(caster, dmgVal);
        }
        caster.GetComponent<FatigueSystem>()?.OnActionPerformed();
        // L'utilisation d'un objet ne met plus fin immédiatement au tour
        // On laisse l'ATB inchangé pour permettre l'exécution d'un mouvement ensuite
        yield return null;

        // Retour au menu principal pour choisir une autre action
        ShowMainMenu();
    }

    /// <summary>
    /// Vérifie si l'emplacement relatif requis par le mouvement est libre.
    /// Retourne faux si une autre unité (hors lanceur et cible) occupe déjà la zone.
    /// </summary>
    public bool HasSpaceForMove(CharacterUnit caster, CharacterUnit target, MusicalMoveSO move)
    {
        // Direction à partir de la cible en fonction de la position relative demandée.
        Vector3 direction = target.transform.forward;
        switch (move.relativePosition)
        {
            case RelativePosition.Back:
                direction = -target.transform.forward;
                break;
            case RelativePosition.Left:
                direction = -target.transform.right;
                break;
            case RelativePosition.Right:
                direction = target.transform.right;
                break;
        }

        float mobilityBonus = caster.currentMobility;
        Vector3 destination = target.transform.position + direction * (move.castDistance + mobilityBonus);
        // Recherche de toute unité se trouvant déjà à l'emplacement calculé.
        Collider[] hits = Physics.OverlapSphere(destination, 0.5f);
        foreach (var h in hits)
        {
            CharacterUnit cu = h.GetComponentInParent<CharacterUnit>();
            // On ignore le lanceur et la cible eux-mêmes.
            if (cu != null && cu != caster && cu != target)
                return false;
        }
        return true;
    }

    public bool IsTargetInRange(CharacterUnit caster, CharacterUnit target, MusicalMoveSO move)
    {
        if (caster == null || target == null || move == null)
        {
            Debug.LogWarning("[IsTargetInRange] caster, target ou move manquant.");
            return false;
        }
        float distance = Vector3.Distance(caster.transform.position, target.transform.position);
        float maxReach = caster.Data.currentRange + move.castDistance;
        return distance <= maxReach;
    }

    public bool IsTargetInRange(CharacterUnit caster, CharacterUnit target, ItemData item)
    {
        if (caster == null || target == null || item == null)
            return false;

        float distance = Vector3.Distance(caster.transform.position, target.transform.position);
        float maxReach = caster.Data.currentRange + item.castDistance;
        return distance <= maxReach;
    }

    /// <summary>
    /// Vérifie si la hauteur actuelle de la cible correspond aux exigences du mouvement.
    /// </summary>
    public bool IsTargetAltitudeValid(CharacterUnit target, MusicalMoveSO move)
    {
        if (target == null || move == null)
            return false;

        switch (move.altitudeCondition)
        {
            case AltitudeCondition.AirOnly:
                // Attaque réservée aux unités sans aucun sol sous elles.
                // Qu'une unité soit terrestre ou aérienne, la présence d'un support
                // en contrebas la protège de ce type d'assaut venu du ciel.
                return !target.HasGroundBelow();
            case AltitudeCondition.GroundOnly:
                // Attaque réservée aux unités terrestres posées sur un support.
                // Cependant, si une unité survole un sol, elle peut aussi être touchée
                // par les attaques terrestres qui résonnent à travers la scène,
                // tandis qu'elle devient intouchable par les attaques aériennes.
                return (!target.IsAirUnit && target.IsGrounded) || target.HasGroundBelow();
            default:
                // Aucune restriction : mouvement utilisable dans toutes les configurations.
                return true;
        }
    }

    private CharacterUnit CheckForInterception(CharacterUnit caster, CharacterUnit target, float range)
    {
        if (caster != null && (!caster.Data.interceptable || caster.isInterceptionImmune))
            return null; // Attaquant non interceptable
        foreach (var unit in activeCharacterUnits)
        {
            if (unit == null || unit == caster || unit == target) continue;
            if (unit.Data.isPlayerControlled == caster.Data.isPlayerControlled) continue;

            if (Vector3.Distance(unit.transform.position, caster.transform.position) <= range)
            {
                var conc = unit.GetComponent<ConcentrationSystem>();
                if (conc != null && conc.IsFull)
                    return unit;

                float chance = unit.currentReflex / (unit.currentReflex + caster.currentReflex + 1f);
                if (Random.value < chance)
                    return unit;
            }
        }
        return null;
    }

    private CharacterUnit FindPlayerInterceptor(CharacterUnit caster, CharacterUnit target, float range)
    {
        if (caster != null && (!caster.Data.interceptable || caster.isInterceptionImmune))
            return null; // Attaquant non interceptable
        CharacterUnit best = null;
        float bestChance = 0f;
        foreach (var unit in activeCharacterUnits)
        {
            if (unit == null || unit == caster || unit == target) continue;
            if (!unit.Data.isPlayerControlled) continue;

            if (Vector3.Distance(unit.transform.position, caster.transform.position) <= range)
            {
                var conc = unit.GetComponent<ConcentrationSystem>();
                if (conc != null && conc.IsFull)
                    return unit;

                float chance = unit.currentReflex / (unit.currentReflex + caster.currentReflex + 1f);
                if (chance > bestChance)
                {
                    bestChance = chance;
                    best = unit;
                }
            }
        }
        return best;
    }

    private IEnumerator TryPlayerInterception(CharacterUnit caster, CharacterUnit target, MusicalMoveSO move)
    {
        interceptionSucceeded = false;
        if (caster != null && (!caster.Data.interceptable || caster.isInterceptionImmune))
            yield break; // Impossible d'intercepter ce lanceur
        var interceptor = FindPlayerInterceptor(caster, target, caster.Data.currentInterceptionRange);
        if (interceptor == null)
            yield break;

        Debug.Log($"[Interception] {interceptor.name} tente d'intercepter {caster.name} ({move.moveName})");
        ActionUIDisplayManager.Instance.DisplayInterceptionAttempt();

        var conc = interceptor.GetComponent<ConcentrationSystem>();
        if (conc != null && conc.IsFull)
        {
            yield return InterceptRoutine(interceptor, caster);
            interceptionSucceeded = true;
            Debug.Log("[Interception] Réussite automatique grâce à la concentration pleine.");
            ActionUIDisplayManager.Instance.DisplayInterceptionResult(true);
            yield break;
        }

        float chance = interceptor.currentReflex / (interceptor.currentReflex + caster.currentReflex + 1f);
        float window = Mathf.Lerp(0.2f, 1.5f, chance);

        GameObject signalObj = null;
        if (interceptionSignalPrefab != null)
        {
            signalObj = Instantiate(interceptionSignalPrefab, target.transform.position + Vector3.up * 2f, Quaternion.identity, target.transform);
            var sig = signalObj.GetComponent<InterceptionSignal>();
            if (sig != null)
                sig.StartSignal(window);
        }

        var action = new InputAction(binding: "<Gamepad>/leftShoulder");
        action.Enable();
        bool pressed = false;
        action.performed += _ => pressed = true;

        float elapsed = 0f;
        while (elapsed < window)
        {
            if (pressed)
            {
                if (signalObj != null) Destroy(signalObj);
                action.Disable();
                yield return InterceptRoutine(interceptor, caster);
                interceptionSucceeded = true;
                Debug.Log("[Interception] Interception réussie !");
                ActionUIDisplayManager.Instance.DisplayInterceptionResult(true);
                yield break;
            }
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (signalObj != null) Destroy(signalObj);
        action.Disable();
        Debug.Log("[Interception] Interception échouée.");
        ActionUIDisplayManager.Instance.DisplayInterceptionResult(false);
    }

    private IEnumerator InterceptRoutine(CharacterUnit interceptor, CharacterUnit caster)
    {
        if (interceptor == null) yield break;

        caster?.PlayInterceptedAnimation();
        caster?.PlayInterceptedSound();
        caster?.ClearAllHarmonics();
        interceptor?.PlayInterceptionAnimation();
        // Son joué par l'unité qui intercepte
        interceptor?.PlayInterceptionSound();

        var move = interceptor.GetRandomMusicalAttack();
        if (move != null)
        {
            ActionUIDisplayManager.Instance.DisplayActionMessage(interceptor.Data.characterName, move.moveName, caster.Data.characterName);
            yield return RhythmQTEManager.Instance.MusicalMoveRoutine(move, interceptor, caster);
            if (move.notes == null || move.notes.Count == 0)
                move.ApplyEffect(interceptor, caster);
        }
    }

    public void EndTurn()
    {
        if (currentCharacterUnit != null)
        {
            Debug.Log($"[BattleTurnManager] Fin du tour de {currentCharacterUnit.name}");
            currentCharacterUnit.currentATB = 0f;

            if (currentCharacterUnit.interceptionImmunityTurns > 0)
            {
                currentCharacterUnit.interceptionImmunityTurns--;
                if (currentCharacterUnit.interceptionImmunityTurns <= 0)
                    currentCharacterUnit.isInterceptionImmune = false;
            }

            if (currentCharacterUnit.Data.characterType == CharacterType.SquadUnit && currentTurnDamage > maxTurnDamage)
            {
                maxTurnDamage = currentTurnDamage;
                mvpUnit = currentCharacterUnit;
            }
            currentCharacterUnit.animator.Play("Idle_Battle");
        }

        ChangeBattleState(BattleState.EndTurn);
        // Cache tous les menus à la fin du tour
        ToggleMenuContainers(false, false, false);
        // Réinitialise la timeline visuelle (ordre + surbrillance)
        BattleTimelineUIManager.Instance?.Refresh(null);
        isTurnResolving = false;
        HandleEndOfBattle();

        // Cache la jauge si elle existe pour éviter les références invalides.
        PassTurnUI.Instance?.Hide(); // Bouclage
    }

    public void AfterMusicalMove(MusicalMoveSO move, CharacterUnit caster, bool wasCritical)
    {
        // Affiche un message si toutes les notes du QTE ont été réussies
        if (wasCritical)
            ActionUIDisplayManager.Instance?.DisplayCriticalHit();

        if (caster != null)
        {
            int cost = move.harmonicCost;
            int generation = move.harmonicGeneration;

            if (wasCritical && move.useCriticalVariant)
            {
                // Ajoute les valeurs spécifiées pour le coup critique
                cost += move.criticalHarmonicCost;
                generation += move.criticalHarmonicGeneration;
            }

            caster.ConsumeHarmonic(caster.Data.harmonicType, cost);
            caster.AddHarmonic(caster.Data.harmonicType, generation);
            caster.SetMoveCooldown(move);
            caster.RegisterMoveUse(move);

            // Activation du mode Awake si le move le permet
            if (move.enterAwake && !caster.IsAwake &&
                caster.GetHarmonicCount(caster.Data.harmonicType) >= caster.Data.resonancePoint)
            {
                caster.EnterAwakeState();
            }

            // Si l'unité n'a plus d'harmonique, son tour se termine immédiatement
            if (caster.GetHarmonicCount(caster.Data.harmonicType) <= 0)
            {
                EndTurn();
                return;
            }
        }

        if (!caster.Data.isPlayerControlled)
        {
            EndTurn();
            return;
        }

        //
        // Vérifie si au moins une compétence reste utilisable pour le lanceur.
        // On prend en compte les moves standards ainsi que le move spécial
        // éventuel. On ignore ceux en cooldown pour éviter de terminer
        // prématurément le tour.

        IEnumerable<MusicalMoveSO> availableMoves = caster.Data.musicalAttacks;
        if (caster.Data.specialMusicalMove != null)
            availableMoves = availableMoves.Append(caster.Data.specialMusicalMove);

        bool hasSkill = availableMoves.Any(m =>
            (!m.onlyAwake || caster.IsAwake) &&
            (!m.enterAwake || !caster.IsAwake) &&
            caster.GetHarmonicCount(caster.Data.harmonicType) >= m.harmonicCost &&
            (!m.enterAwake || caster.GetHarmonicCount(caster.Data.harmonicType) >= caster.Data.resonancePoint) &&
            !caster.IsMoveOnCooldown(m));
        bool hasItem = InventoryManager.Instance.GetUsableItems().Count > 0;

        if (!hasSkill && !hasItem)
            EndTurn();
        else
            ShowMainMenu();
    }

    public IEnumerator ShowMoveInfoAndHandleSelection(MusicalMoveSO move)
    {
        string message = $"{move.description}\nCoût : {move.harmonicCost} harmonique(s)\nGénère : {move.harmonicGeneration} harmonique(s)";
        InfoBoxManager.Instance.OpenInfoBox(move.moveName, message, move.moveIcon);
        while (!InfoBoxManager.Instance.choix.HasValue)
            yield return null;

        if (InfoBoxManager.Instance.choix.Value)
        {
            ToggleMenuContainers(false, false, false);
            HandleTargetSelection(move);
            // La Timeline de préparation prend en charge l'animation de ciblage
        }
        else
        {
            OpenSkillsMenu();
        }
    }

    public IEnumerator ShowItemInfoAndHandleSelection(ItemData item)
    {
        string message = item.description;
        InfoBoxManager.Instance.OpenInfoBox(item.itemName, message, item.itemIcon);
        while (!InfoBoxManager.Instance.choix.HasValue)
            yield return null;

        if (InfoBoxManager.Instance.choix.Value)
        {
            ToggleMenuContainers(false, false, false);
            HandleTargetSelection(item);
            // L'animation de ciblage est désormais gérée par la Timeline de préparation
        }
        else
        {
            OpenItemMenu();
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
            if (unit == null || unit.Data.currentHP <= 0)
                continue;

            bool isPlayer = unit.Data.isPlayerControlled;

            // Trouve toutes les unités ennemies vivantes
            var enemies = unitsInBattle
                .Where(u => u != null && u.Data.currentHP > 0 && u.Data.isPlayerControlled != isPlayer)
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
        foreach (var unit in activeCharacterUnits)
        {
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

        if (allEnemiesDead && currentBattleState != BattleState.VictoryScreen_Await)
        {
            Debug.Log("[BattleTurnManager] 🎉 Tous les ennemis sont vaincus !");
            lastBattleOutcome = BattleOutcome.Victory; // Enregistre l'issue du combat
            ChangeBattleState(BattleState.VictoryScreen_Await);
            StartCoroutine(ReduceTimeAndShowVictoryPanel());
        }
        else if (allSquadDead)
        {
            Debug.Log("[BattleTurnManager] 💀 Tous les alliés sont morts...");
            lastBattleOutcome = BattleOutcome.Defeat;

            if (defeatTimeline != null)
            {
                // Aucune interface de Game Over, on quitte directement le combat
                if (BattleTransitionManager.Instance != null)
                    BattleTransitionManager.Instance.StartCoroutine(
                        BattleTransitionManager.Instance.ExitVictoryScreenAndBattle());
                else
                    Debug.LogWarning("[BattleTurnManager] BattleTransitionManager introuvable pour quitter le combat.");
            }
            else if (gameOverOnDefeat)
            {
                // Passage direct au menu principal
                CleanupAllSpawnedUnits();
                if (GameManager.Instance != null)
                {
                    // Utilisation du GameManager pour charger le menu principal
                    GameManager.Instance.TriggerGameOver();
                }
                else
                {
                    Debug.LogWarning("[BattleTurnManager] GameManager absent, impossible de charger le menu principal.");
                }
            }
            else
            {
                // Affichage du panneau Game Over permettant de continuer la partie
                ChangeBattleState(BattleState.GameOverScreen_Await);
                StartCoroutine(ShowGameOverPanel());
            }
        }
    }

    public void TakeVictoryScreenshot()
    {
        if (VictoryScreenImage == null)
        {
            Debug.LogError("VictoryScreenImage n'est pas assigné !");
            return;
        }

        // Utilise la caméra mémorisée ; la recherche est effectuée ponctuellement si nécessaire
        EnsureBattleCamera();
        Camera screenshotCamera = battleCamera?.GetComponent<Camera>();
        if (screenshotCamera == null)
        {
            Debug.LogError("Aucune caméra trouvée pour la capture !");
            return;
        }

        // Assure-toi que la caméra utilise la render texture pour la capture
        RenderTexture prevRT = screenshotCamera.targetTexture;
        screenshotCamera.targetTexture = VictoryScreenImage;
        screenshotCamera.Render();
        screenshotCamera.targetTexture = prevRT;

        Debug.Log("Screenshot de victoire capturé !");
    }

    private IEnumerator ReduceTimeAndShowVictoryPanel()
    {
        // Ralentissement progressif vers un arrêt complet en 2 secondes
        if (BattleTransitionManager.Instance != null)
            yield return BattleTransitionManager.Instance.StartCoroutine(
                BattleTransitionManager.Instance.SlowTimeScale(0f, 0.5f));
        else
            Time.timeScale = 0f;

        Time.fixedDeltaTime = 0f;

        // Capture de la dernière image avant d'afficher l'écran de victoire
        TakeVictoryScreenshot();

        // Activation du panneau VictoryScreen (animation en temps réel)
        victoryScreen.SetActive(true);
        Transform victoryPanel = victoryScreen.transform.GetChild(0);
        Animator victoryAnim = victoryPanel.GetComponent<Animator>();
        if (victoryAnim != null)
            victoryAnim.updateMode = AnimatorUpdateMode.UnscaledTime;
        victoryPanel.gameObject.SetActive(true);

        GameManager.Instance?.AddXPToSquad(rewardXP);
        GameManager.Instance?.AddItemsToInventory(rewardItems);

        var panel = victoryScreen.GetComponentInChildren<VictoryPanelManager>();

        float duration = Time.time - battleStartTime;
        int totalEnemies = GameManager.Instance != null ? GameManager.Instance.gameData.enemiesDefeatedCount : 0;
        panel?.DisplayVictory(rewardXP, rewardItems, totalEnemies, duration, mvpUnit, maxTurnDamage);

        // Applique la RenderTexture sur le RawImage du panel
        RawImage img = victoryScreen.transform.GetChild(0).GetComponent<RawImage>();
        if (img != null)
        {
            img.texture = VictoryScreenImage;
        }
        else
        {
            Debug.LogWarning("Pas de RawImage sur le VictoryScreen child(0)");
        }

        GameObject continueButton = FindChildRecursive(victoryScreen.transform.GetChild(0), "BattleScene_UI_VictoryPanel_Continue").gameObject;

        // Les unités restent désormais en place durant l'écran de victoire afin
        // que le joueur puisse admirer le champ de bataille tel qu'il était au
        // moment de la victoire. Leur suppression se fera plus tard, au retour
        // dans le monde.
        ChangeBattleState(BattleState.VictoryScreen_CanContinue);
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

    private void SetupCurrentUnitMenus()
    {
        // 1) Essaye de récupérer la BattleCamera par tag
        // Utilise la caméra de combat mémorisée        
        Transform battleCamCam = GameObject.Find("BattleCamera_Cam").transform;
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

            // S'assure de disposer d'au moins 4 emplacements
            // (les trois premiers pour les attaques musicales, le dernier pour le move spécial)
            if (currentSkillsMenuSlots.Count > 0)
            {
                while (currentSkillsMenuSlots.Count < 4)
                {
                    // Clone le premier slot pour compléter la liste si besoin
                    Transform clone = Instantiate(currentSkillsMenuSlots[0], currentSkillsMenuSlots[0].parent);
                    currentSkillsMenuSlots.Add(clone);
                }
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

    public void ShowMainMenu()
    {
        // Réaffiche la jauge de passage de tour si elle existe.
        PassTurnUI.Instance?.Show();
        ActionUIDisplayManager.Instance.DisplayInstruction_SelectItemSkillOrPass();
        ChangeBattleState(BattleState.SquadUnit_MainMenu);
        ToggleMenuContainers(true, false, false);

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
        // Masque la jauge lorsque l'on ouvre le menu des compétences.
        PassTurnUI.Instance?.Hide();
        ActionUIDisplayManager.Instance.DisplayInstruction_SelectSkill();
        ChangeBattleState(BattleState.SquadUnit_SkillsMenu);
        // S'assure qu'aucun item n'est en cours de sélection
        currentItem = null;
        ToggleMenuContainers(false, true, false);
        currentMenuIndex = 0;

        // Réinitialise la page affichée
        currentSkillPageIndex = 0;

        // Récupère toutes les attaques musicales disponibles (hors move spécial)
        skillChoices = currentCharacterUnit.Data.musicalAttacks
            .Where(m => !m.onlyAwake || currentCharacterUnit.IsAwake)
            .Where(m => !m.enterAwake || !currentCharacterUnit.IsAwake)
            .Where(m => currentCharacterUnit.CanUseMove(m))
            .ToList();

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

        int pageSize = specialSlotIndex; // Slots disponibles pour les attaques musicales classiques
        int startIndex = currentSkillPageIndex * pageSize;

        // 1) Affiche les attaques musicales standard dans les premiers slots
        for (int i = 0; i < pageSize; i++)
        {
            int globalIndex = startIndex + i;
            if (globalIndex < skillChoices.Count)
            {
                var move = skillChoices[globalIndex];
                UpdateButton(currentSkillsMenuSlots[i], move.moveName, move.moveIcon);

                bool enoughHarmonic = currentCharacterUnit.GetHarmonicCount(currentCharacterUnit.Data.harmonicType) >= move.harmonicCost;
                bool resonanceOk = !move.enterAwake || currentCharacterUnit.GetHarmonicCount(currentCharacterUnit.Data.harmonicType) >= currentCharacterUnit.Data.resonancePoint;
                bool usageOk = currentCharacterUnit.CanUseMove(move);
                bool available = enoughHarmonic && resonanceOk && usageOk;
                SetButtonAvailability(currentSkillsMenuSlots[i], available, false);
            }
            else
            {
                // Slot vide ou hors de portée
                if (emptyMove != null)
                    UpdateButton(currentSkillsMenuSlots[i], emptyMove.moveName, emptyMove.moveIcon);
                else
                    UpdateButton(currentSkillsMenuSlots[i], "Indisponible", null);
                SetButtonAvailability(currentSkillsMenuSlots[i], false, false);
            }
        }

        // 2) Place le mouvement spécial dans le dernier slot
        if (specialMoveChoice != null)
        {
            UpdateButton(currentSkillsMenuSlots[specialSlotIndex], specialMoveChoice.moveName, specialMoveChoice.moveIcon);

            bool enoughHarmonic = currentCharacterUnit.GetHarmonicCount(currentCharacterUnit.Data.harmonicType) >= specialMoveChoice.harmonicCost;
            bool resonanceOk = !specialMoveChoice.enterAwake || currentCharacterUnit.GetHarmonicCount(currentCharacterUnit.Data.harmonicType) >= currentCharacterUnit.Data.resonancePoint;
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
        // Calcule le nombre de pages possibles (hors slot spécial)
        int pageSize = currentSkillsMenuSlots.Count - 1;
        if (pageSize <= 0)
        {
            // Évite une division par zéro si aucun slot n'est disponible
            Debug.LogWarning("[NextSkillPage] Impossible de changer de page : nombre de slots insuffisant.");
            return;
        }

        int maxPage = Mathf.Max(0, (skillChoices.Count - 1) / pageSize);
        if (currentSkillPageIndex < maxPage)
        {
            currentSkillPageIndex++;
            RefreshSkillsMenuDisplay();
        }
    }

    /// <summary>
    /// Revient à la page précédente des compétences si possible.
    /// </summary>
    public void PreviousSkillPage()
    {
        // Vérifie qu'il existe réellement des pages avant de revenir en arrière
        if (currentSkillPageIndex > 0)
        {
            currentSkillPageIndex--;
            RefreshSkillsMenuDisplay();
        }
    }

    public void OpenItemMenu()
    {
        ActionUIDisplayManager.Instance.DisplayInstruction_SelectItem();
        ChangeBattleState(BattleState.SquadUnit_ItemsMenu);
        // S'assure qu'aucune compétence n'est en cours de sélection
        currentMove = null;
        ToggleMenuContainers(false, false, true);
        currentMenuIndex = 0;

        // Démarre la Timeline d'attente de sélection d'objet
        StartItemPreparingTimeline();

        itemChoices = InventoryManager.Instance.GetUsableItems();

        // 6) Création des boutons d’items
        for (int i = 0; i < itemChoices.Count && i < currentItemsMenuSlots.Count; i++)
        {
            var item = itemChoices[i];
            UpdateButton(currentItemsMenuSlots[i], item.itemName, item.itemIcon);
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

    private void UpdateButton(Transform slot, string label, Sprite icon)
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
        var img = slot.childCount > 3 ? slot.GetChild(3).GetComponent<Image>() : null;

        if (txt != null) txt.text = label;
        if (img != null) img.sprite = icon;
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
        Transform casterCameraAnchor = null;
        if (currentCharacterUnit != null)
        {
            GameObject casterBinding = currentCharacterUnit.GetCasterBindingTarget();
            casterCameraAnchor = casterBinding != null ? casterBinding.transform : null;
        }

        BattleCameraManager.Instance?.ConfigureActionTargets(
            currentCharacterUnit,
            null,
            null,
            casterCameraAnchor,
            null);
        BattleCameraManager.Instance?.SwitchToCamera(ItemPreparingCameraRole);

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

        currentTargetCharacter = filteredUnits[currentTargetIndex];
    }
    #endregion

    #region Gestion de la navigation parmi les unités en combat

    private void HandleTargetCursor()
    {
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
                    float maxReach = currentCharacterUnit.Data.currentRange + currentMove.castDistance;
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
                    float maxReach = currentCharacterUnit.Data.currentRange + currentItem.castDistance;
                    bool inRange = distance <= maxReach;
                    UpdateTargetCursorColor(inRange);
                }
                else
                {
                    targetCursor.transform.position = currentTargetCharacter.transform.position;
                    UpdateTargetCursorColor(true);
                }
            }
        }
    }

    public void HandleTargetSelection(MusicalMoveSO move)
    {
        currentMove = move;
        currentItem = null; // on annule la sélection d'item précédente
        move.targetType = move.defaultTargetType;
        // Détermine s'il est possible de changer de groupe de cibles
        bool canTargetEnemies = move.targetTypes.Contains(TargetType.SingleEnemy)
                               || move.targetTypes.Contains(TargetType.AllEnemies)
                               || move.targetTypes.Contains(TargetType.All);
        bool canTargetAllies = move.targetTypes.Contains(TargetType.SingleAlly)
                              || move.targetTypes.Contains(TargetType.AllAllies)
                              || move.targetTypes.Contains(TargetType.All);
        bool allowGroupSwitch = canTargetEnemies && canTargetAllies;
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
    }

    public void HandleTargetSelection(ItemData item)
    {
        // Lorsque l'objet est choisi, on coupe la Timeline d'attente
        StopItemPreparingTimeline();
        currentItem = item;
        currentMove = null; // on annule la sélection de compétence précédente
        currentItemTargetType = item.defaultTargetType;

        bool canTargetEnemies = item.targetTypes.Contains(TargetType.SingleEnemy) ||
                               item.targetTypes.Contains(TargetType.AllEnemies) ||
                               item.targetTypes.Contains(TargetType.All);
        bool canTargetAllies = item.targetTypes.Contains(TargetType.SingleAlly) ||
                              item.targetTypes.Contains(TargetType.AllAllies) ||
                              item.targetTypes.Contains(TargetType.All);
        bool allowGroupSwitch = canTargetEnemies && canTargetAllies;

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
    }
    #endregion

    #region Gestion des mouvements de la caméra de combat
    public void UpdateCameraBehaviour(BattleState newState)
    {
        // On vérifie ponctuellement que la caméra de combat est bien référencée
        EnsureBattleCamera();
        if (battleCamera == null)
        {
            // Impossible de poursuivre sans caméra
            return;
        }

        // Vérifie que l'unité courante est bien définie. Sans elle,
        // certaines positions de caméra ne peuvent pas être calculées.
        if (currentCharacterUnit == null)
        {
            Debug.LogWarning("[BattleCameraManager] currentCharacterUnit est nul lors de la mise à jour de la caméra.");
            return;
        }

        // Par défaut, on ne regarde pas le lanceur
        lookAtCasterDuringTargetSelection = false;
        // Désactive le focus spécial utilisé pendant le tour ennemi
        lookAtCasterFromTargetPoint = false;

        switch (currentBattleState)
        {
            case BattleState.SquadUnit_MainMenu:
                isFollowingCurrentTarget = false;
                desiredTransform = FindChildRecursive(currentCharacterUnit.transform, "Camera_MainMenu");
                OrientTransformTowardEnemyGroupSmoothXY(desiredTransform, 180f);
                if (desiredTransform == null)
                {
                    Debug.LogError("[BattleCameraManager] Aucun point 'Camera_MainMenu' trouvé.");
                }
                break;

            case BattleState.SquadUnit_SkillsMenu:
                isFollowingCurrentTarget = false;
                desiredTransform = FindChildRecursive(currentCharacterUnit.transform, "Camera_SkillsMenu");
                if (desiredTransform == null)
                {
                    Debug.LogError("[BattleCameraManager] Aucun point 'Camera_SkillsMenu' trouvé.");
                }
                break;

            case BattleState.SquadUnit_ItemsMenu:
                isFollowingCurrentTarget = false;
                desiredTransform = FindChildRecursive(currentCharacterUnit.transform, "Camera_ItemsMenu");
                if (desiredTransform == null)
                {
                    Debug.LogError("[BattleCameraManager] Aucun point 'Camera_ItemsMenu' trouvé.");
                }
                break;

            case BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad:
                if (currentItem != null)
                {
                    desiredTransform = FindChildRecursive(currentCharacterUnit.transform, "Camera_UseItem_Prepare");
                    isFollowingCurrentTarget = true;
                }
                else
                {
                    desiredTransform = GameObject.Find("Camera_FocusSquad").transform;
                    isFollowingCurrentTarget = false;
                }
                break;

            case BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies:
                if (currentItem != null)
                {
                    desiredTransform = FindChildRecursive(currentCharacterUnit.transform, "Camera_UseItem_Prepare");
                    isFollowingCurrentTarget = true;
                }
                else
                {
                    desiredTransform = GameObject.Find("Camera_FocusEnemies").transform;
                    isFollowingCurrentTarget = false;
                }
                break;

            case BattleState.SquadUnit_TargetSelectionAmongSquadForSkill:
                // Caméra sur la cible sélectionnée, regardant le lanceur
                isFollowingCurrentTarget = false;
                lookAtCasterDuringTargetSelection = true;
                desiredTransform = FindChildRecursive(currentTargetCharacter.transform, "Camera_TargetedPoint");
                break;

            case BattleState.SquadUnit_TargetSelectionAmongSquadForItem:
                // Même comportement que pour une compétence
                isFollowingCurrentTarget = false;
                lookAtCasterDuringTargetSelection = true;
                desiredTransform = FindChildRecursive(currentTargetCharacter.transform, "Camera_TargetedPoint");
                break;

            case BattleState.SquadUnit_TargetSelectionAmongEnemiesForSkill:
                // Caméra sur l'ennemi sélectionné, regardant le lanceur
                isFollowingCurrentTarget = false;
                lookAtCasterDuringTargetSelection = true;
                desiredTransform = FindChildRecursive(currentTargetCharacter.transform, "Camera_TargetedPoint");
                break;

            case BattleState.SquadUnit_TargetSelectionAmongEnemiesForItem:
                // Identique pour un objet visant un ennemi
                isFollowingCurrentTarget = false;
                lookAtCasterDuringTargetSelection = true;
                desiredTransform = FindChildRecursive(currentTargetCharacter.transform, "Camera_TargetedPoint");
                break;

            case BattleState.EnemyUnit_Reflexion:
                isFollowingCurrentTarget = false;
                desiredTransform = FindChildRecursive(currentTargetCharacter.transform, "Camera_TargetedPoint");
                break;
            case BattleState.EnemyUnit_PerformingMusicalMove:
                // Place la caméra sur la cible et oriente-la vers l'ennemi
                isFollowingCurrentTarget = false;
                lookAtCasterFromTargetPoint = true;
                desiredTransform = FindChildRecursive(currentTargetCharacter.transform, "Camera_TargetedPoint");
                break;
            case BattleState.EnemyUnit_Item_Prepare:
            case BattleState.EnemyUnit_Item_Use:
                isFollowingCurrentTarget = false;
                desiredTransform = null;
                break;

            case BattleState.VictoryScreen_Await:
                isFollowingCurrentTarget = false;
                desiredTransform = GameObject.Find("Camera_Victory").transform;
                break;

            default:
                isFollowingCurrentTarget = false;
                desiredTransform = null;
                break;
        }
    }

    private void LateUpdate()
    {
        // Si la caméra de combat n'est pas disponible, on ne poursuit pas
        if (battleCamera == null)
        {
            return;
        }

        // Accès direct au transform de la caméra pour limiter les appels
        Transform camTransform = battleCamera.transform;

        CameraController cc = CameraController.Instance;
        // Si une Timeline globale contrôle la caméra (cutscene ou attaque),
        // on laisse entièrement la main au TimelineManager.
        if (TimelineManager.Instance != null && TimelineManager.Instance.IsTimelinePlaying)
            return;

        if (itemMenuTimelineActive)
        {
            // La timeline contrôle la caméra tant que l'objet n'est pas choisi
            return;

        }

        if (lookAtCasterDuringTargetSelection && desiredTransform != null && currentCharacterUnit != null && currentTargetCharacter != null)
        {
            // Nouveau positionnement dynamique pour garder lanceur et cible dans le champ de vision
            ComputeTargetSelectionCamera(out Vector3 camPos, out Quaternion camRot,
                currentCharacterUnit.transform, currentTargetCharacter.transform);

            camTransform.position = Vector3.Lerp(camTransform.position, camPos, Time.deltaTime * cameraSmoothSpeed);
            camTransform.rotation = Quaternion.Slerp(camTransform.rotation, camRot, Time.deltaTime * cameraSmoothSpeed);
        }
        else if (lookAtCasterFromTargetPoint && desiredTransform != null && currentCharacterUnit != null)
        {
            // Positionne la caméra sur le point de la cible en regardant le lanceur
            camTransform.position = Vector3.Lerp(camTransform.position, desiredTransform.position, Time.deltaTime * cameraSmoothSpeed);
            Quaternion targetRotation = Quaternion.LookRotation(currentCharacterUnit.transform.position - camTransform.position);
            camTransform.rotation = Quaternion.Slerp(camTransform.rotation, targetRotation, Time.deltaTime * cameraSmoothSpeed);
        }
        else if (isFollowingCurrentTarget && currentCharacterUnit != null && currentTargetCharacter != null)
        {
            if (desiredTransform != null)
            {
                // Reste sur l'ancre mais suit la cible du regard
                camTransform.position = Vector3.Lerp(camTransform.position, desiredTransform.position, Time.deltaTime * cameraSmoothSpeed);
                Quaternion targetRotation = Quaternion.LookRotation(currentTargetCharacter.transform.position - camTransform.position);
                camTransform.rotation = Quaternion.Slerp(camTransform.rotation, targetRotation, Time.deltaTime * cameraSmoothSpeed);
            }
            else
            {
                Vector3 midPoint = (currentCharacterUnit.transform.position + currentTargetCharacter.transform.position) / 2f;
                Vector3 offset = Vector3.up * 3f - currentCharacterUnit.transform.forward * 5f;

                Vector3 targetPosition = midPoint + offset;
                Quaternion targetRotation = Quaternion.LookRotation(midPoint - camTransform.position);

                camTransform.position = Vector3.Lerp(camTransform.position, targetPosition, Time.deltaTime * cameraSmoothSpeed);
                camTransform.rotation = Quaternion.Slerp(camTransform.rotation, targetRotation, Time.deltaTime * cameraSmoothSpeed);
            }
        }
        else if (desiredTransform != null)
        {
            camTransform.position = Vector3.Lerp(camTransform.position, desiredTransform.position, Time.deltaTime * cameraSmoothSpeed);
            camTransform.rotation = Quaternion.Slerp(camTransform.rotation, desiredTransform.rotation, Time.deltaTime * cameraSmoothSpeed);
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

    public void ChangeBattleState(BattleState newState)
    {
        currentBattleState = newState;
        Debug.Log("Nouvel état de combat: " + newState);
        UpdateCameraBehaviour(newState);
    }

    private void ChangeCurrentCharacterUnit(CharacterUnit newCurrentCharacterUnit)
    {
        currentCharacterUnit = newCurrentCharacterUnit;
    }

    private void EnsureBattleCamera()
    {
        if (battleCamera == null)
        {
            battleCamera = GameObject.FindGameObjectWithTag("BattleCamera");
        }
    }

    void EnsureTargetCursor()
    {
        if (targetCursorPrefab != null)
        {
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
        BattleCameraManager.Instance?.ResetIntroRail();

        // Réinitialisation de l'interface de timeline via le gestionnaire dédié
        BattleTimelineUIManager.Instance?.Clear();

        // Réinitialise les timelines et l'issue du combat
        victoryTimeline = null;
        defeatTimeline = null;
        lastBattleOutcome = BattleOutcome.None;

        // Réinitialise le curseur cible si existant
        if (targetCursor != null)
        {
            Destroy(targetCursor);
            targetCursor = null;
        }
    }
    #endregion
}
