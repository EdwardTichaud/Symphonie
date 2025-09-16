using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // Nécessaire pour utiliser les actions d'input
using UnityEngine.Timeline; // Pour lancer les timelines post-combat
using Unity.Cinemachine; // Pour gérer les caméras Cinemachine

public class BattleTransitionManager : MonoBehaviour
{
    public static BattleTransitionManager Instance { get; private set; }

    private PlayerDetection playerDetection;

    [Header("Ressources Visuals")]
    [SerializeField] private Image worldFadeOverlay;
    [SerializeField] private ParticleSystem maskRingParticles;

    [Header("SFX")]
    [SerializeField] private List<AudioClip> transitionSFXClips = new();
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip brokenGlassSound;
    [SerializeField] private AudioClip versusThunder;

    [Header("Music")]
    [SerializeField] private AudioSource musicSource;

    private GameObject battleCamera;

    [Header("Scenes")]
    [SerializeField] private GameObject worldScene; // GameObject racine de la scène du monde à désactiver pendant les combats

    [Header("UI")]
    [SerializeField] private GameObject qteCircle;
    [SerializeField] private GameObject battleTimeline;
    [SerializeField] private GameObject passTurnButton;
    [SerializeField] private GameObject actionDisplayPanel;

    [Header("Versus Screen")]
    [SerializeField] private GameObject versusCameraCanvas;
    [SerializeField] private GameObject brokenGlass;
    [SerializeField] private GameObject versusTransition; // Transition du Versus
    [SerializeField] private Image SU1; // Portrait du premier allié
    [SerializeField] private Image SU2; // Portrait du second allié
    [SerializeField] private Image SU3; // Portrait du troisième allié
    [SerializeField] private Image EU1; // Portrait du premier ennemi
    [SerializeField] private Image EU2; // Portrait du second ennemi
    [SerializeField] private Image EU3; // Portrait du troisième ennemi
    [SerializeField] private Animator unitSpawnPointsAnimator; // Animator jouant l'animation Versus
    [SerializeField] private List<GameObject> continuePrompts = new(); // Éléments "Continuer" à afficher après chargement

    private bool battleUIShown = false; // Suivi de l'affichage initial de l'UI
    [SerializeField] private GameObject victoryScreen;
    [SerializeField] private GameObject gameOverScreen;

    /// <summary>
    /// Cache les éléments d'interface de combat au début de la transition.
    /// </summary>
    public void HideBattleUI()
    {
        // Désactive le cercle de QTE s'il est présent pour éviter son apparition précoce
        if (qteCircle != null)
            qteCircle.SetActive(false);

        if (battleTimeline != null)
            battleTimeline.SetActive(false);
        if (passTurnButton != null)
            passTurnButton.SetActive(false);
        if (actionDisplayPanel != null)
            actionDisplayPanel.SetActive(false);

        battleUIShown = false;
    }

    /// <summary>
    /// Affiche les éléments d'interface lorsque le joueur commence son premier tour.
    /// </summary>
    public void ShowBattleUIIfNeeded()
    {
        if (battleUIShown)
            return;

        if (qteCircle != null)
            qteCircle.SetActive(true);

        if (battleTimeline != null)
            battleTimeline.SetActive(true);
        if (passTurnButton != null)
            passTurnButton.SetActive(true);
        if (actionDisplayPanel != null)
            actionDisplayPanel.SetActive(true);

        battleUIShown = true;
    }

