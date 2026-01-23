using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Centralise les references de la scene de combat pour les managers.
/// </summary>
public sealed class BattleSceneBindings : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private GameObject worldScene;
    [SerializeField] private Image worldFadeOverlay;

    [Header("Cameras")]
    [SerializeField] private GameObject battleCamera;
    [SerializeField] private Camera battleCameraCam;
    [SerializeField] private Transform battleCameraRig;
    [SerializeField] private Transform battleCameraCanvasTransform;
    [SerializeField] private GameObject battleCameraCanvas;
    [SerializeField] private Camera versusCamera;

    [Header("Battle UI")]
    [SerializeField] private GameObject qteCircle;
    [SerializeField] private GameObject battleTimeline;
    [SerializeField] private GameObject passTurnButton;
    [SerializeField] private GameObject actionDisplayPanel;
    [SerializeField] private GameObject victoryScreen;
    [SerializeField] private GameObject gameOverScreen;

    [Header("Spawn Roots")]
    [SerializeField] private Transform playerSpawnRoot;
    [SerializeField] private Transform enemySpawnRoot;

    [Header("Transitions")]
    [SerializeField] private GameObject battleSceneTransitionCanvas;
    [SerializeField] private Transform enemyPosition01;
    [SerializeField] private GameObject playerRoot;

    [Header("Versus UI")]
    [SerializeField] private GameObject versusCameraCanvas;
    [SerializeField] private GameObject brokenGlass;
    [SerializeField] private GameObject versusTransition;
    [SerializeField] private Image su1;
    [SerializeField] private Image su2;
    [SerializeField] private Image su3;
    [SerializeField] private Image eu1;
    [SerializeField] private Image eu2;
    [SerializeField] private Image eu3;
    [SerializeField] private Animator unitSpawnPointsAnimator;
    [SerializeField] private List<GameObject> continuePrompts = new();

    public GameObject WorldScene => worldScene;
    public Image WorldFadeOverlay => worldFadeOverlay;
    public GameObject BattleCamera => battleCamera;
    public Camera BattleCameraCam => battleCameraCam;
    public Transform BattleCameraRig => battleCameraRig;
    public Transform BattleCameraCanvasTransform => battleCameraCanvasTransform;
    public GameObject BattleCameraCanvas => battleCameraCanvas;
    public Camera VersusCamera => versusCamera;
    public GameObject QteCircle => qteCircle;
    public GameObject BattleTimeline => battleTimeline;
    public GameObject PassTurnButton => passTurnButton;
    public GameObject ActionDisplayPanel => actionDisplayPanel;
    public GameObject VictoryScreen => victoryScreen;
    public GameObject GameOverScreen => gameOverScreen;
    public Transform PlayerSpawnRoot => playerSpawnRoot;
    public Transform EnemySpawnRoot => enemySpawnRoot;
    public GameObject BattleSceneTransitionCanvas => battleSceneTransitionCanvas;
    public Transform EnemyPosition01 => enemyPosition01;
    public GameObject PlayerRoot => playerRoot;
    public GameObject VersusCameraCanvas => versusCameraCanvas;
    public GameObject BrokenGlass => brokenGlass;
    public GameObject VersusTransition => versusTransition;
    public Image SU1 => su1;
    public Image SU2 => su2;
    public Image SU3 => su3;
    public Image EU1 => eu1;
    public Image EU2 => eu2;
    public Image EU3 => eu3;
    public Animator UnitSpawnPointsAnimator => unitSpawnPointsAnimator;
    public List<GameObject> ContinuePrompts => continuePrompts;

    private void Awake()
    {
        ServiceRegistry.Register(this);
    }

    private void OnDestroy()
    {
        ServiceRegistry.Unregister(this);
    }

#if UNITY_EDITOR
    [ContextMenu("Auto Bind From Scene")]
    private void AutoBindFromScene()
    {
        if (battleCamera == null)
            battleCamera = FindByTagSafe("BattleCamera");
        if (versusCamera == null)
            versusCamera = FindByTagSafe("VersusCamera")?.GetComponent<Camera>();

        if (battleCameraCam == null && battleCamera != null)
            battleCameraCam = FindCameraByName(battleCamera, "BattleCamera_Cam");

        if (battleCameraCanvasTransform == null && battleCamera != null)
            battleCameraCanvasTransform = FindChildByName(battleCamera.transform, "BattleCameraCanvas");

        if (battleCameraCanvas == null && battleCameraCanvasTransform != null)
            battleCameraCanvas = battleCameraCanvasTransform.gameObject;

        worldScene = worldScene != null ? worldScene : GameObject.Find("WorldScene");
        worldFadeOverlay = worldFadeOverlay != null ? worldFadeOverlay : GameObject.Find("WorldFadeOverlayPanel")?.GetComponent<Image>();
        battleSceneTransitionCanvas = battleSceneTransitionCanvas != null ? battleSceneTransitionCanvas : GameObject.Find("BattleScene_TransitionCanvas");
        enemyPosition01 = enemyPosition01 != null ? enemyPosition01 : GameObject.Find("EnemyPosition_01")?.transform;
        playerRoot = playerRoot != null ? playerRoot : FindByTagSafe("Player");

        if (playerSpawnRoot == null)
            playerSpawnRoot = FindByTagSafe("PlayerSpawn")?.transform;
        if (enemySpawnRoot == null)
            enemySpawnRoot = FindByTagSafe("EnemySpawn")?.transform;

        qteCircle = qteCircle != null ? qteCircle : GameObject.Find("QTECircle");
        battleTimeline = battleTimeline != null ? battleTimeline : GameObject.Find("BattleTimeline");
        passTurnButton = passTurnButton != null ? passTurnButton : GameObject.Find("PassTurnButton");
        actionDisplayPanel = actionDisplayPanel != null ? actionDisplayPanel : GameObject.Find("ActionDisplayPanel");
        victoryScreen = victoryScreen != null ? victoryScreen : GameObject.Find("VictoryScreen");
        gameOverScreen = gameOverScreen != null ? gameOverScreen : GameObject.Find("GameOverScreen");

        versusCameraCanvas = versusCameraCanvas != null ? versusCameraCanvas : GameObject.Find("VersusCameraCanvas");
        brokenGlass = brokenGlass != null ? brokenGlass : GameObject.Find("BrokenGlass");
        versusTransition = versusTransition != null ? versusTransition : GameObject.Find("VersusTransition");

        EditorUtility.SetDirty(this);
    }

    private static GameObject FindByTagSafe(string tag)
    {
        try
        {
            return GameObject.FindGameObjectWithTag(tag);
        }
        catch (UnityException)
        {
            return null;
        }
    }

    private static Camera FindCameraByName(GameObject root, string name)
    {
        if (root == null || string.IsNullOrWhiteSpace(name))
            return null;

        foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
        {
            if (cam != null && cam.name == name)
                return cam;
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrWhiteSpace(name))
            return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
                return child;
        }

        return null;
    }
#endif
}
