using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;
using UnityEngine.InputSystem; // Nécessaire pour utiliser les actions d'input

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

    [Header("Music")]
    [SerializeField] private AudioSource musicSource;

    private Camera battleCamera;

    [Header("Views")]
    public GameObject worldView;
    public GameObject battleView;
    public GameObject battleTransition;

    [Header("UI")]
    [SerializeField] private GameObject qteCircle;
    [SerializeField] private GameObject battleTimeline;
    [SerializeField] private GameObject passTurnButton;
    [SerializeField] private GameObject actionDisplayPanel;

    [Header("Versus Screen")]
    [SerializeField] private GameObject versusCamera; // Portrait du premier allié
    [SerializeField] private GameObject brokenGlass;
    [SerializeField] private GameObject versusTransition; // Transition du Versus
    [SerializeField] private Image SU1; // Portrait du premier allié
    [SerializeField] private Image SU2; // Portrait du second allié
    [SerializeField] private Image SU3; // Portrait du troisième allié
    [SerializeField] private Image EU1; // Portrait du premier ennemi
    [SerializeField] private Image EU2; // Portrait du second ennemi
    [SerializeField] private Image EU3; // Portrait du troisième ennemi
    [SerializeField] private Animator unitSpawnPointsAnimator; // Animator jouant l'animation Versus

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
            if (i < squad.Count && squad[i] != null && squad[i].portrait != null)
            {
                // Affecte le portrait du personnage
                squadImages[i].sprite = squad[i].portrait;
                // Assure une opacité totale
                Color c = squadImages[i].color; c.a = 1f; squadImages[i].color = c;
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
            if (i < enemies.Count && enemies[i] != null && enemies[i].portrait != null)
            {
                enemyImages[i].sprite = enemies[i].portrait;
                Color c = enemyImages[i].color; c.a = 1f; enemyImages[i].color = c;
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
        battleCamera = GameObject.FindGameObjectWithTag("BattleCamera")?.GetComponent<Camera>();
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
    /// Lance toutes les étapes de la transition vers le mode combat.
    /// </summary>
    public void StartCombatTransition()
    {
        CombatSkyboxManager.Instance?.ApplyBattleSkybox();

        // Masque l'interface jusqu'au premier tour du joueur
        HideBattleUI();

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
        // Prépare le champ de bataille sans instanciation coûteuse
        playerDetection ??= FindFirstObjectByType<PlayerDetection>();
        int battlefieldIndex = playerDetection.detectedEnemies[0].battlefieldIndex;
        BattlefieldManager.Instance.SetBattlefield(battlefieldIndex);

        // Effet de distorsion pour accentuer le passage en mode combat
        if (PostProcessManager.Instance != null)
            yield return StartCoroutine(PostProcessManager.Instance.PulseLensDistortion(1f, 1f));

        battleView.SetActive(true);
        versusCamera.SetActive(true);

        // Met à jour les portraits du Versus et lance l'animation d'apparition
        UpdateVersusPortraits();
        unitSpawnPointsAnimator?.Play("VersusLaunched");

        // --- Attente de la confirmation du joueur ---
        // On active temporairement l'action "Confirm" afin de laisser le joueur
        // valider l'écran de Versus à son rythme avant de poursuivre la transition.
        InputAction confirmAction = InputsManager.Instance.playerInputs.Battle.Confirm;
        confirmAction.Enable();
        yield return new WaitUntil(() => confirmAction.triggered);
        confirmAction.Disable();

        if (battleTransition != null)
        {
            battleTransition.SetActive(true);
            versusTransition.SetActive(false);
            brokenGlass.SetActive(true);
            Animator glass = brokenGlass.GetComponent<Animator>();
            glass.Play("Glass_Explode");
        }

        yield return new WaitForSecondsRealtime(0.5f);

        // Les unités apparaissent après l'explosion
        NewBattleManager.Instance.SpawnAll();

        CharacterUnit firstUnit = NewBattleManager.Instance.ReturnFirstStrikeCharacter();
        GameObject introCam = GameObject.Find("BattleScene/Camera_BattleIntro");
        if (introCam != null)
        {
            if (firstUnit != null)
                introCam.transform.position = firstUnit.transform.position;

            PlayableDirector introDirector = introCam.GetComponentInChildren<PlayableDirector>();
            if (introDirector != null)
            {
                // On joue la Timeline d'introduction
                TimelineManager.Instance.PlayTimeline(introDirector);
                // On attend la fin de la timeline pour éviter l'affichage de l'UI pendant la cinématique
                yield return new WaitUntil(() => !TimelineManager.Instance.IsTimelinePlaying);
            }
            else
            {
                Debug.LogWarning("[BattleTransitionManager] Timeline d'intro introuvable.");
            }
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

        yield return NewBattleManager.Instance.StartBattle();
    }


    public IEnumerator ExitVictoryScreenAndBattle()
    {
        // Restauration progressive du temps vers la normale
        yield return StartCoroutine(RestoreTimeScale(Time.timeScale, 1f, 2f));

        CombatSkyboxManager.Instance?.RestoreDefaultSkybox();

        //yield return FadeToBlack(2f);

        // On récupère l'indice du champ de bataille utilisé pendant ce combat
        int battlefieldIndex = 0;
        if (NewBattleManager.Instance != null && NewBattleManager.Instance.enemyTemplates.Count > 0)
            battlefieldIndex = NewBattleManager.Instance.enemyTemplates[0].battlefieldIndex;

        // Suppression des ennemis vaincus présents dans le monde
        var worldEnemies = FindObjectsOfType<Enemy>().Where(e => e.wasPartOfLastBattle).ToList();
        foreach (var enemy in worldEnemies)
        {
            Destroy(enemy);
        }

        // Réinstanciation des battlefields pour revenir à un état propre et
        // conservation uniquement de celui correspondant au combat effectué
        BattlefieldManager.Instance?.RebuildBattlefieldsKeeping(battlefieldIndex);

        GameManager.Instance.ChangeGameState(GameState.Exploration);

        playerDetection ??= FindFirstObjectByType<PlayerDetection>();
        playerDetection.ResetDetection(1f);

        //Switch Battle vers World
        if (worldView != null && battleView != null)
        {
            worldView.SetActive(true);
            battleView.SetActive(false);
        }

        if (maskRingParticles != null)
        {
            maskRingParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            maskRingParticles.Play();
        }

        HideVictoryPanel();
        HideGameOverPanel();
        ResetBattleFlagsOnAllEnemies();
        NewBattleManager.Instance.ResetBattleInfos();

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

        //yield return FadeToTransparent(1f);

        yield return new WaitForSecondsRealtime(0);
    }

    /// <summary>
    /// Réinitialise tous les éléments activés lors du lancement du combat afin
    /// d'éviter qu'ils restent visibles ou actifs une fois le combat terminé.
    /// </summary>
    private void ResetTransitionObjects()
    {
        // On désactive la caméra dédiée à l'écran de Versus si elle existe
        if (versusCamera != null)
            versusCamera.SetActive(false);

        // On masque les objets de transition utilisés pendant l'entrée en combat
        if (battleTransition != null)
            battleTransition.SetActive(false);

        if (versusTransition != null)
            versusTransition.SetActive(true); // prêt pour un futur combat

        if (brokenGlass != null)
            brokenGlass.SetActive(false);

        // La caméra de combat est repliée pour garantir un retour propre à l'exploration
        if (battleCamera != null)
            battleCamera.gameObject.SetActive(false);

        // Le canvas de transition est désactivé pour éviter tout résidu visuel
        GameObject.Find("BattleScene_TransitionCanvas")?.transform.GetChild(0).gameObject.SetActive(false);
    }

    void SetupBattleCameraAndUI()
    {
        if (battleCamera == null)
        {
            Debug.LogError($"[BattleTransitionManager] Caméra taggée BattleCamera' introuvable !");
        }

        // Active le GameObject principal de la caméra ainsi que tous ses enfants
        // pour s'assurer que l'affichage se fasse correctement
        battleCamera.gameObject.SetActive(true);
        foreach (Transform child in battleCamera.transform)
        {
            child.gameObject.SetActive(true);
        }

        var battleUICanvas = FindObjectsOfType<Canvas>().FirstOrDefault(c => c.renderMode == RenderMode.ScreenSpaceCamera);
        if (battleUICanvas != null)
            battleUICanvas.worldCamera = battleCamera;

        GameObject.Find("BattleScene_TransitionCanvas")?.transform.GetChild(0).gameObject.SetActive(true);
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

    /// <summary>
    /// Nettoie le flag de participation au combat pour tous les ennemis.
    /// </summary>
    public void ResetBattleFlagsOnAllEnemies()
    {
        foreach (var enemy in FindObjectsOfType<Enemy>())
            enemy.wasPartOfLastBattle = false;
    }

    #endregion
}
