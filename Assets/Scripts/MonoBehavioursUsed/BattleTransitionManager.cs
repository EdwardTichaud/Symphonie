using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class BattleTransitionManager : MonoBehaviour
{
    public static BattleTransitionManager Instance { get; private set; }

    private PlayerDetection playerDetection;

    [Header("RT_Combat")]
    [SerializeField] private RenderTexture battleRenderTexture;

    [Header("Rifters")]
    [SerializeField] private WorldRiftMaterialTweener worldRiftTweener;
    private BattleRiftMaterialTweener battleRiftTweener;

    [Header("Tags")]
    [SerializeField] private string battleCameraTag = "BattleCamera";

    [Header("Ressources Visuals")]
    [SerializeField] private GameObject battleRevealMask;
    [SerializeField] private float revealDuration = 1f;
    [SerializeField] private Image worldFadeOverlay;
    [SerializeField] private ParticleSystem maskRingParticles;
    [SerializeField] private float maskTargetSize = 4500f;

    [Header("Camera Intro")]
    [SerializeField] CameraPath battleIntroPath;

    [Header("SFX")]
    [SerializeField] private List<AudioClip> transitionSFXClips = new();
    [SerializeField] private AudioSource sfxSource;

    [Header("Music")]
    [SerializeField] private List<AudioClip> battleMusics = new();
    [SerializeField] private AudioSource musicSource;

    private Camera battleCamera;

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
        battleCamera = GameObject.FindGameObjectWithTag(battleCameraTag)?.GetComponent<Camera>();
    }

    #endregion

    #region Transition
    /// <summary>
    /// Lance toutes les étapes de la transition vers le mode combat.
    /// </summary>
    public void StartCombatTransition()
    {
        StopAllCoroutines();

        CombatSkyboxManager.Instance?.ApplyBattleSkybox();

        AudioClip randomClip = battleMusics[Random.Range(0, battleMusics.Count)];
        AudioManager.Instance.TransitionToCombat(randomClip);
        StartCoroutine(PlayTransitionSoundsSequentially());
        StartCoroutine(TransitionRoutine());

        GameManager.Instance.CurrentState = GameState.BattleTransition;
        InputsManager.Instance.ActivateOnly(InputsManager.Instance.playerInputs.Battle.Get());

        Debug.Log("[BattleTransitionManager] Transition de combat démarrée.");
    }

    /// <summary>
    /// Enchaîne les différentes étapes visuelles et logiques de la transition.
    /// </summary>
    private IEnumerator TransitionRoutine()
    {
        yield return SlowTimeScale(to: 0.1f, speed: 2f);
        yield return new WaitForSecondsRealtime(worldRiftTweener.tweenDuration);

        playerDetection ??= FindFirstObjectByType<PlayerDetection>();
        int battlefieldIndex = playerDetection.detectedEnemies[0].battlefieldIndex;
        Transform battleFieldParent = GameObject.Find("BattleScene_Battlefields").transform;
        GameObject currentBattlefield = Instantiate(ZoneManager.Instance.currentZone.battlefields[battlefieldIndex], battleFieldParent.position, Quaternion.identity);
        currentBattlefield.transform.SetParent(battleFieldParent, false);
        currentBattlefield.gameObject.SetActive(true);

        battleCamera = null;
        battleCamera = GameObject.FindGameObjectWithTag(battleCameraTag)?.GetComponent<Camera>();
        battleCamera.enabled = true;
        battleCamera.targetTexture = battleRenderTexture;

        NewBattleManager.Instance.SpawnAll();

        if (!SetupBattleCameraAndUI())
            yield break;

        if (battleIntroPath != null)
        {
            battleIntroPath.PlaySequence();
            Debug.Log("[BattleTransitionManager] CameraPath Intro Combat lancé !");
        }
        else
        {
            Debug.LogWarning("[BattleTransitionManager] Aucun CameraPath défini pour l'intro combat !");
        }

        battleRiftTweener = FindFirstObjectByType<BattleRiftMaterialTweener>();
        if (battleRiftTweener == null)
        {
            Debug.LogError("[BattleTransitionManager] BattleRiftMaterialTweener introuvable !");
            yield break;
        }

        battleRiftTweener.PlayCombatTween(50f, 0.1f, 1f);

        if (battleRevealMask == null)
        {
            Debug.LogError("[BattleTransitionManager] CombatRevealMask introuvable !");
            yield break;
        }

        battleRevealMask.SetActive(true);
        RectTransform maskRect = battleRevealMask.GetComponent<RectTransform>();
        maskRect.sizeDelta = Vector2.zero;

        if (maskRingParticles != null)
        {
            maskRingParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            maskRingParticles.Play();
        }

        yield return AnimateMaskCircle(maskRect, revealDuration);

        while (NewBattleManager.Instance.unitsInBattle.Count <= 0)
            yield return null;

        worldRiftTweener.TweenToZeroRoutine();
        yield return battleRiftTweener.TweenToZeroRoutine();

        NewBattleManager.Instance.ChangeBattleState(BattleState.Initialization); // Lance CameraPathIntro

        while (battleIntroPath.IsPlaying)
        {
            Debug.Log("[BattleTransitionManager] Attente de la fin du CameraPath Intro Combat...");
            yield return null;
        }

        yield return NewBattleManager.Instance.StartBattle();
        yield return RestoreTimeScale(from: 0.1f, to: 1f, speed: 2f);
    }

    public IEnumerator ExitVictoryScreenAndBattle()
    {
        Time.timeScale = 1f;

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

        battleRevealMask.SetActive(true);
        RectTransform maskRect = battleRevealMask.GetComponent<RectTransform>();
        maskRect.sizeDelta = Vector2.zero;

        if (maskRingParticles != null)
        {
            maskRingParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            maskRingParticles.Play();
        }

        yield return AnimateMaskCircleReverse(maskRect, revealDuration);

        HideVictoryPanel();
        HideGameOverPanel();
        ResetBattleFlagsOnAllEnemies();
        NewBattleManager.Instance.ResetBattleInfos();

        AudioManager.Instance.ReturnFromBattle();

        InputsManager.Instance.ActivateOnly(InputsManager.Instance.playerInputs.World.Get());

        battleIntroPath.triggered = false;

        if (Application.isPlaying && !Application.isEditor && SaveAndLoadManager.Instance != null)
        {
            SaveAndLoadManager.Instance.AutoSave();
        }

        //yield return FadeToTransparent(1f);
    }

    /// <summary>
    /// Libère les références caméras et visuels temporaires après la transition.
    /// </summary>
    private void ResetCameraAndVisuals()
    {
        battleCamera = null;
        battleRevealMask = null;
        maskRingParticles = null;
    }

    private bool SetupBattleCameraAndUI()
    {
        if (battleCamera == null)
        {
            Debug.LogError($"[BattleTransitionManager] Caméra taggée '{battleCameraTag}' introuvable !");
            return false;
        }

        battleCamera.targetTexture = battleRenderTexture;

        if (battleCamera.transform.childCount > 0)
            battleCamera.transform.GetChild(0).gameObject.SetActive(true);

        var battleUICanvas = FindObjectsOfType<Canvas>().FirstOrDefault(c => c.renderMode == RenderMode.ScreenSpaceCamera);
        if (battleUICanvas != null)
            battleUICanvas.worldCamera = battleCamera;

        GameObject.Find("BattleScene_TransitionCanvas")?.transform.GetChild(0).gameObject.SetActive(true);

        return true;
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
        float epsilon = 0.001f;

        while (to - Time.timeScale > epsilon)
        {
            Time.timeScale += Time.unscaledDeltaTime * speed;
            if (Time.timeScale > to)
                Time.timeScale = to;

            yield return new WaitForEndOfFrame();
        }

        Time.timeScale = to;
    }

    private void EndVisualTransition()
    {
        //if (battleCamera != null)
        //    battleCamera.targetTexture = null;

        //if (worldFadeOverlay != null)
        //    StartCoroutine(FadeToTransparent(0.5f));
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

    private IEnumerator AnimateMaskCircle(RectTransform mask, float duration)
    {
        float elapsed = 0f;
        ParticleSystem.ShapeModule shape = default;
        if (maskRingParticles != null)
            shape = maskRingParticles.shape;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            float size = Mathf.Lerp(0f, maskTargetSize, t);
            mask.sizeDelta = Vector2.one * size;

            if (maskRingParticles != null)
            {
                shape.radius = size / 2f;
            }
            yield return null;
        }

        mask.sizeDelta = Vector2.one * maskTargetSize;

        if (maskRingParticles != null)
        {
            shape.radius = maskTargetSize / 2f;
            maskRingParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private IEnumerator AnimateMaskCircleReverse(RectTransform mask, float duration)
    {
        float elapsed = 0f;
        ParticleSystem.ShapeModule shape = default;
        if (maskRingParticles != null)
            shape = maskRingParticles.shape;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            float size = Mathf.Lerp(maskTargetSize, 0f, t); // inversé ici !
            mask.sizeDelta = Vector2.one * size;

            if (maskRingParticles != null)
            {
                shape.radius = size / 2f;
            }

            yield return null;
        }

        mask.sizeDelta = Vector2.zero;

        if (maskRingParticles != null)
        {
            shape.radius = 0f;
            maskRingParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
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
