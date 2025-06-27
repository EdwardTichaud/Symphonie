using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Playables;

#region TargetType
public enum TargetType
{
    Self,
    SingleEnemy,
    AllEnemies,
    SingleAlly,
    AllAllies
}
#endregion

#region BattleState
public enum BattleState
{
    None,
    Initialization,
    FirstStrikeSequence,
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
    SquadUnit_UseItem,

    // EnemyUnit Turn
    EnemyUnit_Reflexion,
    EnemyUnit_PerformingMusicalMove,
    EnemyUnit_UseItem,

    // Game Over
    VictoryScreen_Await,
    VictoryScreen_CanContinue,

    GameOverScreen_Await,
    GameOverScreen_CanContinue,
}
#endregion

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
    [SerializeField] private CameraPath firstStrikeCameraPath;

    [Header("Fin de combat")]
    public GameObject victoryScreen;
    public GameObject gameOverScreen;
    public RenderTexture VictoryScreenImage;
    public RenderTexture GameOverScreenImage;

    [Header("Timeline UI")]
    public RectTransform timelineContainer;
    public GameObject timelineUnitPrefab;
    public List<BattleTimelineUnit> timelineUIObjects = new();

    private CharacterUnit previousUnit; // Champ de classe, pas une variable locale
    [HideInInspector] public CharacterUnit currentCharacterUnit;
    private bool isTurnResolving = false;

    private const float ATB_THRESHOLD = 100f;

    [Header("Sprites des touches")]
    [SerializeField] private Sprite inputSprite1;
    [SerializeField] private Sprite inputSprite2;
    [SerializeField] private Sprite inputSprite3;
    [SerializeField] private Sprite inputSprite4;

    [Header("Gestion du curseur de cible")]
    public GameObject targetCursorPrefab;
    [HideInInspector] public GameObject targetCursor;
    private List<CharacterUnit> filteredUnits = new();
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
            UpdateCameraBehaviour(currentBattleState); // Met à jour la caméra quand la cible change
        }
    }
    //-------------------------------------------------------------------------------------

    // Caméra
    [Header("Caméra de combat")]
    [HideInInspector] public Transform battleCameraTransform;
    public float cameraSmoothSpeed = 5f;
    [HideInInspector] public Transform desiredTransform;
    private Vector3 desiredPosition;
    private Quaternion desiredRotation;
    public bool isFollowingCurrentTarget = false;
    private bool isOrbiting = false;
    private float currentOrbitAngle;
    private Transform orbitCenter;

    // Compétences et items disponibles pour l’unité qui joue
    // Garder en public
    [HideInInspector] public List<MusicalMoveSO> skillChoices = new List<MusicalMoveSO>();
    [HideInInspector] public List<ItemData> itemChoices = new List<ItemData>();
    [HideInInspector] public MusicalMoveSO currentMove;
    [HideInInspector] public ItemData currentItem;
    public int currentMenuIndex;
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
        if (activeCharacterUnits.Count > 0)
        {
            Debug.LogWarning("[NewBattleManager] SpawnAll déjà exécuté ou unités déjà présentes.");
            return;
        }

        activeCharacterUnits.Clear();
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

            Vector3 offset = spawnPoint.position - spawnPoint.forward * 4f;

            var unitGO = Instantiate(pc.characterBattleModel, offset, Quaternion.identity);
            unitGO.transform.SetParent(spawnPoint, worldPositionStays: true);
            unitGO.name = $"SquadUnit_{i}";

            // ✅ Spawn du rayon à la bonne position
            if (squadUnitRay != null)
                Instantiate(squadUnitRay, offset, Quaternion.identity);

            var unit = unitGO.GetComponent<CharacterUnit>();
            unit.Initialize(pc);
            unitsInBattle.Add(unit);

            float animationDuration = PlayRandomStartAnimation(unitGO); // ⏱️
            StartCoroutine(AnimateSpawn(unitGO, spawnPoint.position, animationDuration));
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

            Vector3 offset = spawnPoint.position + spawnPoint.forward * 4f;

            var unitGO = Instantiate(enemyData.characterBattleModel, offset, Quaternion.Euler(0f, 180f, 0f));
            unitGO.transform.SetParent(spawnPoint, worldPositionStays: true);
            unitGO.name = $"EnemyUnit_{i}";

            // ✅ Spawn du rayon à la bonne position
            if (enemyUnitRay != null)
                Instantiate(enemyUnitRay, offset, Quaternion.identity);

            var eu = unitGO.GetComponent<CharacterUnit>();
            eu.Initialize(enemyData);
            unitsInBattle.Add(eu);

            float animationDuration = PlayRandomStartAnimation(unitGO); // ⏱️
            StartCoroutine(AnimateSpawn(unitGO, spawnPoint.position, animationDuration));
        }
    }
    #endregion

    #region Mise en scène de la scène de bataille
    private IEnumerator AnimateSpawn(GameObject unitGO, Vector3 targetPosition, float duration)
    {
        Vector3 startPos = unitGO.transform.position;
        float elapsed = 0f;

        CharacterUnit activeUnit = unitGO.GetComponentInChildren<CharacterUnit>();
        OrientAllUnitsTowardCenter(activeUnit);

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            unitGO.transform.position = Vector3.Lerp(startPos, targetPosition, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        unitGO.transform.position = targetPosition;
    }

    private float PlayRandomStartAnimation(GameObject unitGO)
    {
        var animator = unitGO.GetComponentInChildren<Animator>();

        if (animator != null)
        {
            int choice = Random.Range(1, 2);
            string animationName = $"EquipOnMove_{choice}";
            animator.Play(animationName);

            return 2f;
        }

        return 2f;
    }
    #endregion

    #region Démarrage du combat
    public IEnumerator StartBattle()
    {
        Debug.Log("[BattleTurnManager] Démarrage du combat");

        //0 Liste "unitsInBattle" construite avec SpawnAll

        //1 Filtrer pour ne garder que les unités dont les HP sont > 0
        activeCharacterUnits = ReturnActiveUnits();

        //2 Initialise l’UI de timeline
        InitializeTimelineUI(unitsInBattle);

        //3 Affecter currentTarget au premier ennemi de la liste
        SetDefaultCurrentTarget();

        //4 Réinitialise les ATB
        ResetAllATB();

        //5 Détermine quel joueur joue en premier
        CharacterUnit firstPlayer = ReturnFirstStrikeCharacter();

        //6 Joue la séquence de premier tour
        if (firstPlayer != null && firstStrikeCameraPath != null)
        {
            yield return FirstStrikeSequenceRoutine(firstPlayer);
        }

        //7 Démarre la boucle de tours
        StartCoroutine(TurnLoop());

        //// Change l’état du jeu
        //GameManager.Instance.ChangeGameState(GameState.StartBattle);
    }

    //1 Filtrer pour ne garder que les unités dont les HP sont > 0
    private List<CharacterUnit> ReturnActiveUnits()
    {
        List<CharacterUnit> activeCharacterUnits = unitsInBattle.Where(c => c.currentHP > 0).ToList();
        return activeCharacterUnits;
    }

    //2 Initialise l’UI de timeline
    private void InitializeTimelineUI(List<CharacterUnit> characters)
    {
        foreach (var go in timelineUIObjects)
            Destroy(go.gameObject);
        timelineUIObjects.Clear();

        foreach (var unit in characters)
        {
            var slot = Instantiate(timelineUnitPrefab, timelineContainer);
            var ui = slot.GetComponent<BattleTimelineUnit>();
            ui.Initialize(unit);
            timelineUIObjects.Add(ui);
        }
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
    private CharacterUnit ReturnFirstStrikeCharacter()
    {
        CharacterUnit firstPlayer = activeCharacterUnits
            .Where(u => u.Data.isPlayerControlled)
            .OrderByDescending(u => u.currentInitiative)
            .FirstOrDefault();

        return firstPlayer;
    }

    //6 Joue la séquence de premier tour
    private IEnumerator FirstStrikeSequenceRoutine(CharacterUnit unit)
    {
        ChangeBattleState(BattleState.FirstStrikeSequence);
        Transform target = FindChildRecursive(unit.transform, "spine_03");
        Transform position = FindChildRecursive(unit.transform, "spine_03");
        CameraController.Instance.StartPathFollow(
            firstStrikeCameraPath,
            position,
            forceLook: true,
            targetToLook: target,
            alignImmediately: false
        );
        yield return new WaitForSeconds(firstStrikeCameraPath.GetTotalDuration());
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

            yield return ExecuteTurn(CalculateNextUnit());
            yield return new WaitForSeconds(0.2f);
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

        ChangeCurrentCharacterUnit(characterUnit);

        // S'assure que la BattleCamera peut se déplacer librement
        CameraController cc = CameraController.Instance;
        if (cc != null && cc.currentWorldCameraState != WorldCameraState.ResearchClosestCamPoint)
            cc.ReleaseCam();

        if (characterUnit.Data.characterType == CharacterType.SquadUnit)
            ChangeBattleState(BattleState.SquadUnit_MainMenu);
        else if (characterUnit.Data.characterType == CharacterType.EnemyUnit)
            ChangeBattleState(BattleState.EnemyUnit_Reflexion);

        SetupCurrentUnitMenus(); // prépare les panels de l’unité
        ShowMainMenu(); // montre le menu principal

        InputsManager.Instance.playerInputs.Battle.Enable();
        OrientAllUnitsTowardEnemyGroupSmooth();
    }

    private IEnumerator ExecuteTurn(CharacterUnit unit)
    {
        if (currentBattleState != BattleState.VictoryScreen_Await && currentBattleState != BattleState.VictoryScreen_CanContinue && currentBattleState != BattleState.GameOverScreen_Await && currentBattleState != BattleState.GameOverScreen_CanContinue)
        {
            if (unit.TryGetComponent<FatigueSystem>(out var fatigue) && fatigue.IsAsleep)
            {
                EndTurn();
                yield break;
            }
            isTurnResolving = true;

            // 1) On stocke l’unité qui jouait juste avant (champ de classe)
            CharacterUnit oldUnit = previousUnit;

            // 2) Mise à jour de l’unité courante
            currentCharacterUnit = unit;
            UpdateTimelineHighlight(unit);

            ChangeBattleState(BattleState.NewTurn);

            Debug.Log($"[BattleTurnManager] Tour de {unit.name} (ATB: {unit.currentATB})");
            OrientAllUnitsTowardEnemyGroupSmooth();

            yield return new WaitForSeconds(0.5f);

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

    private void UpdateTimelineHighlight(CharacterUnit activeUnit)
    {
        foreach (var ui in timelineUIObjects)
        {
            bool isCurrent = activeUnit != null && ui.characterData == activeUnit.Data;
            ui.SetHighlight(isCurrent);
        }
    }

    public void RemoveFromTimeline(CharacterUnit deadUnit)
    {
        activeCharacterUnits.Remove(deadUnit);

        var ui = timelineUIObjects.FirstOrDefault(x => x.characterData == deadUnit.Data);
        if (ui != null)
        {
            timelineUIObjects.Remove(ui);
            Destroy(ui.gameObject);
        }
    }

    private IEnumerator EnemyTurnWithQTE(CharacterUnit enemy)
    {
        ChangeBattleState(BattleState.EnemyUnit_PerformingMusicalMove);
        yield return new WaitForSeconds(1f);

        var move = enemy.GetRandomMusicalAttack();
        var target = enemy.SelectTargetFromSquad();

        _currentTargetCharacter = target;

        if (move == null || target == null)
        {
            Debug.LogWarning("[EnemyTurn] Aucune attaque ou cible valide !");
            yield break;
        }

        ActionUIDisplayManager.Instance.DisplayAttackName(move.moveName);
        yield return RhythmQTEManager.Instance.MusicalMoveRoutine(move, enemy, target);
    }

    public IEnumerator ExecuteMoveOnTarget(MusicalMoveSO move, CharacterUnit caster, CharacterUnit target)
    {
        Debug.Log($"{caster} exécute le mouvement {move.moveName} sur {target}");
        ToggleMenuContainers(false, false, false);
        if (!HasSpaceForMove(caster, target, move))
        {
            Debug.LogWarning("[ExecuteMoveOnTarget] Pas assez d'espace pour executer le mouvement.");
            yield break;
        }
        var interceptor = CheckForInterception(caster, target, caster.Data.currentInterceptionRange);
        if (interceptor != null)
        {
            yield return InterceptRoutine(interceptor, caster);
            yield break;
        }
        yield return RhythmQTEManager.Instance.MusicalMoveRoutine(move, caster, target);
        move.ApplyEffect(caster, target);

        // Ajout du système de rage manuellement
        var rage = caster.GetComponent<RageSystem>();
        if (rage != null && move.effectType == MusicalEffectType.Damage)
        {
            float bonus = rage.CalculateBonusDamage();
            if (bonus > 0)
            {
                target.TakeDamage(bonus);
            }
        }

        currentCharacterUnit.currentATB = 0f;
    }

    public IEnumerator UseItemOnTarget(ItemData item, CharacterUnit caster, CharacterUnit target)
    {
        InventoryManager.Instance.UseItem(item, target);
        caster.GetComponent<FatigueSystem>()?.OnActionPerformed();
        currentCharacterUnit.currentATB = 0f;
        yield return null;
    }

    private bool HasSpaceForMove(CharacterUnit caster, CharacterUnit target, MusicalMoveSO move)
    {
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
        Collider[] hits = Physics.OverlapSphere(destination, 0.5f);
        foreach (var h in hits)
        {
            CharacterUnit cu = h.GetComponentInParent<CharacterUnit>();
            if (cu != null && cu != caster && cu != target)
                return false;
        }
        return true;
    }

    private CharacterUnit CheckForInterception(CharacterUnit caster, CharacterUnit target, float range)
    {
        foreach (var unit in activeCharacterUnits)
        {
            if (unit == null || unit == caster || unit == target) continue;
            if (unit.Data.isPlayerControlled == caster.Data.isPlayerControlled) continue;

            if (Vector3.Distance(unit.transform.position, caster.transform.position) <= range)
            {
                float chance = unit.currentReflex / (unit.currentReflex + caster.currentReflex + 1f);
                if (Random.value < chance)
                    return unit;
            }
        }
        return null;
    }

    private IEnumerator InterceptRoutine(CharacterUnit interceptor, CharacterUnit caster)
    {
        if (interceptor == null) yield break;

        var move = interceptor.GetRandomMusicalAttack();
        if (move != null)
        {
            ActionUIDisplayManager.Instance.DisplayAttackName(move.moveName);
            yield return RhythmQTEManager.Instance.MusicalMoveRoutine(move, interceptor, caster);
            move.ApplyEffect(interceptor, caster);
        }
    }

    public void EndTurn()
    {
        if (currentCharacterUnit != null)
        {
            Debug.Log($"[BattleTurnManager] Fin du tour de {currentCharacterUnit.name}");
            currentCharacterUnit.currentATB = 0f;
        }

        ChangeBattleState(BattleState.EndTurn);
        UpdateTimelineHighlight(null);
        isTurnResolving = false;
        HandleEndOfBattle();
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
                Animator anim = unit.GetComponentInChildren<Animator>();
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
            Vector3 direction = (averagePosition - unit.transform.position).normalized;
            if (direction == Vector3.zero)
                continue;

            // Calcul de l’angle entre l’orientation actuelle et la cible
            float angle = Vector3.Angle(unit.transform.forward, direction);
            if (angle > 90f)
            {
                // Si > 90°, déclenche "isTurning" sur l’Animator enfant
                Animator anim = unit.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    anim.SetTrigger("isTurning");
                }
            }

            // Lance la rotation en douceur vers targetRotation
            Quaternion targetRotation = Quaternion.LookRotation(direction);
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
        // Tant que l’angle entre la rotation actuelle et la target est > 0.5f
        while (Quaternion.Angle(unit.transform.rotation, targetRotation) > 0.5f)
        {
            unit.transform.rotation = Quaternion.RotateTowards(
                unit.transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
            yield return null;
        }

        // Orientation finale propre (uniquement sur l’axe Y)
        unit.transform.rotation = Quaternion.Euler(0, targetRotation.eulerAngles.y, 0);
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

        if (allEnemiesDead)
        {
            Debug.Log("[BattleTurnManager] 🎉 Tous les ennemis sont vaincus !");
            ChangeBattleState(BattleState.VictoryScreen_Await);
            StartCoroutine(ReduceTimeAndShowVictoryPanel());
        }
        else if (allSquadDead)
        {
            Debug.Log("[BattleTurnManager] 💀 Tous les alliés sont morts...");
            ChangeBattleState(BattleState.GameOverScreen_Await);
            StartCoroutine(ShowGameOverPanel());
        }
    }

    public void TakeVictoryScreenshot()
    {
        if (VictoryScreenImage == null)
        {
            Debug.LogError("VictoryScreenImage n'est pas assigné !");
            return;
        }

        Camera battleCamera = GameObject.FindGameObjectWithTag("BattleCamera")?.GetComponent<Camera>();
        if (battleCamera == null)
        {
            Debug.LogError("Aucune caméra trouvée pour la capture !");
            return;
        }

        // Assure-toi que la caméra utilise la render texture pour la capture
        RenderTexture prevRT = battleCamera.targetTexture;
        battleCamera.targetTexture = VictoryScreenImage;
        battleCamera.Render();
        battleCamera.targetTexture = prevRT;

        Debug.Log("Screenshot de victoire capturé !");
    }

    private IEnumerator ReduceTimeAndShowVictoryPanel()
    {
        float transitionDuration = 0.3f;
        float t = 0f;

        while (t < transitionDuration)
        {
            t += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(1f, 0.05f, t / transitionDuration);
            Time.fixedDeltaTime = Time.timeScale * 0.02f;
            yield return null;
        }

        Time.timeScale = 0.05f;
        Time.fixedDeltaTime = 0.001f;

        TakeVictoryScreenshot(); // 👈 Capture ici immédiatement

        // Optionnel : attendre encore un peu avant d’afficher le panneau
        yield return new WaitForSecondsRealtime(0.1f); // Soumis à timeScale

        //Prendre une photo de la dernière frame de la mort de l'ennemi avant VictoryScreen

        victoryScreen.transform.GetChild(0).gameObject.SetActive(true);

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

        CleanupAllSpawnedUnits();

        ChangeBattleState(BattleState.VictoryScreen_CanContinue);
    }

    IEnumerator ShowGameOverPanel()
    {
        yield return new WaitForSeconds(0.5f); // Attente pour la transition
        gameOverScreen.transform.GetChild(0).gameObject.SetActive(true);
    }

    private void CleanupAllSpawnedUnits()
    {
        foreach (var unit in unitsInBattle)
            if (unit != null)
                Destroy(unit.gameObject);

        unitsInBattle.Clear();
    }
    #endregion

    #region Gestion de l’ouverture des menus

    private void SetupCurrentUnitMenus()
    {
        // 1) Essaye de récupérer la BattleCamera par tag
        Transform battleCamera = GameObject.FindGameObjectWithTag("BattleCamera").transform;

        // 2) Cherche le panneau MainMenu_Panel, de façon récursive (plutôt que Find("MainMenu_Panel"))
        Transform mainPanel = FindChildRecursive(battleCamera, "MainMenu_Panel");
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
        Transform skillsPanel = FindChildRecursive(battleCamera, "SkillsMenu_Panel");
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
        }

        // 4) Panneau ItemsMenu_Panel
        Transform itemsPanel = FindChildRecursive(battleCamera, "ItemsMenu_Panel");
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
        ActionUIDisplayManager.Instance.DisplayInstruction_SelectItemOrSkill();
        ChangeBattleState(BattleState.SquadUnit_MainMenu);
        ToggleMenuContainers(true, false, false);

        Debug.Log($"[ShowMainMenu] Nombre de slots = {currentMainMenuSlots.Count}");
        for (int i = 0; i < currentMainMenuSlots.Count; i++)
            Debug.Log($"Slot {i} = {currentMainMenuSlots[i].name}, enfants : {currentMainMenuSlots[i].childCount}");

        UpdateButton(currentMainMenuSlots[0], "Compétences", null);
        UpdateButton(currentMainMenuSlots[1], "Objet", null);
    }

    public void OpenSkillsMenu()
    {
        ActionUIDisplayManager.Instance.DisplayInstruction_SelectSkill();
        ChangeBattleState(BattleState.SquadUnit_SkillsMenu);
        ToggleMenuContainers(false, true, false);
        currentMenuIndex = 0;

        skillChoices = currentCharacterUnit.Data.musicalAttacks.ToList();

        // 7) Création des boutons de compétences
        for (int i = 0; i < skillChoices.Count && i < currentSkillsMenuSlots.Count; i++)
        {
            var move = skillChoices[i];
            UpdateButton(currentSkillsMenuSlots[i], move.moveName, move.moveIcon);
        }
        // Indique les emplacements vides
        for (int j = skillChoices.Count; j < currentSkillsMenuSlots.Count; j++)
        {
            if (emptyMove != null)
                UpdateButton(currentSkillsMenuSlots[j], emptyMove.moveName, emptyMove.moveIcon);
            else
                UpdateButton(currentSkillsMenuSlots[j], "Indisponible", null);
        }
    }

    public void OpenItemMenu()
    {
        ActionUIDisplayManager.Instance.DisplayInstruction_SelectItem();
        ChangeBattleState(BattleState.SquadUnit_ItemsMenu);
        ToggleMenuContainers(false, false, true);
        currentMenuIndex = 0;

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
        if (currentCharacterUnit.Data == null) return;

        Transform battleCamera = GameObject.FindGameObjectWithTag("BattleCamera").transform;

        battleCamera.transform.GetChild(0).GetChild(0).gameObject.SetActive(showMain);
        battleCamera.transform.GetChild(0).GetChild(1).gameObject.SetActive(showSkills);
        battleCamera.transform.GetChild(0).GetChild(2).gameObject.SetActive(showItems);
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
    #endregion

    #region Gestion de la navigation dans les menus
    private void HandleTargetNavigation()
    {
        bool isEnemyTargeting = currentBattleState == BattleState.SquadUnit_TargetSelectionAmongEnemiesForSkill ||
                                currentBattleState == BattleState.SquadUnit_TargetSelectionAmongEnemiesForItem;

        bool isSquadTargeting = currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadForSkill ||
                                currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadForItem;

        if (!isEnemyTargeting && !isSquadTargeting)
            return;

        CharacterType requiredType = isEnemyTargeting ? CharacterType.EnemyUnit : CharacterType.SquadUnit;

        // 🔍 DEBUG : ce que contient activeCharacterUnits
        Debug.Log($"[TargetNav] Active Units = {activeCharacterUnits.Count}");
        foreach (var unit in activeCharacterUnits)
        {
            Debug.Log($"[TargetNav] Unit = {unit.name}, type = {unit.characterType}, active = {unit.gameObject.activeInHierarchy}");
        }

        // 🔧 Ajustement pour tests : enlève gameObject.activeInHierarchy pour isoler le souci
        filteredUnits = activeCharacterUnits
            .Where(u => u.characterType == requiredType) // Retire le test d'activité temporairement
            .Take(3)
            .ToList();

        if (filteredUnits.Count == 0)
        {
            Debug.LogWarning("[TargetNav] Aucune unité filtrée trouvée !");
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
        //if (currentTargetCharacter != null && currentTargetCharacter.currentHP <= 0)
        //{
        //    targetCursor.SetActive(false);
        //    return;
        //}

        if (currentBattleState == BattleState.SquadUnit_TargetSelectionAmongEnemiesForSkill || currentBattleState == BattleState.SquadUnit_TargetSelectionAmongEnemiesForItem)
        {
            if (targetCursor != null && currentTargetCharacter != null)
            {
                targetCursor.SetActive(true);
                targetCursor.transform.position = currentTargetCharacter.transform.position;
            }
        }
        else
        {
            if (targetCursor != null)
            {
                targetCursor.transform.position = Vector3.zero;
                targetCursor.SetActive(false);
            }
        }
    }

    public void HandleTargetSelection(MusicalMoveSO move)
    {
        switch (move.defaultTargetType)
        {
            case TargetType.Self:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadForSkill);

                currentTargetCharacter = activeCharacterUnits
                    .FirstOrDefault(u => u.characterType == CharacterType.SquadUnit && u.currentHP > 0);
                currentTargetIndex = 0;
                break;

            case TargetType.SingleEnemy:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongEnemiesForSkill);

                currentTargetCharacter = activeCharacterUnits
                    .FirstOrDefault(u => u.characterType == CharacterType.EnemyUnit && u.currentHP > 0);
                currentTargetIndex = 0;
                break;

            case TargetType.AllEnemies:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies);

                currentTargetCharacter = activeCharacterUnits
                    .FirstOrDefault(u => u.characterType == CharacterType.SquadUnit && u.currentHP > 0);
                currentTargetIndex = 0;
                break;
                break;

            case TargetType.SingleAlly:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadForSkill);
                break;

                currentTargetCharacter = activeCharacterUnits
                    .FirstOrDefault(u => u.characterType == CharacterType.SquadUnit && u.currentHP > 0);
                currentTargetIndex = 0;
                break;

            case TargetType.AllAllies:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad);

                currentTargetCharacter = activeCharacterUnits
                    .FirstOrDefault(u => u.characterType == CharacterType.SquadUnit && u.currentHP > 0);
                currentTargetIndex = 0;
                break;
                break;

            default:
                Debug.LogWarning($"[BattleTurnManager] Type de cible par défaut non géré : {move.defaultTargetType}");
                return;
        }
    }

    public void HandleTargetSelection(ItemData item)
    {
        switch (item.defaultTargetType)
        {
            case TargetType.Self:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadForItem);

                currentTargetCharacter = activeCharacterUnits
                    .FirstOrDefault(u => u.characterType == CharacterType.SquadUnit && u.currentHP > 0);
                currentTargetIndex = 0;
                break;

            case TargetType.SingleEnemy:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongEnemiesForItem);

                currentTargetCharacter = activeCharacterUnits
                    .FirstOrDefault(u => u.characterType == CharacterType.SquadUnit && u.currentHP > 0);
                currentTargetIndex = 0;
                break;

            case TargetType.AllEnemies:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies);
                currentTargetCharacter = activeCharacterUnits
                    .FirstOrDefault(u => u.characterType == CharacterType.SquadUnit && u.currentHP > 0);
                currentTargetIndex = 0;
                break;

            case TargetType.SingleAlly:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadForItem);
                currentTargetCharacter = activeCharacterUnits
                    .FirstOrDefault(u => u.characterType == CharacterType.SquadUnit && u.currentHP > 0);
                currentTargetIndex = 0;
                break;

            case TargetType.AllAllies:
                ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad);
                currentTargetCharacter = activeCharacterUnits
                    .FirstOrDefault(u => u.characterType == CharacterType.SquadUnit && u.currentHP > 0);
                currentTargetIndex = 0;
                break;

            default:
                Debug.LogWarning($"[BattleTurnManager] Type de cible par défaut non géré : {item.defaultTargetType}");
                return;
        }

        ActionUIDisplayManager.Instance.DisplayInstruction_SelectTarget();
    }
    #endregion

    #region Gestion des mouvements de la caméra de combat
    public void UpdateCameraBehaviour(BattleState newState)
    {
        if (battleCameraTransform == null)
        {
            battleCameraTransform = GameObject.FindGameObjectWithTag("BattleCamera")?.transform;
            if (battleCameraTransform == null)
            {
                Debug.LogError("[BattleCameraManager] Aucune caméra de combat trouvée !");
                return;
            }
        }

        switch (currentBattleState)
        {
            case BattleState.Initialization:
                CameraPath cP = GameObject.Find("BattleScene_Camera_BattleIntro").GetComponent<CameraPath>();
                if(!cP.IsPlaying && !cP.triggered)
                {
                    cP.PlaySequence();
                }
                isFollowingCurrentTarget = false;
                desiredTransform = null;
                break;
            case BattleState.FirstStrikeSequence:
                isFollowingCurrentTarget = false;
                desiredTransform = null;
                break;
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
                isFollowingCurrentTarget = false;
                desiredTransform = GameObject.Find("Camera_FocusSquad").transform;
                break;

            case BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies:
                isFollowingCurrentTarget = false;
                desiredTransform = GameObject.Find("Camera_FocusEnemies").transform;
                break;

            case BattleState.SquadUnit_TargetSelectionAmongSquadForSkill:
                isFollowingCurrentTarget = false;
                desiredTransform = FindChildRecursive(currentTargetCharacter.transform, "Camera_TargetedPoint");
                break;

            case BattleState.SquadUnit_TargetSelectionAmongSquadForItem:
                isFollowingCurrentTarget = false;
                desiredTransform = FindChildRecursive(currentTargetCharacter.transform, "Camera_TargetedPoint");
                break;

            case BattleState.SquadUnit_TargetSelectionAmongEnemiesForSkill:
                isFollowingCurrentTarget = false;
                desiredTransform = FindChildRecursive(currentTargetCharacter.transform, "Camera_TargetedPoint");
                break;

            case BattleState.SquadUnit_TargetSelectionAmongEnemiesForItem:
                isFollowingCurrentTarget = false;
                desiredTransform = FindChildRecursive(currentTargetCharacter.transform, "Camera_TargetedPoint");
                break;

            case BattleState.SquadUnit_PerformingMusicalMove:
                isFollowingCurrentTarget = true;
                desiredTransform = FindChildRecursive(currentCharacterUnit.transform, "Camera_Move_1");
                break;

            case BattleState.EnemyUnit_PerformingMusicalMove:
                isFollowingCurrentTarget = true;
                desiredTransform = FindChildRecursive(currentCharacterUnit.transform, "Camera_Move_1");
                break;
            case BattleState.SquadUnit_UseItem:
                isFollowingCurrentTarget = true;
                desiredTransform = FindChildRecursive(currentCharacterUnit.transform, "Camera_Move_1");
                break;
            case BattleState.EnemyUnit_UseItem:
                isFollowingCurrentTarget = true;
                desiredTransform = FindChildRecursive(currentCharacterUnit.transform, "Camera_Move_1");
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
        if (battleCameraTransform == null)
        {
            battleCameraTransform = GameObject.FindGameObjectWithTag("BattleCamera")?.transform;
            if (battleCameraTransform == null)
            {
                Debug.LogError("[BattleCameraManager] Aucune caméra de combat trouvée !");
                return;
            }
        }

        CameraController cc = CameraController.Instance;
        if (cc != null && (cc.IsFollowingPath || cc.currentWorldCameraState != WorldCameraState.ResearchClosestCamPoint))
        {
            return; // Laisse la main au CameraController pour éviter les conflits
        }

        if (isFollowingCurrentTarget && currentCharacterUnit != null && currentTargetCharacter != null)
        {
            Vector3 midPoint = (currentCharacterUnit.transform.position + currentTargetCharacter.transform.position) / 2f;
            Vector3 offset = Vector3.up * 3f - currentCharacterUnit.transform.forward * 5f;

            Vector3 targetPosition = midPoint + offset;
            Quaternion targetRotation = Quaternion.LookRotation(midPoint - battleCameraTransform.position);

            battleCameraTransform.position = Vector3.Lerp(battleCameraTransform.position, targetPosition, Time.deltaTime * cameraSmoothSpeed);
            battleCameraTransform.rotation = Quaternion.Slerp(battleCameraTransform.rotation, targetRotation, Time.deltaTime * cameraSmoothSpeed);
        }
        else if (desiredTransform != null)
        {
            battleCameraTransform.position = Vector3.Lerp(battleCameraTransform.position, desiredTransform.position, Time.deltaTime * cameraSmoothSpeed);
            battleCameraTransform.rotation = Quaternion.Slerp(battleCameraTransform.rotation, desiredTransform.rotation, Time.deltaTime * cameraSmoothSpeed);
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

    void EnsureTargetCursor()
    {
        if (targetCursorPrefab != null)
        {
            targetCursor = Instantiate(targetCursorPrefab, transform.position, Quaternion.identity);
            targetCursor.SetActive(false);
        }
    }

    public void ResetBattleInfos()
    {
        // Réinitialise l’état du combat
        ChangeBattleState(BattleState.None);

        // Nettoie les références
        currentCharacterUnit = null;
        unitsInBattle.Clear();
        activeCharacterUnits.Clear();

        // Réinitialisation UI timeline
        //foreach (var ui in timelineUIObjects)
        //{
        //    Destroy(ui.gameObject);
        //    timelineUIObjects.Clear();
        //}

        // Réinitialise le curseur cible si existant
        if (targetCursor != null)
        {
            Destroy(targetCursor);
        }
    }
    #endregion
}
