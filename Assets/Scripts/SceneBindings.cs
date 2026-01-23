using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Central registry for scene object references to avoid fragile name/tag lookups.
/// </summary>
public sealed class SceneBindings : MonoBehaviour
{
    public static SceneBindings Instance { get; private set; }

    [Serializable]
    private struct NamedBinding
    {
        public string name;
        public GameObject value;
    }

    [Serializable]
    private struct TaggedBinding
    {
        public string tag;
        public GameObject value;
    }

    [Header("Tag bindings")]
    [SerializeField] private GameObject player;
    [SerializeField] private GameObject worldCamera;
    [SerializeField] private GameObject battleCamera;
    [SerializeField] private GameObject versusCamera;

    [Header("Name bindings")]
    [SerializeField] private GameObject worldScene;
    [SerializeField] private GameObject worldFadeOverlayPanel;
    [SerializeField] private GameObject battleCameraCam;
    [SerializeField] private GameObject battleSceneTransitionCanvas;
    [SerializeField] private Transform enemyPosition01;
    [SerializeField] private Transform battlefieldParent;
    [SerializeField] private GameObject localInfoBoxCanvas;
    [SerializeField] private GameObject battleCameraCanvas;
    [SerializeField] private GameObject timelineLauncher;
    [SerializeField] private GameObject timelineManagerCanvas;
    [SerializeField] private GameObject qtePanel;
    [SerializeField] private GameObject qteCirclesPanel;

    [Header("Extra bindings")]
    [SerializeField] private List<NamedBinding> extraNames = new();
    [SerializeField] private List<TaggedBinding> extraTags = new();

    private readonly Dictionary<string, GameObject> nameLookup = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GameObject> tagLookup = new(StringComparer.Ordinal);

    public GameObject Player => player;
    public Camera WorldCamera => worldCamera != null ? worldCamera.GetComponent<Camera>() : null;
    public GameObject BattleCamera => battleCamera;
    public Camera BattleCameraComponent => battleCamera != null ? battleCamera.GetComponent<Camera>() : null;
    public Camera VersusCamera => versusCamera != null ? versusCamera.GetComponent<Camera>() : null;
    public GameObject WorldScene => worldScene;
    public GameObject WorldFadeOverlayPanel => worldFadeOverlayPanel;
    public GameObject BattleCameraCam => battleCameraCam;
    public GameObject BattleSceneTransitionCanvas => battleSceneTransitionCanvas;
    public Transform EnemyPosition01 => enemyPosition01;
    public Transform BattlefieldParent => battlefieldParent;
    public GameObject LocalInfoBoxCanvas => localInfoBoxCanvas;
    public GameObject BattleCameraCanvas => battleCameraCanvas;
    public GameObject TimelineLauncher => timelineLauncher;
    public GameObject TimelineManagerCanvas => timelineManagerCanvas;
    public GameObject QtePanel => qtePanel;
    public GameObject QteCirclesPanel => qteCirclesPanel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[SceneBindings] Duplicate instance detected, destroying extra.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RebuildLookups();
        ServiceRegistry.Register(this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        ServiceRegistry.Unregister(this);
    }

    public static bool TryGetByName(string name, out GameObject value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        SceneBindings instance = Instance;
        if (instance == null)
            return false;

        return instance.TryGetByNameInternal(name, out value);
    }

    public static bool TryGetByTag(string tag, out GameObject value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        SceneBindings instance = Instance;
        if (instance == null)
            return false;

        return instance.TryGetByTagInternal(tag, out value);
    }

    private bool TryGetByNameInternal(string name, out GameObject value)
    {
        if (!nameLookup.TryGetValue(name, out value))
            return false;

        if (value == null)
        {
            nameLookup.Remove(name);
            return false;
        }

        return true;
    }

    private bool TryGetByTagInternal(string tag, out GameObject value)
    {
        if (!tagLookup.TryGetValue(tag, out value))
            return false;

        if (value == null)
        {
            tagLookup.Remove(tag);
            return false;
        }

        return true;
    }

    private void RebuildLookups()
    {
        nameLookup.Clear();
        tagLookup.Clear();

        AddTag("Player", player);
        AddTag("WorldCamera", worldCamera);
        AddTag("BattleCamera", battleCamera);
        AddTag("VersusCamera", versusCamera);

        AddName("WorldScene", worldScene);
        AddName("WorldFadeOverlayPanel", worldFadeOverlayPanel);
        AddName("BattleCamera_Cam", battleCameraCam);
        AddName("BattleScene_TransitionCanvas", battleSceneTransitionCanvas);
        AddName("EnemyPosition_01", enemyPosition01 != null ? enemyPosition01.gameObject : null);
        AddName("BattleScene/Battlefields", battlefieldParent != null ? battlefieldParent.gameObject : null);
        AddName("LocalInfoBoxCanvas", localInfoBoxCanvas);
        AddName("BattleCameraCanvas", battleCameraCanvas);
        AddName("TimelineLauncher", timelineLauncher);
        AddName("TimelineManagerCanvas", timelineManagerCanvas);
        AddName("QTEPanel", qtePanel);
        AddName("QTECirclesPanel", qteCirclesPanel);

        if (extraNames != null)
        {
            foreach (NamedBinding binding in extraNames)
                AddName(binding.name, binding.value);
        }

        if (extraTags != null)
        {
            foreach (TaggedBinding binding in extraTags)
                AddTag(binding.tag, binding.value);
        }
    }

    private void AddName(string name, GameObject value)
    {
        if (string.IsNullOrWhiteSpace(name) || value == null)
            return;

        nameLookup[name] = value;
    }

    private void AddTag(string tag, GameObject value)
    {
        if (string.IsNullOrWhiteSpace(tag) || value == null)
            return;

        tagLookup[tag] = value;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            RebuildLookups();
    }

    [ContextMenu("Auto Bind From Scene")]
    private void AutoBindFromScene()
    {
        player = FindByTagSafe("Player");
        worldCamera = FindByTagSafe("WorldCamera");
        battleCamera = FindByTagSafe("BattleCamera");
        versusCamera = FindByTagSafe("VersusCamera");

        worldScene = GameObject.Find("WorldScene");
        worldFadeOverlayPanel = GameObject.Find("WorldFadeOverlayPanel");
        battleCameraCam = GameObject.Find("BattleCamera_Cam");
        battleSceneTransitionCanvas = GameObject.Find("BattleScene_TransitionCanvas");
        enemyPosition01 = GameObject.Find("EnemyPosition_01")?.transform;
        battlefieldParent = GameObject.Find("BattleScene/Battlefields")?.transform;
        localInfoBoxCanvas = GameObject.Find("LocalInfoBoxCanvas");
        battleCameraCanvas = GameObject.Find("BattleCameraCanvas");
        timelineLauncher = GameObject.Find("TimelineLauncher");
        timelineManagerCanvas = GameObject.Find("TimelineManagerCanvas");
        qtePanel = GameObject.Find("QTEPanel");
        qteCirclesPanel = GameObject.Find("QTECirclesPanel");

        RebuildLookups();
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
#endif
}
