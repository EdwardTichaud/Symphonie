using UnityEngine;
using UnityEngine.InputSystem;
using UnityEditor;
using System.Collections;

public class InputsManager : MonoBehaviour
{
    public static InputsManager Instance { get; private set; }
    public PlayerInputs playerInputs;
    private CharacterController3D controller;

    [Header("Pass Turn")]
    public PassTurnUI passTurnUI;
    public float passHoldDuration = 2f;
    public Pulse passTurnPulse;
    private Coroutine passRoutine;

    private InputActionMap[] allMaps;

    #region Initialisation
    /// <summary>
    /// Instancie l'asset d'inputs et configure le singleton.
    /// </summary>
    void Awake()
    {
        playerInputs = new PlayerInputs();

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        allMaps = new[]
        {
            playerInputs.World.Get(),
            playerInputs.Inventory.Get(),
            playerInputs.Battle.Get(),
            playerInputs.Munin.Get(),
            playerInputs.InfoBox.Get()
        };
    }

    /// <summary>
    /// Active le mapping World au démarrage et trouve le contrôleur.
    /// </summary>
    void Start()
    {
        ActivateOnly(playerInputs.World.Get());
        SetInputs();
        controller = FindFirstObjectByType<CharacterController3D>();
    }

    /// <summary>
    /// Abonne les actions aux différents callbacks.
    /// </summary>
    public void SetInputs()
    {
        var battle = playerInputs.Battle;
        battle.Select1.performed += OnSelect1;
        battle.Select2.performed += OnSelect2;
        battle.Select3.performed += OnSelect3;
        battle.Back.started += OnBackStarted;
        battle.Back.performed += OnBackInput;
        battle.Back.canceled += OnBackCanceled;
        battle.Confirm.performed += OnConfirm;
        battle.EnemiesGroupSelection.performed += OnEnemiesGroupSelection;
        battle.SquadGroupSelection.performed += OnSquadGroupSelection;

        var world = playerInputs.World;
        world.ForceCam.performed += OnForceCamInput;

    }

    /// <summary>
    /// Désabonne tous les callbacks des actions.
    /// </summary>
    public void ResetInputs()
    {
        var battle = playerInputs.Battle;
        battle.Select1.performed -= OnSelect1;
        battle.Select2.performed -= OnSelect2;
        battle.Select3.performed -= OnSelect3;
        battle.Back.started -= OnBackStarted;
        battle.Back.performed -= OnBackInput;
        battle.Back.canceled -= OnBackCanceled;
        battle.Confirm.performed -= OnConfirm;
        battle.EnemiesGroupSelection.performed -= OnEnemiesGroupSelection;
        battle.SquadGroupSelection.performed -= OnSquadGroupSelection;

        var world = playerInputs.World;
        world.ForceCam.performed -= OnForceCamInput;
    }

    /// <summary>
    /// Active uniquement les maps données et désactive les autres.
    /// </summary>
    public void ActivateOnly(params InputActionMap[] mapsToEnable)
    {
        // 1) on désactive tout
        foreach (var m in allMaps)
            m.Disable();

        // 2) on ré-active le sous-ensemble voulu
        foreach (var m in mapsToEnable)
            m.Enable();
    }

    #endregion

    #region Inputs
    /// <summary>
    /// Callback de validation des actions de combat.
    /// </summary>
    private void OnConfirm(InputAction.CallbackContext ctx)
    {
        NewBattleManager bm = NewBattleManager.Instance;
        if (bm.currentBattleState == BattleState.SquadUnit_TargetSelectionAmongEnemiesForSkill
            || bm.currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadForSkill
            //|| bm.currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad
            //|| bm.currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies
            )
        {
            if (!bm.IsTargetInRange(bm.currentCharacterUnit, bm.currentTargetCharacter, bm.currentMove))
            {
                ActionUIDisplayManager.Instance.DisplayInstruction_TargetTooFar();
                return;
            }

            bm.ChangeBattleState(BattleState.SquadUnit_PerformingMusicalMove);
            bm.StartCoroutine(bm.ExecuteMoveOnTarget(bm.currentMove, bm.currentCharacterUnit, bm.currentTargetCharacter));
            bm.ToggleMenuContainers(false, false, false);
        }
        else if (bm.currentBattleState == BattleState.SquadUnit_TargetSelectionAmongEnemiesForItem
            || bm.currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadForItem)
            {
            if (!bm.IsTargetInRange(bm.currentCharacterUnit, bm.currentTargetCharacter, bm.currentItem))
            {
                ActionUIDisplayManager.Instance.DisplayInstruction_TargetTooFar();
                return;
            }

            if (bm.currentItem != null && bm.currentItem.itemTargetingAnimation != null)
                bm.currentCharacterUnit.GetComponentInChildren<Animator>()
                    .Play(bm.currentItem.itemTargetingAnimation.name);

            bm.ChangeBattleState(BattleState.SquadUnit_Item_Use);
            bm.StartCoroutine(bm.UseItemOnTarget(bm.currentItem, bm.currentCharacterUnit, bm.currentTargetCharacter));
            bm.ToggleMenuContainers(false, false, false);
        }

        if (bm.currentBattleState == BattleState.VictoryScreen_CanContinue)
        {
            bm.ChangeBattleState(BattleState.None);
            BattleTransitionManager.Instance.StartCoroutine(BattleTransitionManager.Instance.ExitVictoryScreenAndBattle());
        }
    }

