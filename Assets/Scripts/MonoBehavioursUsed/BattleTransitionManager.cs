using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;

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

    #endregion

    #region Transition
    /// <summary>
    /// Lance toutes les étapes de la transition vers le mode combat.
    /// </summary>
    public void StartCombatTransition()
    {
        CombatSkyboxManager.Instance?.ApplyBattleSkybox();

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
        // Prépare le champ de bataille
        playerDetection ??= FindFirstObjectByType<PlayerDetection>();
        int battlefieldIndex = playerDetection.detectedEnemies[0].battlefieldIndex;
        Transform battleFieldParent = GameObject.Find("BattleScene/Battlefields").transform;
        GameObject currentBattlefield = Instantiate(ZoneManager.Instance.currentZone.battlefields[battlefieldIndex], battleFieldParent.position, Quaternion.identity);
        currentBattlefield.transform.SetParent(battleFieldParent, false);
        currentBattlefield.gameObject.SetActive(true);

        NewBattleManager.Instance.SpawnAll();

        battleView.SetActive(true);

        CharacterUnit firstUnit = NewBattleManager.Instance.ReturnFirstStrikeCharacter();
        GameObject introCam = GameObject.Find("BattleScene/Camera_BattleIntro");
        if (introCam != null)
        {
            if (firstUnit != null)
                introCam.transform.position = firstUnit.transform.position;

            PlayableDirector introDirector = introCam.GetComponentInChildren<PlayableDirector>();
            if (introDirector != null)
            {
                TimelineManager.Instance.PlayTimeline(introDirector);
                yield return new WaitForSecondsRealtime(1.8f);
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

        // Effet de fissuration + explosion caméra
        GameObject battleCamera = GameObject.FindGameObjectWithTag("BattleCamera");
        if (battleCamera != null)
        {
            Transform fractureParent = battleCamera.transform.GetChild(2);
            fractureParent.gameObject.SetActive(true);

            var explode = fractureParent.GetChild(0).GetComponent<Animator>();
            if (explode != null)
            {
                explode.Play("Glass_Explode");
            }
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
        Time.timeScale = 1f;
        // Restauration du fixedDeltaTime après la pause du VictoryScreen
        Time.fixedDeltaTime = 0.02f;

        CombatSkyboxManager.Instance?.RestoreDefaultSkybox();

        //yield return FadeToBlack(2f);

        var worldEnemies = FindObjectsOfType<Enemy>().Where(e => e.wasPartOfLastBattle).ToList();
        foreach (var enemy in worldEnemies)
        {
            Destroy(enemy);
        }

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

        InputsManager.Instance.ActivateOnly(InputsManager.Instance.playerInputs.World.Get());

        if (Application.isPlaying && !Application.isEditor && SaveAndLoadManager.Instance != null)
        {
            SaveAndLoadManager.Instance.AutoSave();
        }

        //yield return FadeToTransparent(1f);

        yield return new WaitForSecondsRealtime(0);
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

    private IEnumerator SlowTimeScale(float to, float speed)
    {
        float epsilon = 0.001f; // petit seuil pour éviter les flottants imprécis

        while (Time.timeScale - to > epsilon)
        {
            float newScale = Time.timeScale - Time.unscaledDeltaTime * speed;
            if (newScale <= to + epsilon)
                newScale = to;
            // Empêche toute valeur négative due au calcul
            Time.timeScale = Mathf.Max(0f, newScale);

            yield return new WaitForEndOfFrame();
        }

        // Assure qu'à la fin, on a la bonne valeur pile
        Time.timeScale = to;
    }

    private IEnumerator RestoreTimeScale(float from, float to, float speed)
    {
        Debug.Log("Début de la restauration du temps");
        float epsilon = 0.001f;

        while (to - Time.timeScale > epsilon)
        {
            Time.timeScale += Time.unscaledDeltaTime * speed;
            if (Time.timeScale > to)
                Time.timeScale = to;

            yield return new WaitForEndOfFrame();
        }

        Time.timeScale = to;
        Debug.Log("Temps restauré");
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
