using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Centralise l'initialisation de la scene et l'enregistrement des services.
/// </summary>
[DefaultExecutionOrder(-10000)]
public sealed class GameRoot : MonoBehaviour
{
    public static GameRoot Instance { get; private set; }
    public static bool KeepManagersSceneBound => Instance != null && Instance.keepManagersSceneBound;

    [Header("Mode")]
    [SerializeField] private bool keepManagersSceneBound = true;
    [SerializeField] private bool persistRootAcrossScenes = false;

    [Header("Bindings")]
    [SerializeField] private SceneBindings sceneBindings;
    [SerializeField] private BattleSceneBindings battleSceneBindings;

    [Header("Services")]
    [SerializeField] private List<UnityEngine.Object> services = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (persistRootAcrossScenes)
            DontDestroyOnLoad(gameObject);

        CacheBindingsIfMissing();
        RegisterServices();
    }

    private void OnDestroy()
    {
        UnregisterServices();
        if (Instance == this)
            Instance = null;
    }

    private void CacheBindingsIfMissing()
    {
        if (sceneBindings == null)
            sceneBindings = UnityEngine.Object.FindFirstObjectByType<SceneBindings>(FindObjectsInactive.Include);
        if (battleSceneBindings == null)
            battleSceneBindings = UnityEngine.Object.FindFirstObjectByType<BattleSceneBindings>(FindObjectsInactive.Include);
    }

    private void RegisterServices()
    {
        if (sceneBindings != null)
            ServiceRegistry.Register(sceneBindings);
        if (battleSceneBindings != null)
            ServiceRegistry.Register(battleSceneBindings);

        if (services == null)
            return;

        foreach (UnityEngine.Object service in services)
            ServiceRegistry.Register(service);
    }

    private void UnregisterServices()
    {
        if (sceneBindings != null)
            ServiceRegistry.Unregister(sceneBindings);
        if (battleSceneBindings != null)
            ServiceRegistry.Unregister(battleSceneBindings);

        if (services == null)
            return;

        foreach (UnityEngine.Object service in services)
            ServiceRegistry.Unregister(service);
    }

#if UNITY_EDITOR
    [ContextMenu("Auto Bind From Scene")]
    private void AutoBindFromScene()
    {
        CacheBindingsIfMissing();
        EditorUtility.SetDirty(this);
    }

    [ContextMenu("Auto Bind Core Services")]
    private void AutoBindCoreServices()
    {
        services ??= new List<UnityEngine.Object>();
        services.Clear();

        AddServiceIfFound<GameManager>();
        AddServiceIfFound<InputsManager>();
        AddServiceIfFound<AudioManager>();
        AddServiceIfFound<TimelineManager>();
        AddServiceIfFound<BattleTransitionManager>();
        AddServiceIfFound<NewBattleManager>();
        AddServiceIfFound<CameraController>();
        AddServiceIfFound<ZoneManager>();
        AddServiceIfFound<SquadManager>();
        AddServiceIfFound<InventoryManager>();
        AddServiceIfFound<EventsManager>();
        AddServiceIfFound<EffectManager>();

        EditorUtility.SetDirty(this);
    }

    private void AddServiceIfFound<T>() where T : UnityEngine.Object
    {
        T found = UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        if (found != null)
            services.Add(found);
    }
#endif
}