    /// <summary>
    /// Sélectionne l'option 1 dans les menus.
    /// </summary>
    private void OnSelect1(InputAction.CallbackContext ctx)
    {
        NewBattleManager bm = NewBattleManager.Instance;

        if (bm.currentBattleState == BattleState.SquadUnit_MainMenu)
        {
            bm.OpenSkillsMenu();
        }
        else if (bm.currentBattleState == BattleState.SquadUnit_SkillsMenu)
        {
            if (bm.skillChoices.Count > 0)
            {
                bm.currentMove = bm.skillChoices[0];
                if (bm.currentCharacterUnit.GetHarmonicCount(bm.currentCharacterUnit.Data.harmonicType) < bm.currentMove.harmonicCost)
                {
                    ActionUIDisplayManager.Instance.DisplayInstruction_NotEnoughHarmonics();
                    bm.ShowMainMenu();
                    return;
                }
                bm.ToggleMenuContainers(false, false, false);
                bm.HandleTargetSelection(bm.currentMove);

                if (bm.currentMove.musicalMoveTargetingAnimation != null)
                    bm.currentCharacterUnit.GetComponentInChildren<Animator>().Play(bm.currentMove.musicalMoveTargetingAnimation.name);
            }
            else
            {
                Debug.LogWarning("[InputsManager] OnSelect1 ignoré : pas de skill disponible !");
            }
        }
        else if (bm.currentBattleState == BattleState.SquadUnit_ItemsMenu)
        {
            if (bm.itemChoices.Count > 0)
            {
                bm.currentItem = bm.itemChoices[0];
                bm.ToggleMenuContainers(false, false, false);
                bm.HandleTargetSelection(bm.currentItem);

                if (bm.currentItem.itemTargetingAnimation != null)
                    bm.currentCharacterUnit.GetComponentInChildren<Animator>().Play(bm.currentItem.itemTargetingAnimation.name);
            }
            else
            {
                Debug.LogWarning("[InputsManager] OnSelect1 ignoré : pas d'item disponible !");
            }
        }
    }

    /// <summary>
    /// Sélectionne l'option 2 dans les menus.
    /// </summary>
    private void OnSelect2(InputAction.CallbackContext ctx)
    {
        NewBattleManager bm = NewBattleManager.Instance;

        if (bm.currentBattleState == BattleState.SquadUnit_MainMenu)
        {
            bm.OpenItemMenu();
        }
        else if (bm.currentBattleState == BattleState.SquadUnit_SkillsMenu)
        {
            if (bm.skillChoices.Count > 1)
            {
                bm.currentMove = bm.skillChoices[1];
                if (bm.currentCharacterUnit.GetHarmonicCount(bm.currentCharacterUnit.Data.harmonicType) < bm.currentMove.harmonicCost)
                {
                    ActionUIDisplayManager.Instance.DisplayInstruction_NotEnoughHarmonics();
                    bm.ShowMainMenu();
                    return;
                }
                bm.ToggleMenuContainers(false, false, false);
                bm.HandleTargetSelection(bm.currentMove);

                if (bm.currentMove.musicalMoveTargetingAnimation != null)
                    bm.currentCharacterUnit.GetComponentInChildren<Animator>().Play(bm.currentMove.musicalMoveTargetingAnimation.name);
            }
            else
            {
                Debug.LogWarning("[InputsManager] OnSelect2 ignoré : pas assez de skills !");
            }
        }
        else if (bm.currentBattleState == BattleState.SquadUnit_ItemsMenu)
        {
            if (bm.itemChoices.Count > 1)
            {
                bm.currentItem = bm.itemChoices[1];
                bm.ToggleMenuContainers(false, false, false);
                bm.HandleTargetSelection(bm.currentItem);

                if (bm.currentItem.itemTargetingAnimation)
                    bm.currentCharacterUnit.GetComponentInChildren<Animator>().Play(bm.currentItem.itemTargetingAnimation.name);
            }
            else
            {
                Debug.LogWarning("[InputsManager] OnSelect2 ignoré : pas assez d'items !");
            }
        }
    }

