using System.Collections;
using System.Collections.Generic;
using System.Linq;              // ← Ajouté pour FirstOrDefault
using UnityEngine;
using UnityEngine.SceneManagement;
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
    [SerializeField] private Transform cameraFocusTarget;

    [Header("Camera Intro - Rotation")]
    [SerializeField] private float introRotationRadius = 8f;
    [SerializeField] private float introRotationSpeed = 60f;
    [SerializeField] private float introRotationHeight = 3f;
    [SerializeField] private float introDuration = 2f;
    [SerializeField] private float introZoomStart = 6f;
    [SerializeField] private float introZoomEnd = 12f;

    [Header("Camera Intro - Translation")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;

    [Header("SFX")]
    public List<AudioClip> transitionSFXClips = new();
    public AudioSource sfxSource;

    [Header("Music")]
    public List<AudioClip> battleMusics = new();
    public AudioSource musicSource;

    private AsyncOperation preloadOp;
    private Camera battleCamera;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);

        worldFadeOverlay = GameObject.Find("WorldFadeOverlayPanel")?.GetComponent<Image>();
        playerDetection = FindFirstObjectByType<PlayerDetection>();
    }

    public void StartCombatTransition()
    {
        StartCoroutine(PlayRandomBattleMusic());
        StartCoroutine(PlayTransitionSoundsSequentially());
        worldRiftTweener.PlayCombatTween(50f, 0.1f, 2f);
        StartCoroutine(FadeToBlack(revealDuration));
        StartCoroutine(TransitionRoutine());

        GameManager.Instance.CurrentState = GameState.BattleTransition;
        InputsManager.Instance.ActivateOnly(InputsManager.Instance.playerInputs.Battle.Get());
        NewBattleManager.Instance.SetBattleInputs();

        Debug.Log("[BattleTransitionManager] Démarrage de la transition de combat...");
    }

    private IEnumerator PlayRandomBattleMusic()
    {
        yield return new WaitForSeconds(1f);

        if (musicSource == null || battleMusics == null || battleMusics.Count == 0)
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

    private IEnumerator TransitionRoutine()
    {
        // 1. Ralenti progressif mais sans bloquer la coroutine
        while (Time.timeScale > 0.1f)
        {
            Time.timeScale -= Time.unscaledDeltaTime * 2f;
            Time.timeScale = Mathf.Max(Time.timeScale, 0.1f); // Sécurité
            yield return new WaitForEndOfFrame(); // Ne dépend pas du timeScale
        }

        //// 2. Lancement du chargement async
        //preloadOp = SceneManager.LoadSceneAsync("BattleScene", LoadSceneMode.Additive);
        //preloadOp.allowSceneActivation = false;

        //while (preloadOp.progress < 0.9f)
        //    yield return new WaitForEndOfFrame(); // Toujours timeScale-safe

        // 3. Attente visuelle avant activation
        yield return new WaitForSecondsRealtime(worldRiftTweener.tweenDuration);

        //preloadOp.allowSceneActivation = true;

        //while (!preloadOp.isDone)
        //    yield return new WaitForEndOfFrame();
        //yield return null;

        // 4. Rétablit le temps progressivement
        while (Time.timeScale < 1f)
        {
            Time.timeScale += Time.unscaledDeltaTime * 2f;
            Time.timeScale = Mathf.Min(Time.timeScale, 1f);
            yield return new WaitForEndOfFrame();
        }

        // 3) Spawn immédiat des unités (avant toute transition visuelle)
        NewBattleManager.Instance.SpawnAll();

        // 4) Initialisation des éléments visuels
        battleCamera = GameObject.FindWithTag(battleCameraTag)?.GetComponent<Camera>();
        if (battleCamera == null)
        {
            Debug.LogError($"[BattleTransitionManager] Caméra taggée '{battleCameraTag}' introuvable !");
            yield break;
        }

        battleCamera.targetTexture = battleRenderTexture;
        battleCamera.enabled = true;
        battleCamera.transform.GetChild(0).gameObject.SetActive(true); // Active l'UI

        battleRiftTweener = FindFirstObjectByType<BattleRiftMaterialTweener>();
        if (battleRiftTweener == null)
        {
            Debug.LogError("[BattleTransitionManager] BattleRiftMaterialTweener introuvable !");
            yield break;
        }

        Canvas battleUICanvas = FindObjectsOfType<Canvas>()
            .FirstOrDefault(c => c.renderMode == RenderMode.ScreenSpaceCamera);
        if (battleUICanvas != null)
            battleUICanvas.worldCamera = battleCamera;

        GameObject battleTransitionCanvas = GameObject.Find("BattleScene_TransitionCanvas")
            ?.transform.GetChild(0).gameObject;
        if (battleTransitionCanvas != null)
            battleTransitionCanvas.SetActive(true);

        battleRevealMask = GameObject.Find("BattleScene_CombatRevealMask_Image");
        if (battleRevealMask == null)
        {
            Debug.LogError("[BattleTransitionManager] Le masque circulaire est introuvable !");
            yield break;
        }

        maskRingParticles = GameObject.Find("BattleScene_CombatRevealParticles")
            ?.GetComponent<ParticleSystem>();

        battleRiftTweener.PlayCombatTween(50f, 0.1f, 1f);

        RectTransform maskRect = battleRevealMask.GetComponent<RectTransform>();
        maskRect.sizeDelta = Vector2.zero;

        if (maskRingParticles != null)
        {
            maskRingParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            maskRingParticles.Play();
        }

        // 5) Animation d’intro (cercle de masque)
        yield return AnimateMaskCircle(maskRect, revealDuration);

        // 6) Fin de la transition visuelle
        EndVisualTransition();

        // 7) Attends que les unités soient bien prêtes (fail-safe, souvent inutile ici)
        while (NewBattleManager.Instance.unitsInBattle.Count <= 0)
            yield return null;

        NewBattleManager.Instance.ChangeBattleState(NewBattleManager.BattleState.Initialization);

        // 8) Rétracte les effets de rift
        worldRiftTweener.TweenToZeroRoutine();
        yield return battleRiftTweener.TweenToZeroRoutine();

        // 9) Cache le canvas de transition
        if (battleTransitionCanvas != null)
            battleTransitionCanvas.SetActive(false);

        NewBattleManager.Instance.LaunchBattle();
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
            mask.sizeDelta = new Vector2(size, size);

            if (maskRingParticles != null)
            {
                var shape = maskRingParticles.shape;
                shape.radius = size / 2f;
            }

            yield return null;
        }

        mask.sizeDelta = new Vector2(maskTargetSize, maskTargetSize);

        if (maskRingParticles != null)
        {
            var shape = maskRingParticles.shape;
            shape.radius = maskTargetSize / 2f;
            maskRingParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void EndVisualTransition()
    {
        // 1) Supprime le RenderTexture pour que la battleCamera rende à l’écran
        battleCamera.targetTexture = null;

        // 2) Cache ou désactive le RawImage qui faisait office de transition
        if (battleRevealMask != null)
        {
            battleRevealMask.SetActive(false);
        }

        // 3) Relance un fade-out progressif pour rendre l’overlay transparent
        if (worldFadeOverlay != null)
        {
            StartCoroutine(FadeToTransparent(0.5f));
        }
    }

    private IEnumerator FadeToBlack(float duration)
    {
        yield return new WaitForSeconds(2f);

        float elapsed = 0f;
        float startAlpha = 0f;
        float endAlpha = 1f;

        if (worldFadeOverlay == null)
        {
            Debug.LogWarning("WorldFadeOverlay manquant !");
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            Color col = worldFadeOverlay.color;
            col.a = Mathf.Lerp(startAlpha, endAlpha, t);
            worldFadeOverlay.color = col;

            yield return null;
        }

        Color final = worldFadeOverlay.color;
        final.a = endAlpha;
        worldFadeOverlay.color = final;
    }

    private IEnumerator FadeToTransparent(float duration)
    {
        float elapsed = 0f;
        float startAlpha = worldFadeOverlay.color.a;
        float endAlpha = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            Color col = worldFadeOverlay.color;
            col.a = Mathf.Lerp(startAlpha, endAlpha, t);
            worldFadeOverlay.color = col;

            yield return null;
        }

        Color final = worldFadeOverlay.color;
        final.a = endAlpha;
        worldFadeOverlay.color = final;
    }

    public IEnumerator MoveCameraToPosition(Camera cam, Vector3 from, Vector3 to, Transform lookAtTarget, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            cam.transform.position = Vector3.Lerp(from, to, t);
            if (lookAtTarget != null)
            {
                cam.transform.LookAt(lookAtTarget.position);
            }

            yield return null;
        }

        cam.transform.position = to;
        if (lookAtTarget != null)
            cam.transform.LookAt(lookAtTarget.position);
    }

    //public void EndCombatTransition()
    //{
    //    StartCoroutine(UnloadCombat());
    //}

    //private IEnumerator UnloadCombat()
    //{
    //    SceneManager.UnloadSceneAsync("BattleScene");
    //    yield return null;
    //}

    public IEnumerator ExitCombatRoutine()
    {
        Debug.Log("[BattleTransitionManager] Sortie du combat...");

        // 2) Fade vers noir
        yield return FadeToBlack(1f);

        //// 5) Décharge la scène de combat
        //yield return SceneManager.UnloadSceneAsync("BattleScene");

        AudioManager.Instance.ReturnFromCombat();

        // 7) Reset visuel (overlay)
        yield return FadeToTransparent(1f);
    }
}
