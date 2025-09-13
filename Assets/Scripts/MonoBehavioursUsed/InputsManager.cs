using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

#if UNITY_EDITOR
// Directives réservées à l'éditeur pour l'inspecteur personnalisé.
// En production, UnityEditor n'est pas disponible, d'où l'encapsulation.
using UnityEditor;
#endif

public class InputsManager : MonoBehaviour
{
    public static InputsManager Instance { get; private set; }
    public PlayerInputs playerInputs;
    private CharacterController3D controller;

    [Header("Pass Turn")]
    public float passHoldDuration = 2f;
    private Pulse passTurnPulse;
    private Coroutine passRoutine;

    private InputActionMap[] allMaps;


    /// <summary>
    /// Indique si les validations doivent être temporairement ignorées.
    /// Cette variable passe à <c>true</c> lorsqu'une compétence ou un objet est
    /// sélectionné avec la même touche que la confirmation. Toutes les tentatives
    /// de validation sont alors ignorées jusqu'au relâchement de la touche.
    /// </summary>
    private bool ignorerProchaineValidation = false;

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

        // --- Détection des périphériques au démarrage ---
        // Force un rafraîchissement de l'Input System pour
        // recenser les périphériques déjà branchés lorsque
        // le jeu se lance (évite de devoir les rebrancher).
        InputSystem.Update();

        // Affiche dans la console les périphériques détectés
        // afin de faciliter le débogage et la maintenance.
        foreach (var device in InputSystem.devices)
        {
            Debug.Log($"Périphérique détecté au lancement : {device.displayName}");
        }

        // Écoute les changements de périphériques pour
        // gérer les branchements/débranchements à chaud.
        InputSystem.onDeviceChange += OnDeviceChange;

        allMaps = new[]
        {
            playerInputs.World.Get(),
            playerInputs.Inventory.Get(),
            playerInputs.Battle.Get(),
            playerInputs.InfoBox.Get(),
            playerInputs.Menu.Get()
        };