    /// <summary>
    /// Sélectionne l'option 3 dans les menus.
    /// </summary>
    private void OnSelect3(InputAction.CallbackContext ctx)
    {
        NewBattleManager bm = NewBattleManager.Instance;

        if (bm.currentBattleState == BattleState.SquadUnit_SkillsMenu)
        {
            if (bm.skillChoices.Count > 2)
            {
                bm.currentMove = bm.skillChoices[2];
                if (bm.currentCharacterUnit.GetHarmonicCount(bm.currentCharacterUnit.Data.harmonicType) < bm.currentMove.harmonicCost)
                {
                    ActionUIDisplayManager.Instance.DisplayInstruction_NotEnoughHarmonics();
                    bm.ShowMainMenu();
                    return;
                }
                bm.ToggleMenuContainers(false, false, false);
                bm.HandleTargetSelection(bm.currentMove);

                if (bm.currentMove.musicalMoveTargetingAnimation != null)
                    bm.currentCharacterUnit.GetComponentInChildren<Animator>().Play(bm.currentMove.musicalMoveTargetingAnimation.name);
            }
            else
            {
                Debug.LogWarning("[InputsManager] OnSelect3 ignoré : pas assez de skills !");
            }
        }
        else if (bm.currentBattleState == BattleState.SquadUnit_ItemsMenu)
        {
            if (bm.itemChoices.Count > 2)
            {
                bm.currentItem = bm.itemChoices[2];
                bm.ToggleMenuContainers(false, false, false);
                bm.HandleTargetSelection(bm.currentItem);

                if (bm.currentItem.itemTargetingAnimation != null)
                    bm.currentCharacterUnit.GetComponentInChildren<Animator>().Play(bm.currentItem.itemTargetingAnimation.name);
            }
            else
            {
                Debug.LogWarning("[InputsManager] OnSelect3 ignoré : pas assez d'items !");
            }
        }
    }

    private void OnBackInput(InputAction.CallbackContext ctx)
    {
        NewBattleManager bm = NewBattleManager.Instance;

        if (bm.currentBattleState == BattleState.SquadUnit_SkillsMenu ||
            bm.currentBattleState == BattleState.SquadUnit_ItemsMenu)
        {
            bm.ShowMainMenu();
            return;
        }

        if (IsSkillTargetSelectionState(bm.currentBattleState))
        {
            bm.OpenSkillsMenu();
            bm.currentCharacterUnit.GetComponentInChildren<Animator>().SetTrigger("exitAction");
        }
        else if (IsItemTargetSelectionState(bm.currentBattleState))
        {
            bm.OpenItemMenu();
        }
    }

    private bool IsSkillTargetSelectionState(BattleState state)
    {
        return state == BattleState.SquadUnit_TargetSelectionAmongEnemiesForSkill ||
               state == BattleState.SquadUnit_TargetSelectionAmongSquadForSkill ||
               (state == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad && NewBattleManager.Instance.currentMove != null) ||
               (state == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies && NewBattleManager.Instance.currentMove != null);
    }

    private bool IsItemTargetSelectionState(BattleState state)
    {
        return state == BattleState.SquadUnit_TargetSelectionAmongEnemiesForItem ||
               state == BattleState.SquadUnit_TargetSelectionAmongSquadForItem ||
               (state == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad && NewBattleManager.Instance.currentItem != null) ||
               (state == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies && NewBattleManager.Instance.currentItem != null);
    }

    private void OnBackStarted(InputAction.CallbackContext ctx)
    {
        NewBattleManager bm = NewBattleManager.Instance;
        if (bm.currentBattleState == BattleState.SquadUnit_MainMenu && passRoutine == null)
        {
            if (passTurnPulse != null)
                passTurnPulse.pulseSpeed = 0f;
            passRoutine = StartCoroutine(PassTurnRoutine());
        }
    }

    private void OnBackCanceled(InputAction.CallbackContext ctx)
    {
        if (passTurnPulse != null)
            passTurnPulse.pulseSpeed = 2f;

        if (passRoutine != null)
        {
            StopCoroutine(passRoutine);
            passRoutine = null;
        }

        passTurnUI?.ResetProgressSmooth();
    }