    /// <summary>
    /// Met à jour l'écran de Versus avec les portraits des unités alliées et ennemies.
    /// Les emplacements vides deviennent totalement transparents.
    /// </summary>
    private void UpdateVersusPortraits()
    {
        // Récupération des listes d'unités
        var squad = SquadManager.Instance != null ? SquadManager.Instance.SquadCharacters : new List<CharacterData>();
        var enemies = NewBattleManager.Instance != null ? NewBattleManager.Instance.enemyTemplates : new List<CharacterData>();

        // Tableau des images alliées et traitement
        Image[] squadImages = { SU1, SU2, SU3 };
        for (int i = 0; i < squadImages.Length; i++)
        {
            if (i < squad.Count && squad[i] != null)
            {
                // Récupère la liste des sprites de Versus définie dans le CharacterData
                List<Sprite> sprites = squad[i].versusSprites;
                if (sprites != null && sprites.Count > 0)
                {
                    // Choisit un sprite aléatoire parmi la liste fournie
                    squadImages[i].sprite = sprites[Random.Range(0, sprites.Count)];
                    // Assure une opacité totale pour le sprite sélectionné
                    Color c = squadImages[i].color; c.a = 1f; squadImages[i].color = c;
                }
                else
                {
                    // Liste nulle ou vide : on rend l'image totalement transparente
                    // pour éviter d'afficher un portrait par défaut ou un sprite incorrect
                    squadImages[i].sprite = null;
                    Color c = squadImages[i].color; c.a = 0f; squadImages[i].color = c;
                }
            }
            else
            {
                // Aucune unité : image totalement transparente
                squadImages[i].sprite = null;
                Color c = squadImages[i].color; c.a = 0f; squadImages[i].color = c;
            }
        }

        // Tableau des images ennemies et traitement identique
        Image[] enemyImages = { EU1, EU2, EU3 };
        for (int i = 0; i < enemyImages.Length; i++)
        {
            if (i < enemies.Count && enemies[i] != null)
            {
                List<Sprite> sprites = enemies[i].versusSprites;
                if (sprites != null && sprites.Count > 0)
                {
                    // Sélection d'un sprite aléatoire pour l'ennemi courant
                    enemyImages[i].sprite = sprites[Random.Range(0, sprites.Count)];
                    // Opacité maximale pour garantir la visibilité du portrait
                    Color c = enemyImages[i].color; c.a = 1f; enemyImages[i].color = c;
                }
                else
                {
                    // Liste nulle ou vide : le portrait doit être complètement transparent
                    enemyImages[i].sprite = null;
                    Color c = enemyImages[i].color; c.a = 0f; enemyImages[i].color = c;
                }
            }
            else
            {
                enemyImages[i].sprite = null;
                Color c = enemyImages[i].color; c.a = 0f; enemyImages[i].color = c;
            }
        }
    }