        // Les actions LeftShoulder et RightShoulder sont désormais définies dans
        // l'asset PlayerInputs. Elles seront reliées aux callbacks dans SetInputs().
    }

    /// <summary>
    /// Active le mapping World au démarrage et trouve le contrôleur.
    /// </summary>
    void Start()
    {
        ActivateOnly(playerInputs.World.Get());
        SetInputs();
        controller = FindFirstObjectByType<CharacterController3D>();
        passTurnPulse = PassTurnUI.Instance.gameObject.GetComponentInChildren<Pulse>();
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
        battle.Awake.performed += OnAwake; // Appui sur la touche d'éveil
        battle.Back.started += OnBackStarted;
        battle.Back.performed += OnBackInput;
        battle.Back.canceled += OnBackCanceled;
        // "Confirm" est également utilisé pour sélectionner la 3e compétence.
        // On écoute donc l'évènement "canceled" afin de savoir quand la touche est relâchée.
        battle.Confirm.performed += OnConfirm;
        battle.Confirm.canceled += OnConfirmCanceled;
        battle.EnemiesGroupSelection.performed += OnEnemiesGroupSelection;
        battle.SquadGroupSelection.performed += OnSquadGroupSelection;
        // Naviguer entre les pages de compétences avec les boutons d'épaules
        battle.LeftShoulder.performed += OnPreviousSkillPage;
        battle.RightShoulder.performed += OnNextSkillPage;

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
        battle.Awake.performed -= OnAwake; // Désabonnement de la touche d'éveil
        battle.Back.started -= OnBackStarted;
        battle.Back.performed -= OnBackInput;
        battle.Back.canceled -= OnBackCanceled;
        // On se désabonne également de l'évènement "canceled" lié à Confirm.
        battle.Confirm.performed -= OnConfirm;
        battle.Confirm.canceled -= OnConfirmCanceled;
        battle.EnemiesGroupSelection.performed -= OnEnemiesGroupSelection;
        battle.SquadGroupSelection.performed -= OnSquadGroupSelection;
        battle.LeftShoulder.performed -= OnPreviousSkillPage;
        battle.RightShoulder.performed -= OnNextSkillPage;

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

    #region Gestion des périphériques
    /// <summary>
    /// Désabonne les événements lors de la destruction du singleton.
    /// </summary>
    void OnDestroy()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        // Nettoyage des abonnements aux boutons d'épaules
        if (playerInputs != null)
        {
            var battle = playerInputs.Battle;
            battle.LeftShoulder.performed -= OnPreviousSkillPage;
            battle.RightShoulder.performed -= OnNextSkillPage;
        }
    }

    /// <summary>
    /// Callback appelé lors du branchement ou de la reconnexion d'un périphérique.
    /// </summary>
    /// <param name="device">Le périphérique concerné.</param>
    /// <param name="change">Le type de changement détecté.</param>
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)
        {
            Debug.Log($"Périphérique (re)connecté : {device.displayName}");
        }
    }

    #endregion

    #region Inputs
    /// <summary>
    /// Callback de validation des actions de combat.
    /// </summary>
    private void OnConfirm(InputAction.CallbackContext ctx)
    {
        // Si une sélection vient juste d'être effectuée, on ignore cette validation
        // pour éviter d'exécuter immédiatement le mouvement sans choix de cible.
        if (ignorerProchaineValidation)
        {
            // Le joueur vient de sélectionner une compétence ou un objet
            // avec la même touche que la validation. On ignore donc toute
            // tentative de confirmation tant que la touche n'a pas été relâchée.
            return;
        }

        NewBattleManager bm = NewBattleManager.Instance;
        if (bm.currentBattleState == BattleState.SquadUnit_TargetSelectionAmongEnemiesForSkill
            || bm.currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadForSkill
            || bm.currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad
            || bm.currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies
            )
        {
            // Liste des messages d'erreurs à afficher au joueur.
            // Cette liste permet de communiquer plusieurs problèmes en même temps
            // (distance trop grande, cible au mauvais endroit, etc.).
            var instructions = new System.Collections.Generic.List<string>();

            // Vérifie si la cible est à portée de l'attaque.
            // Si elle ne l'est pas, on l'ajoute à la liste des messages.
            if (!bm.IsTargetInRange(bm.currentCharacterUnit, bm.currentTargetCharacter, bm.currentMove))
            {
                instructions.Add("Cible trop éloignée");
            }

            // Vérifie que l'altitude de la cible correspond aux exigences du mouvement.
            // Exemple : un mouvement terrestre ne peut pas toucher une cible en l'air.
            if (!bm.IsTargetAltitudeValid(bm.currentTargetCharacter, bm.currentMove))
            {
                if (bm.currentMove.altitudeCondition == AltitudeCondition.AirOnly)
                {
                    // Indique que la cible doit être en l'air sans aucun sol sous elle.
                    instructions.Add("La cible doit être en l'air sans sol sous elle");
                }
                else if (bm.currentMove.altitudeCondition == AltitudeCondition.GroundOnly)
                {
                    // Indique que la cible doit être au sol.
                    instructions.Add("La cible doit être au sol");
                }
            }

            // Si une ou plusieurs erreurs ont été détectées, on les affiche
            // toutes en même temps, puis on quitte la méthode.
            if (instructions.Count > 0)
            {
                ActionUIDisplayManager.Instance.DisplayInstruction(string.Join("\n", instructions));
                return;
            }

            // Empêche le lancement si la position relative est déjà occupée par une autre unité.
            if (!bm.HasSpaceForMove(bm.currentCharacterUnit, bm.currentTargetCharacter, bm.currentMove))
            {
                ActionUIDisplayManager.Instance.DisplayInstruction_TargetPositionOccupied();
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


            bm.ChangeBattleState(BattleState.SquadUnit_Item_Use);
            bm.StartCoroutine(bm.UseItemOnTarget(bm.currentItem, bm.currentCharacterUnit, bm.currentTargetCharacter));
            bm.ToggleMenuContainers(false, false, false);
        }

        if (bm.currentBattleState == BattleState.VictoryScreen_CanContinue ||
            bm.currentBattleState == BattleState.GameOverScreen_CanContinue)
        {
            bm.ChangeBattleState(BattleState.None);
            BattleTransitionManager.Instance.StartCoroutine(
                BattleTransitionManager.Instance.ExitVictoryScreenAndBattle());
        }
    }

    /// <summary>
    /// Réinitialise l'indicateur d'ignorance lorsque la touche "Confirm" est relâchée.
    /// </summary>
    private void OnConfirmCanceled(InputAction.CallbackContext ctx)
    {
        // Dès que le joueur relâche la touche, la validation redevient possible.
        ignorerProchaineValidation = false;
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
            int baseIndex = bm.currentSkillPageIndex * bm.currentSkillsMenuSlots.Count;
            if (bm.skillChoices.Count > baseIndex)
            {
                bm.currentMove = bm.skillChoices[baseIndex];
                if (bm.currentCharacterUnit.GetHarmonicCount(bm.currentCharacterUnit.Data.harmonicType) < bm.currentMove.harmonicCost)
                {
                    ActionUIDisplayManager.Instance.DisplayInstruction_NotEnoughHarmonics();
                    return;
                }
                if (bm.currentCharacterUnit.IsMoveOnCooldown(bm.currentMove))
                {
                    ActionUIDisplayManager.Instance.DisplayInstruction_MoveOnCooldown();
                    bm.ShowMainMenu();
                    return;
                }
                bm.ToggleMenuContainers(false, false, false);
                // La touche utilisée pour cette compétence est aussi celle de confirmation :
                // on ignore donc toute validation tant qu'elle reste enfoncée.
                ignorerProchaineValidation = true;
                bm.HandleTargetSelection(bm.currentMove);
                // Les animations de visée sont désormais intégrées dans la Timeline de préparation
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
                // Empêche une utilisation immédiate de l'objet si la touche est maintenue.
                ignorerProchaineValidation = true;
                bm.HandleTargetSelection(bm.currentItem);
                // La Timeline de préparation gère maintenant les animations liées à l'objet
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
            int baseIndex = bm.currentSkillPageIndex * bm.currentSkillsMenuSlots.Count;
            if (bm.skillChoices.Count > baseIndex + 1)
            {
                bm.currentMove = bm.skillChoices[baseIndex + 1];
                if (bm.currentCharacterUnit.GetHarmonicCount(bm.currentCharacterUnit.Data.harmonicType) < bm.currentMove.harmonicCost)
                {
                    ActionUIDisplayManager.Instance.DisplayInstruction_NotEnoughHarmonics();
                    return;
                }
                if (bm.currentCharacterUnit.IsMoveOnCooldown(bm.currentMove))
                {
                    ActionUIDisplayManager.Instance.DisplayInstruction_MoveOnCooldown();
                    bm.ShowMainMenu();
                    return;
                }
                bm.ToggleMenuContainers(false, false, false);
                // La touche peut rester enfoncée : éviter un lancement automatique.
                ignorerProchaineValidation = true;
                bm.HandleTargetSelection(bm.currentMove);
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

// Ignore la validation tant que le bouton est pressé.
                ignorerProchaineValidation = true;
                bm.HandleTargetSelection(bm.currentItem);
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
            int baseIndex = bm.currentSkillPageIndex * bm.currentSkillsMenuSlots.Count;
            if (bm.skillChoices.Count > baseIndex + 2)
            {
                bm.currentMove = bm.skillChoices[baseIndex + 2];
                if (bm.currentCharacterUnit.GetHarmonicCount(bm.currentCharacterUnit.Data.harmonicType) < bm.currentMove.harmonicCost)
                {
                    ActionUIDisplayManager.Instance.DisplayInstruction_NotEnoughHarmonics();
                    return;
                }
                if (bm.currentCharacterUnit.IsMoveOnCooldown(bm.currentMove))
                {
                    ActionUIDisplayManager.Instance.DisplayInstruction_MoveOnCooldown();
                    bm.ShowMainMenu();
                    return;
                }
                bm.ToggleMenuContainers(false, false, false);

// Sans cette ligne, le mouvement serait lancé aussitôt la compétence choisie.
                ignorerProchaineValidation = true;
                bm.HandleTargetSelection(bm.currentMove);
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
                // Empêche l'utilisation automatique de l'objet.
                ignorerProchaineValidation = true;
                bm.HandleTargetSelection(bm.currentItem);
            }
            else
            {
                Debug.LogWarning("[InputsManager] OnSelect3 ignoré : pas assez d'items !");
            }
        }
    }

    /// <summary>
    /// Tente d'activer l'état Awake lorsque le joueur est dans le SkillsMenu.
    /// </summary>
    private void OnAwake(InputAction.CallbackContext ctx)
    {
        NewBattleManager bm = NewBattleManager.Instance;

        if (bm.currentBattleState != BattleState.SquadUnit_SkillsMenu)
            return; // Ignore l'input si le menu des compétences n'est pas actif

        CharacterUnit unit = bm.currentCharacterUnit;
        if (unit == null)
            return;

        // Vérifie la réserve d'harmonique nécessaire pour l'éveil
        if (unit.GetHarmonicCount(unit.Data.harmonicType) < unit.Data.resonancePoint)
        {
            // Feedback si le joueur n'a pas assez d'harmonique
            ActionUIDisplayManager.Instance.DisplayInstruction_NotEnoughHarmonics();
            return;
        }

        // Active l'état Awake et rafraîchit les compétences disponibles
        unit.EnterAwakeState();
        bm.OpenSkillsMenu();
    }

    /// <summary>
    /// Appelé lorsque le joueur presse l'épaule droite pour afficher la page suivante de compétences.
    /// </summary>
    private void OnNextSkillPage(InputAction.CallbackContext ctx)
    {
        NewBattleManager bm = NewBattleManager.Instance;

        // On ne réagit que si le joueur se trouve bien dans le menu des compétences,
        // afin d'éviter tout comportement inattendu dans les autres états de combat.
        if (bm.currentBattleState != BattleState.SquadUnit_SkillsMenu)
            return;

        // Calcule le nombre de cases disponibles pour les attaques musicales classiques
        // (le dernier slot du menu étant réservé au mouvement spécial).
        int pageSize = bm.currentSkillsMenuSlots.Count - 1;

        // Si le total des attaques disponibles tient sur une seule page, inutile de tenter
        // une navigation : on quitte simplement la méthode.
        if (bm.skillChoices == null || bm.skillChoices.Count <= pageSize)
            return;

        // Toutes les conditions sont réunies : on peut afficher la page suivante.
        bm.NextSkillPage();
    }

    /// <summary>
    /// Appelé lorsque le joueur presse l'épaule gauche pour afficher la page précédente de compétences.
    /// </summary>
    private void OnPreviousSkillPage(InputAction.CallbackContext ctx)
    {
        NewBattleManager bm = NewBattleManager.Instance;

        // De la même manière que pour l'épaule droite, on vérifie d'abord que le menu
        // des compétences est bien actif avant de traiter l'entrée.
        if (bm.currentBattleState != BattleState.SquadUnit_SkillsMenu)
            return;

        // Détermination du nombre d'attaques affichables par page (hors move spécial).
        int pageSize = bm.currentSkillsMenuSlots.Count - 1;

        // Si une seule page suffit à afficher toutes les compétences, il n'y a rien
        // à afficher de plus et l'on quitte la méthode.
        if (bm.skillChoices == null || bm.skillChoices.Count <= pageSize)
            return;

        // Navigation vers la page précédente.
        bm.PreviousSkillPage();
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

        PassTurnUI.Instance.ResetProgressSmooth();
    }

    private IEnumerator PassTurnRoutine()
    {
        float elapsed = 0f;
        while (elapsed < passHoldDuration)
        {
            if (!playerInputs.Battle.Back.IsPressed())
            {
                passRoutine = null;
                PassTurnUI.Instance.ResetProgressSmooth();
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            PassTurnUI.Instance.SetProgress(elapsed / passHoldDuration);
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
            bool allowed = false;
            if (bm.currentMove != null)
            {
                if (bm.currentMove.targetTypes.Contains(TargetType.SingleEnemy))
                {
                    desired = TargetType.SingleEnemy;
                    allowed = true;
                }
                else if (bm.currentMove.targetTypes.Contains(TargetType.AllEnemies))
                {
                    desired = TargetType.AllEnemies;
                    allowed = true;
                }
                else if (bm.currentMove.targetTypes.Contains(TargetType.All))
                {
                    desired = TargetType.All;
                    allowed = true;
                }

                if (allowed)
                    bm.currentMove.targetType = desired;
            }
            if (bm.currentItem != null)
            {
                bool itemAllowed = false;
                if (bm.currentItem.targetTypes.Contains(TargetType.SingleEnemy))
                {
                    desired = TargetType.SingleEnemy;
                    itemAllowed = true;
                }
                else if (bm.currentItem.targetTypes.Contains(TargetType.AllEnemies))
                {
                    desired = TargetType.AllEnemies;
                    itemAllowed = true;
                }
                else if (bm.currentItem.targetTypes.Contains(TargetType.All))
                {
                    desired = TargetType.All;
                    itemAllowed = true;
                }
                if (itemAllowed)
                    bm.currentItemTargetType = desired;
                allowed = allowed || itemAllowed;
            }
            if (!allowed)
                return;

            bm.ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies);
            bm.SetCurrentTargetToFirst(CharacterType.EnemyUnit);

        }
    }

    private void OnSquadGroupSelection(InputAction.CallbackContext ctx)
    {
        NewBattleManager bm = NewBattleManager.Instance;
        if (bm.currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies)
        {
            TargetType desired = TargetType.SingleAlly;
            bool allowed = false;
            if (bm.currentMove != null)
            {
                if (bm.currentMove.targetTypes.Contains(TargetType.SingleAlly))
                {
                    desired = TargetType.SingleAlly;
                    allowed = true;
                }
                else if (bm.currentMove.targetTypes.Contains(TargetType.AllAllies))
                {
                    desired = TargetType.AllAllies;
                    allowed = true;
                }
                else if (bm.currentMove.targetTypes.Contains(TargetType.All))
                {
                    desired = TargetType.All;
                    allowed = true;
                }

                if (allowed)
                    bm.currentMove.targetType = desired;
            }
            if (bm.currentItem != null)
            {
                bool itemAllowed = false;
                if (bm.currentItem.targetTypes.Contains(TargetType.SingleAlly))
                {
                    desired = TargetType.SingleAlly;
                    itemAllowed = true;
                }
                else if (bm.currentItem.targetTypes.Contains(TargetType.AllAllies))
                {
                    desired = TargetType.AllAllies;
                    itemAllowed = true;
                }
                else if (bm.currentItem.targetTypes.Contains(TargetType.All))
                {
                    desired = TargetType.All;
                    itemAllowed = true;
                }
                if (itemAllowed)
                    bm.currentItemTargetType = desired;
                allowed = allowed || itemAllowed;
            }
            if (!allowed)
                return;

            bm.ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad);
            bm.SetCurrentTargetToFirst(CharacterType.SquadUnit);

        }
    }

    private void OnForceCamInput(InputAction.CallbackContext ctx)
    {
        CameraController cc = CameraController.Instance;
    }
    #endregion
}

#if UNITY_EDITOR
// ----- Inspecteur personnalisé pour InputsManager (éditeur uniquement) -----
[CustomEditor(typeof(InputsManager))]
[CanEditMultipleObjects]
public class InputsManagerEditor : Editor
{
    private void OnEnable()
    {
        // Mise à jour régulière de l'inspector pour afficher l'état des maps.
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
        // Sécurité : on vérifie que l'objet sérialisé existe toujours
        if (serializedObject == null)
            return;

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
                if (obj == null) continue;
                var mgr = obj as InputsManager;
                if (mgr == null) continue;

                EditorGUILayout.LabelField($"-- {mgr.gameObject.name} --", EditorStyles.miniBoldLabel);
                DrawMapStatus("World", mgr.playerInputs.World.Get());
                DrawMapStatus("Inventory", mgr.playerInputs.Inventory.Get());
                DrawMapStatus("Battle", mgr.playerInputs.Battle.Get());
                DrawMapStatus("InfoBox", mgr.playerInputs.InfoBox.Get());
                DrawMapStatus("Menu", mgr.playerInputs.Menu.Get());
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
#endif