    private IEnumerator PassTurnRoutine()
    {
        float elapsed = 0f;
        while (elapsed < passHoldDuration)
        {
            if (!playerInputs.Battle.Back.IsPressed())
            {
                passRoutine = null;
                passTurnUI?.ResetProgressSmooth();
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            passTurnUI?.SetProgress(elapsed / passHoldDuration);
            yield return null;
        }

        passRoutine = null;
        NewBattleManager.Instance.EndTurn();
    }

    private void OnEnemiesGroupSelection(InputAction.CallbackContext ctx)
    {
        NewBattleManager bm = NewBattleManager.Instance;
        if (bm.currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad)
        {
            TargetType desired = TargetType.SingleEnemy;
            if (bm.currentMove != null)
            {
                if (bm.currentMove.targetTypes.Contains(TargetType.SingleEnemy))
                    desired = TargetType.SingleEnemy;
                else if (bm.currentMove.targetTypes.Contains(TargetType.AllEnemies))
                    desired = TargetType.AllEnemies;
                bm.currentMove.targetType = desired;
            }
            if (bm.currentItem != null)
            {
                if (bm.currentItem.targetTypes.Contains(TargetType.SingleEnemy))
                    desired = TargetType.SingleEnemy;
                else if (bm.currentItem.targetTypes.Contains(TargetType.AllEnemies))
                    desired = TargetType.AllEnemies;
                bm.currentItemTargetType = desired;
            }
            bm.ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies);

        }
    }

    private void OnSquadGroupSelection(InputAction.CallbackContext ctx)
    {
        NewBattleManager bm = NewBattleManager.Instance;
        if (bm.currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies)
        {
            TargetType desired = TargetType.SingleAlly;
            if (bm.currentMove != null)
            {
                if (bm.currentMove.targetTypes.Contains(TargetType.SingleAlly))
                    desired = TargetType.SingleAlly;
                else if (bm.currentMove.targetTypes.Contains(TargetType.AllAllies))
                    desired = TargetType.AllAllies;
                bm.currentMove.targetType = desired;
            }
            if (bm.currentItem != null)
            {
                if (bm.currentItem.targetTypes.Contains(TargetType.SingleAlly))
                    desired = TargetType.SingleAlly;
                else if (bm.currentItem.targetTypes.Contains(TargetType.AllAllies))
                    desired = TargetType.AllAllies;
                bm.currentItemTargetType = desired;
            }
            bm.ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad);

        }
    }

    private void OnForceCamInput(InputAction.CallbackContext ctx)
    {
        CameraController cc = CameraController.Instance;
        if (cc.currentWorldCameraState != WorldCameraState.Forced)
        {
            cc.ForceCam();
            controller.movementMode = CharacterController3D.MovementMode.TPSOverShoulder;
        }
        else
        {
            cc.ReleaseCam();
            controller.movementMode = CharacterController3D.MovementMode.FixedCamera;
        }
    }
    #endregion
}

[CustomEditor(typeof(InputsManager))]
[CanEditMultipleObjects]
public class InputsManagerEditor : Editor
{
    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        // Rafraîchit l'Inspector en temps réel
        Repaint();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Affiche l'inspecteur par défaut
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.LabelField("🎮 Input Action Maps Status", EditorStyles.boldLabel);

            // Pour chaque instance sélectionnée
            foreach (var obj in targets)
            {
                var mgr = obj as InputsManager;
                if (mgr == null) continue;

                EditorGUILayout.LabelField($"-- {mgr.gameObject.name} --", EditorStyles.miniBoldLabel);
                DrawMapStatus("World", mgr.playerInputs.World.Get());
                DrawMapStatus("Inventory", mgr.playerInputs.Inventory.Get());
                DrawMapStatus("Battle", mgr.playerInputs.Battle.Get());
                DrawMapStatus("Munin", mgr.playerInputs.Munin.Get());
                DrawMapStatus("InfoBox", mgr.playerInputs.InfoBox.Get());
                EditorGUILayout.Space();
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Passez en Play Mode pour voir l'état des Input Action Maps.",
                MessageType.Info
            );
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawMapStatus(string label, InputActionMap map)
    {
        bool isEnabled = map.enabled;
        string statusText = isEnabled ? "Enabled" : "Disabled";

        var style = new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = isEnabled ? Color.green : Color.red }
        };

        EditorGUILayout.LabelField(label, statusText, style);
    }
}