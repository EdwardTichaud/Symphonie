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
    }

    public void StartCombatTransition()
    {
        StopAllCoroutines();
        //ResetCameraAndVisuals();

        StartCoroutine(PlayRandomBattleMusic());
        StartCoroutine(PlayTransitionSoundsSequentially());
        StartCoroutine(FadeToBlack(revealDuration));
        StartCoroutine(TransitionRoutine());

        GameManager.Instance.CurrentState = GameState.BattleTransition;
        InputsManager.Instance.ActivateOnly(InputsManager.Instance.playerInputs.Battle.Get());
        NewBattleManager.Instance.SetBattleInputs();

        Debug.Log("[BattleTransitionManager] Transition de combat démarrée.");
    }

    private IEnumerator TransitionRoutine()
    {
        yield return SlowTimeScale(to: 0.1f, speed: 2f);
        yield return new WaitForSecondsRealtime(worldRiftTweener.tweenDuration);

        yield return RestoreTimeScale(from: 0.1f, to: 1f, speed: 2f);

        NewBattleManager.Instance.SpawnAll();

        if (!SetupBattleCameraAndUI())
            yield break;

        if (battleIntroPath != null)
        {
            CameraController.Instance.StartPathFollow(battleIntroPath, "BattleCamera", 0, true);
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
        CameraController.Instance.SetCamerasDepth(0f, 1f);
        RectTransform maskRect = battleRevealMask.GetComponent<RectTransform>();
        maskRect.sizeDelta = Vector2.zero;

        if (maskRingParticles != null)
        {
            maskRingParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            maskRingParticles.Play();
        }

        yield return AnimateMaskCircle(maskRect, revealDuration);

        EndVisualTransition();

        while (NewBattleManager.Instance.unitsInBattle.Count <= 0)
            yield return null;

        worldRiftTweener.TweenToZeroRoutine();
        yield return battleRiftTweener.TweenToZeroRoutine();

        if (TimelineManager.Instance.IsTimelinePlaying == false)
        {
            NewBattleManager.Instance.LaunchBattle();
        }       
    }

    public IEnumerator ExitVictoryScreenAndBattle()
    {
        Time.timeScale = 1f;
        yield return FadeToBlack(2f);

        var worldEnemies = FindObjectsOfType<Enemy>().Where(e => e.wasPartOfLastBattle).ToList();
        foreach (var enemy in worldEnemies)
            enemy?.DissolveFadeOff();

        GameManager.Instance.CurrentState = GameState.Exploration;

        playerDetection ??= FindFirstObjectByType<PlayerDetection>();
        playerDetection.ResetDetection();

        HideVictoryPanel();
        HideGameOverPanel();
        ResetBattleFlagsOnAllEnemies();

        CameraController.Instance.SetCamerasDepth(1f, 0f);

        battleCamera ??= GameObject.FindGameObjectWithTag(battleCameraTag)?.GetComponent<Camera>();
        if (battleCamera != null)
        {
            if (battleCamera.transform.childCount > 0)
                battleCamera.transform.GetChild(0).gameObject.SetActive(false);
        }

        AudioManager.Instance.ReturnFromBattle();
        yield return FadeToTransparent(1f);
    }

    private void ResetCameraAndVisuals()
    {
        battleCamera = null;
        battleRevealMask = null;
        maskRingParticles = null;
    }

    private bool SetupBattleCameraAndUI()
    {
        battleCamera = GameObject.FindWithTag(battleCameraTag)?.GetComponent<Camera>();
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
        while (Time.timeScale > to)
        {
            Time.timeScale -= Time.unscaledDeltaTime * speed;
            Time.timeScale = Mathf.Max(Time.timeScale, to);
            yield return new WaitForEndOfFrame();
        }
    }

    private IEnumerator RestoreTimeScale(float from, float to, float speed)
    {
        while (Time.timeScale < to)
        {
            Time.timeScale += Time.unscaledDeltaTime * speed;
            Time.timeScale = Mathf.Min(Time.timeScale, to);
            yield return new WaitForEndOfFrame();
        }
    }

    private void EndVisualTransition()
    {
        if (battleCamera != null)
            battleCamera.targetTexture = null;

        if (battleRevealMask != null)
            battleRevealMask.SetActive(false);

        if (worldFadeOverlay != null)
            StartCoroutine(FadeToTransparent(0.5f));
    }

    private IEnumerator PlayRandomBattleMusic()
    {
        yield return new WaitForSeconds(1f);
        if (musicSource == null || battleMusics.Count == 0)
        {
            Debug.LogWarning("[BattleMusic] AudioSource ou liste vide !");
            yield break;
        }

        AudioClip selected = battleMusics[Random.Range(0, battleMusics.Count)];
        musicSource.clip = selected;
        musicSource.loop = true;
        musicSource.Play();

        Debug.Log($"[BattleMusic] Lecture de : {selected.name}");
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
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            float size = Mathf.Lerp(0f, maskTargetSize, t);
            mask.sizeDelta = Vector2.one * size;

            if (maskRingParticles != null)
            {
                var shape = maskRingParticles.shape;
                shape.radius = size / 2f;
            }
            yield return null;
        }

        mask.sizeDelta = Vector2.one * maskTargetSize;

        if (maskRingParticles != null)
        {
            var shape = maskRingParticles.shape;
            shape.radius = maskTargetSize / 2f;
            maskRingParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private IEnumerator FadeToBlack(float duration)
    {
        yield return FadeAlpha(0f, 1f, duration);
    }

    private IEnumerator FadeToTransparent(float duration)
    {
        yield return FadeAlpha(worldFadeOverlay?.color.a ?? 1f, 0f, duration);
    }

    private IEnumerator FadeAlpha(float from, float to, float duration)
    {
        if (worldFadeOverlay == null)
        {
            Debug.LogWarning("WorldFadeOverlay manquant !");
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            Color col = worldFadeOverlay.color;
            col.a = Mathf.Lerp(from, to, t);
            worldFadeOverlay.color = col;

            yield return null;
        }

        Color final = worldFadeOverlay.color;
        final.a = to;
        worldFadeOverlay.color = final;
    }

    private void HideVictoryPanel() => NewBattleManager.Instance.victoryScreen?.transform.GetChild(0).gameObject.SetActive(false);
    private void HideGameOverPanel() => NewBattleManager.Instance.gameOverScreen?.transform.GetChild(0).gameObject.SetActive(false);

    public void ResetBattleFlagsOnAllEnemies()
    {
        foreach (var enemy in FindObjectsOfType<Enemy>())
            enemy.wasPartOfLastBattle = false;
    }
}