    #region Initialisation
    /// <summary>
    /// Prépare les références globales du gestionnaire de transition.
    /// </summary>
    private void Awake()
    {
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

        worldFadeOverlay ??= GameObject.Find("WorldFadeOverlayPanel")?.GetComponent<Image>();
        playerDetection ??= FindFirstObjectByType<PlayerDetection>();
        battleCamera = GameObject.FindGameObjectWithTag("BattleCamera");

        // Tente de récupérer automatiquement le GameObject "WorldScene" si l'on n'a rien assigné dans l'inspecteur
        worldScene ??= GameObject.Find("WorldScene");

        // Recherche automatique des éléments "Continuer" si aucun n'est assigné
        if (continuePrompts == null || continuePrompts.Count == 0)
        {
            continuePrompts = new List<GameObject>();
            if (versusTransition != null)
            {
                // Parcourt tous les enfants du Versus pour trouver ceux contenant "Continuer"
                foreach (Transform t in versusTransition.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.Contains("Continuer"))
                        continuePrompts.Add(t.gameObject);
                }
            }
        }
    }

    /// <summary>
    /// S'assure que l'interface de combat est masquée au chargement de la scène.
    /// </summary>
    private void Start()
    {
        // On cache toutes les UI liées au combat tant que le joueur n'a pas débuté son premier tour.
        HideBattleUI();
    }

    #endregion

    #region Transition
    /// <summary>
    /// Prépare et lance un combat initié depuis une Timeline en spécifiant
    /// explicitement les ennemis à affronter via un ScriptableObject.
    /// Cette surcharge facilite l'appel depuis une Timeline qui ne peut
    /// transmettre qu'un seul paramètre dans ses signaux.
    /// </summary>
    /// <param name="config">ScriptableObject contenant les ennemis et les timelines associées.</param>
    public void StartTimelineBattle(TimelineBattleConfigSO config)
    {
        // ✅ Dès qu'une timeline demande un combat, on arrête immédiatement
        // la timeline en cours pour éviter tout chevauchement d'actions.
        TimelineManager.Instance?.StopTimeline(); // Met fin à la cinématique en cours

        if (config == null)
        {
            Debug.LogWarning("[BattleTransitionManager] TimelineBattleConfigSO non fourni pour StartTimelineBattle.");
            return;
        }

        // ------------------------------------------------------------------
        // 1) Rassemble les ennemis définis dans le ScriptableObject
        // ------------------------------------------------------------------
        List<CharacterData> enemies = new();
        if (config.enemy1 != null) enemies.Add(config.enemy1);
        if (config.enemy2 != null) enemies.Add(config.enemy2);
        if (config.enemy3 != null) enemies.Add(config.enemy3);

        if (enemies.Count == 0)
        {
            Debug.LogWarning("[BattleTransitionManager] Aucun ennemi spécifié dans TimelineBattleConfigSO.");
            return;
        }

        // Stocke les timelines post-combat directement dans le gestionnaire de combat
        if (NewBattleManager.Instance != null)
        {
            NewBattleManager.Instance.victoryTimeline = config.victoryTimeline;
            NewBattleManager.Instance.defeatTimeline = config.defeatTimeline;
            NewBattleManager.Instance.gameOverOnDefeat = config.defeatTimeline == null; // Game Over si aucune timeline de défaite
            NewBattleManager.Instance.lastBattleOutcome = BattleOutcome.None; // Réinitialisation de l'issue du combat
        }

        // --------------------------------------------------------------
        // 2) Simule la détection classique en remplissant PlayerDetection
        // --------------------------------------------------------------
        playerDetection ??= FindFirstObjectByType<PlayerDetection>();
        if (playerDetection != null)
        {
            playerDetection.detectedEnemies.Clear();
            playerDetection.detectedEnemies.AddRange(enemies);
            playerDetection.detectionOn = false; // Évite toute nouvelle détection
            playerDetection.battleEngaged = true; // Marque le combat comme en cours
        }
        else
        {
            Debug.LogWarning("[BattleTransitionManager] PlayerDetection introuvable, état non synchronisé.");
        }

        // --------------------------------------------------------------
        // 3) Transmet la liste au NewBattleManager pour le combat
        // --------------------------------------------------------------
        if (NewBattleManager.Instance != null)
        {
            NewBattleManager.Instance.enemyTemplates.Clear();
            NewBattleManager.Instance.enemyTemplates.AddRange(enemies);
        }
        else
        {
            Debug.LogWarning("[BattleTransitionManager] NewBattleManager introuvable, combat annulé.");
            return;
        }

        // ------------------------------------------------------------------
        // 4) Lancement de la transition de combat classique
        // ------------------------------------------------------------------
        StartCombatTransition();
    }

    /// <summary>
    /// Lance toutes les étapes de la transition vers le mode combat.
    /// </summary>
    public void StartCombatTransition()
    {
        // ✅ Sécurité supplémentaire : s'assure qu'aucune timeline ne
        // continue pendant la transition de combat.
        TimelineManager.Instance?.StopTimeline();

        // Sauvegarde la position actuelle de la WorldCamera pour la restituer après le combat
        CameraController.Instance?.SaveWorldCameraTransform();

        CombatSkyboxManager.Instance?.ApplyBattleSkybox();

        // Masque l'interface jusqu'au premier tour du joueur
        HideBattleUI();

        // Désactive également le GameObject principal du monde pour empêcher toute interaction
        if (worldScene != null)
            worldScene.SetActive(false);

        // Active uniquement les caméras nécessaires au lancement du combat
        CameraActivationManager.Instance?.ActivateBattleAndVersusCameras();

        AudioClip randomClip = null;
        ZoneSO currentZone = ZoneManager.Instance != null ? ZoneManager.Instance.currentZone : null;
        if (currentZone != null && currentZone.battleMusic != null && currentZone.battleMusic.Length > 0)
        {
            randomClip = currentZone.battleMusic[Random.Range(0, currentZone.battleMusic.Length)];
        }

        if (randomClip != null)
            AudioManager.Instance.TransitionToCombat(randomClip);
        else
            Debug.LogWarning("[BattleTransitionManager] Aucune musique de combat trouvée pour la zone actuelle.");
        StartCoroutine(PlayTransitionSoundsSequentially());
        StartCoroutine(TransitionRoutine());

        Debug.Log("[BattleTransitionManager] Transition de combat démarrée.");
    }

    /// <summary>
    /// Enchaîne les différentes étapes visuelles et logiques de la transition.
    /// </summary>
    private IEnumerator TransitionRoutine()
    {
        // 🛡️ Assure que les filtres BlackScreen et WhiteScreen sont totalement
        //    transparents avant même l'affichage de l'écran de Versus.
        //    Cette précaution évite tout flash résiduel provenant d'une
        //    transition précédente.
        var fader = FadeChildrenOpacity.Instance;
        if (fader != null)
        {
            // Indice 0 = BlackScreen, indice 1 = WhiteScreen.
            // La durée est fixée à 0 pour appliquer instantanément la
            // transparence complète.
            fader.EnsureTransparency(0, 0f);
            fader.EnsureTransparency(1, 0f);
        }

        // Détermine quel champ de bataille doit être chargé en fonction de
        // l'ennemi détecté par le joueur.
        // 
        // Dans certains cas (ex. zone non initialisée correctement), l'objet
        // PlayerDetection peut être désactivé ou sa liste d'ennemis vide.
        // Afin d'éviter une NullReference et de garantir l'affichage de l'écran
        // de Versus, on tente d'abord de récupérer l'indice depuis
        // PlayerDetection. Si cela échoue, on se replie sur la liste copiée
        // dans le NewBattleManager.

        // Récupération (ou création) de la référence vers PlayerDetection
        playerDetection ??= FindFirstObjectByType<PlayerDetection>();

        // Valeur par défaut au cas où aucun ennemi valide n'est trouvé
        int battlefieldIndex = 0;

        if (playerDetection != null && playerDetection.detectedEnemies.Count > 0 &&
            playerDetection.detectedEnemies[0] != null)
        {
            // ✅ Cas nominal : utilisation de l'ennemi détecté pour choisir le champ de bataille
            battlefieldIndex = playerDetection.detectedEnemies[0].battlefieldIndex;
        }
        else if (NewBattleManager.Instance.enemyTemplates.Count > 0 &&
                 NewBattleManager.Instance.enemyTemplates[0] != null)
        {
            // 🔁 Repli : on s'appuie sur la liste d'ennemis déjà copiée dans le
            // NewBattleManager lors de la détection. Cela évite un blocage de la
            // transition si PlayerDetection n'est plus accessible.
            battlefieldIndex = NewBattleManager.Instance.enemyTemplates[0].battlefieldIndex;
        }
        else
        {
            // ⚠️ Aucun ennemi valide trouvé : on loggue un avertissement afin de
            // faciliter le débogage et on conserve l'indice par défaut (0).
            Debug.LogWarning("[BattleTransitionManager] Impossible de déterminer l'indice du battlefield ; utilisation de 0 par défaut.");
        }

        // Effet de distorsion pour accentuer le passage en mode combat
        if (PostProcessManager.Instance != null)
            StartCoroutine(PostProcessManager.Instance.PulseLensDistortion(-1f, 0.5f));

        AudioManager.Instance.PlaySound(versusThunder);
        StartCoroutine(VersusThunderFlash()); // Déclenche un flash blanc synchronisé avec le son

        battleCamera.SetActive(true);
        versusTransition.SetActive(true);
        versusCameraCanvas.SetActive(true);

        // S'assure que la VersusCam est bien active conjointement à la BattleCam
        CameraActivationManager.Instance?.ActivateBattleAndVersusCameras();

        // Cache le bouton "Continuer" tant que le chargement du champ de bataille n'est pas terminé
        foreach (var go in continuePrompts)
            if (go != null)
                go.SetActive(false);

        // Met à jour les portraits du Versus et lance l'animation d'apparition
        UpdateVersusPortraits();
        unitSpawnPointsAnimator?.Play("VersusLaunched");

        // Pendant que l'écran de Versus est affiché, on charge uniquement le
        // champ de bataille correspondant à l'ennemi rencontré. L'action
        // "Confirm" ne sera disponible qu'une fois ce chargement terminé afin
        // d'éviter toute validation prématurée.
        yield return StartCoroutine(BattlefieldManager.Instance.LoadBattlefield(battlefieldIndex));

        // Affiche à présent le bouton "Continuer" puisque le joueur peut confirmer
        foreach (var go in continuePrompts)
            if (go != null)
                go.SetActive(true);

        // --- Attente de la confirmation du joueur ---
        // L'écran de Versus reste jusqu'à ce que le joueur appuie sur
        // "Confirm". Cette action vient d'être rendue disponible car le
        // battlefield est prêt.
        InputAction confirmAction = InputsManager.Instance.playerInputs.Battle.Confirm;
        confirmAction.Enable();
        yield return new WaitUntil(() => confirmAction.triggered);
        confirmAction.Disable();

        versusTransition.SetActive(false);
        brokenGlass.SetActive(true);
        Animator glass = brokenGlass.GetComponent<Animator>();
        glass.Play("Glass_Explode");

        // À la fin de l'animation, la VersusCam doit se désactiver
        if (CameraActivationManager.Instance != null)
            StartCoroutine(CameraActivationManager.Instance.DisableVersusAfterAnimation(glass));

        AudioManager.Instance.PlaySound(brokenGlassSound);

        // Les unités apparaissent après l'explosion
        NewBattleManager.Instance.SpawnAll();

        SetRenderingLayer.Instance.ApplyToAll();

        if (battleCamera != null)
        {
            battleCamera.SetActive(true); // S'assure que la camera de combat est active

            // Au lieu de jouer une timeline d'introduction, on affiche directement
            // la camera orbitale autour du champ de bataille avec une transition instantanée.
            BattleCameraManager.Instance?.SwitchToCamera("CinemachineCamera_10_OrbitAroundBattlefield", 0f);
        }
        else
        {
            Debug.LogWarning("[BattleTransitionManager] BattleScene_Camera_BattleIntro introuvable.");
        }

        SetupBattleCameraAndUI();

        GameManager.Instance.CurrentState = GameState.BattleTransition;
        InputsManager.Instance.ActivateOnly(InputsManager.Instance.playerInputs.Battle.Get());

        NewBattleManager.Instance.ChangeBattleState(BattleState.Initialization);

        while (NewBattleManager.Instance.unitsInBattle.Count <= 0)
            yield return null;

        // ⏳ laisser les timelines d'intro se jouer avant le début réel du combat.
        

        // 🎮 Lancement du combat et affichage du menu principal après la pause.
        yield return NewBattleManager.Instance.StartBattle();
    }


    public IEnumerator ExitVictoryScreenAndBattle()
    {
        // Restauration progressive du temps vers la normale
        yield return StartCoroutine(RestoreTimeScale(Time.timeScale, 1f, 2f));

        CombatSkyboxManager.Instance?.RestoreDefaultSkybox();

        //yield return FadeToBlack(2f);

        // Nous récupérons ici le composant PlayerDetection afin de pouvoir
        // manipuler la liste temporaire des ennemis en combat.
        playerDetection ??= FindFirstObjectByType<PlayerDetection>();

        // Suppression des ennemis vaincus présents dans le monde
        // La liste enemiesInFight contient déjà uniquement les ennemis engagés
        var worldEnemies = playerDetection.enemiesInFight.ToList();
        foreach (var enemy in worldEnemies)
        {
            // On retire l'ennemi de la liste afin qu'il ne puisse plus être redétecté
            playerDetection.enemiesInFight.Remove(enemy);
            // Destruction complète de l'objet ennemi dans le World pour
            // éviter qu'il ne persiste après la victoire du joueur
            if (enemy != null)
                Destroy(enemy.transform.parent.gameObject);
        }

        // Par sécurité, on vide la liste au cas où il resterait des références
        playerDetection.enemiesInFight.Clear();
        playerDetection.detectedEnemies.Clear(); // Nettoyage complet des ennemis détectés

        GameManager.Instance.ChangeGameState(GameState.Exploration);

        // Réactive la détection après un court délai
        playerDetection.ResetDetection(1f);

        // Réactive également le GameObject principal du monde pour reprendre l'exploration
        if (worldScene != null)
            worldScene.SetActive(true);

        // Retour à l'exploration : seule la WorldCam doit être active
        CameraActivationManager.Instance?.ActivateWorldCamera();
        // Replace la caméra du monde à l'endroit où le joueur l'avait laissée
        CameraController.Instance?.RestoreWorldCameraTransform();

        if (maskRingParticles != null)
        {
            maskRingParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            maskRingParticles.Play();
        }

        HideVictoryPanel();
        HideGameOverPanel();

        AudioManager.Instance.ReturnFromBattle();

        // Nettoie les éléments visuels ou caméras activés pendant la transition de combat
        ResetTransitionObjects();

        // Cache l'interface de combat pour le retour à l'exploration
        HideBattleUI();

        InputsManager.Instance.ActivateOnly(InputsManager.Instance.playerInputs.World.Get());

        if (Application.isPlaying && !Application.isEditor && SaveAndLoadManager.Instance != null)
        {
            SaveAndLoadManager.Instance.AutoSave();
        }

        // Attente avant de supprimer le champ de bataille pour laisser le temps
        // au joueur de revenir visuellement dans le monde.
        yield return new WaitForSecondsRealtime(1f);

        // Détermine si une timeline post-combat doit être jouée avant de réinitialiser
        TimelineAsset postBattleTimeline = null;
        if (NewBattleManager.Instance != null)
        {
            if (NewBattleManager.Instance.lastBattleOutcome == BattleOutcome.Victory)
                postBattleTimeline = NewBattleManager.Instance.victoryTimeline;
            else if (NewBattleManager.Instance.lastBattleOutcome == BattleOutcome.Defeat)
                postBattleTimeline = NewBattleManager.Instance.defeatTimeline;
        }

        // Destruction complète du battlefield et remise à zéro des infos du combat
        BattlefieldManager.Instance?.UnloadCurrentBattlefield();
        NewBattleManager.Instance?.ResetBattleInfos();

        // Joue la timeline correspondante si une est définie
        if (postBattleTimeline != null)
            TimelineManager.Instance.PlayTimelineOnCurrentNPC(postBattleTimeline);

        //yield return FadeToTransparent(1f);

        yield return new WaitForSecondsRealtime(0);
    }

    /// <summary>
    /// Réinitialise tous les éléments activés lors du lancement du combat afin
    /// d'éviter qu'ils restent visibles ou actifs une fois le combat terminé.
    /// </summary>
    private void ResetTransitionObjects()
    {
       versusTransition.SetActive(false); // Bouclage pour être sûr
       brokenGlass.SetActive(false);
       versusCameraCanvas.SetActive(false);

        // Le canvas de transition est désactivé pour éviter tout résidu visuel
        GameObject.Find("BattleScene_TransitionCanvas")?.transform.GetChild(0).gameObject.SetActive(false);
    }

    void SetupBattleCameraAndUI()
    {
        // Active la hiérarchie caméra
        battleCamera.SetActive(true);
        foreach (Transform child in battleCamera.transform)
            child.gameObject.SetActive(true);

        // Récupère la Camera Unity via son GameObject "BattleCamera_Cam"
        GameObject camObj = GameObject.Find("BattleCamera_Cam");
        if (camObj == null)
        {
            Debug.LogError("Impossible de trouver BattleCamera_Cam dans la scène.");
            return;
        }

        Camera unityCam = camObj.GetComponent<Camera>();
        if (unityCam == null)
        {
            Debug.LogError("BattleCamera_Cam n’a pas de composant Camera.");
            return;
        }

        // Assigne la caméra au Canvas en ScreenSpace-Camera
        var battleUICanvas = FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .FirstOrDefault(c => c.renderMode == RenderMode.ScreenSpaceCamera);

        if (battleUICanvas != null)
        {
            battleUICanvas.worldCamera = unityCam;
        }

        // Active le premier enfant du TransitionCanvas si trouvé
        GameObject.Find("BattleScene_TransitionCanvas")
            ?.transform.GetChild(0).gameObject.SetActive(true);
    }

    public IEnumerator SlowTimeScale(float to, float speed)
    {
        float epsilon = 0.001f; // petit seuil pour éviter les flottants imprécis

        while (Time.timeScale - to > epsilon)
        {
            float newScale = Time.timeScale - Time.unscaledDeltaTime * speed;
            if (newScale <= to + epsilon)
                newScale = to;

            Time.timeScale = Mathf.Max(0f, newScale);
            Time.fixedDeltaTime = Time.timeScale * 0.02f;

            yield return null;
        }

        Time.timeScale = to;
        Time.fixedDeltaTime = Time.timeScale * 0.02f;
    }

    private IEnumerator RestoreTimeScale(float from, float to, float speed)
    {
        float epsilon = 0.001f;

        while (to - Time.timeScale > epsilon)
        {
            Time.timeScale += Time.unscaledDeltaTime * speed;
            if (Time.timeScale > to)
                Time.timeScale = to;

            Time.fixedDeltaTime = Time.timeScale * 0.02f;
            yield return null;
        }

        Time.timeScale = to;
        Time.fixedDeltaTime = 0.02f;
    }

    private IEnumerator PlayTransitionSoundsSequentially()
    {
        if (sfxSource == null || transitionSFXClips.Count == 0)
        {
            Debug.LogWarning("[TransitionAudio] AudioSource ou clips manquants.");
            yield break;
        }

        foreach (var clip in transitionSFXClips)
        {
            sfxSource.clip = clip;
            sfxSource.Play();
            yield return new WaitForSeconds(clip.length);
        }
    }

    /// <summary>
    /// Crée un flash blanc rapide synchronisé avec le son "versusThunder".
    /// Utilise l'overlay existant en le colorant temporairement en blanc puis en le faisant disparaître.
    /// </summary>
    private IEnumerator VersusThunderFlash()
    {
        if (worldFadeOverlay == null)
        {
            Debug.LogWarning("[BattleTransitionManager] worldFadeOverlay manquant pour le flash blanc.");
            yield break;
        }

        // Sauvegarde l'état initial pour éviter des effets indésirables
        bool wasActive = worldFadeOverlay.gameObject.activeSelf;
        Color originalColor = worldFadeOverlay.color;

        // Prépare l'overlay en blanc totalement transparent
        worldFadeOverlay.gameObject.SetActive(true);
        worldFadeOverlay.color = new Color(1f, 1f, 1f, 0f);

        // Phase d'apparition très rapide simulant l'éclair
        float flashInDuration = 0.05f;
        float elapsed = 0f;
        while (elapsed < flashInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsed / flashInDuration);
            worldFadeOverlay.color = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }

        // Phase de disparition progressive pour un retour doux
        float flashOutDuration = 0.25f;
        elapsed = 0f;
        while (elapsed < flashOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / flashOutDuration);
            worldFadeOverlay.color = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }

        // Restaure l'overlay en forçant sa transparence.
        //
        // Sans cette étape, si un fondu noir précédent (par exemple issu d'une
        // timeline passée) a laissé l'overlay actif et opaque, le flash se
        // terminerait sur un écran noir persistant. En imposant un alpha nul,
        // on garantit que l'effet visuel est toujours correctement réinitialisé.
        worldFadeOverlay.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
        worldFadeOverlay.gameObject.SetActive(wasActive);
    }

    //private IEnumerator FadeToBlack(float duration)
    //{
    //    yield return FadeAlpha(0f, 1f, duration);
    //}

    //private IEnumerator FadeToTransparent(float duration)
    //{
    //    yield return FadeAlpha(worldFadeOverlay?.color.a ?? 1f, 0f, duration);
    //}

    //private IEnumerator FadeAlpha(float from, float to, float duration)
    //{
    //    if (worldFadeOverlay == null)
    //    {
    //        Debug.LogWarning("WorldFadeOverlay manquant !");
    //        yield break;
    //    }

    //    float elapsed = 0f;
    //    while (elapsed < duration)
    //    {
    //        elapsed += Time.unscaledDeltaTime;
    //        float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

    //        Color col = worldFadeOverlay.color;
    //        col.a = Mathf.Lerp(from, to, t);
    //        worldFadeOverlay.color = col;

    //        yield return null;
    //    }

    //    Color final = worldFadeOverlay.color;
    //    final.a = to;
    //    worldFadeOverlay.color = final;
    //}

    private void HideVictoryPanel() => NewBattleManager.Instance.victoryScreen?.transform.GetChild(0).gameObject.SetActive(false);
    private void HideGameOverPanel() => NewBattleManager.Instance.gameOverScreen?.transform.GetChild(0).gameObject.SetActive(false);

    #endregion
}
