using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

public class DecorCullingManager : MonoBehaviour
{
    public static DecorCullingManager Instance { get; private set; }
    public static bool HasInstance => Instance != null;

    [Tooltip("Marge de sécurité autour des décors (en mètres).")]
    public float margin = 5f;

    [Tooltip("Nom du layer utilisé pour le culling.")]
    public string layerName = "World_Culling";

    private class TrackedObject
    {
        public GameObject gameObject;
        public Renderer[] renderers;
        public Collider[] colliders;
        public BoundingSphere baseSphere;
    }

    private readonly List<TrackedObject> objects = new();
    private CullingGroup cullingGroup;
    private BoundingSphere[] spheres = new BoundingSphere[0];
    private Camera targetCamera;
    private int layerMask;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        targetCamera = Camera.main;
        cullingGroup = new CullingGroup();
        cullingGroup.onStateChanged += OnStateChanged;
        cullingGroup.targetCamera = targetCamera;

        // Obtenir le LayerMask depuis le nom
        layerMask = LayerMask.NameToLayer(layerName);
        if (layerMask < 0)
        {
            Debug.LogError($"Layer \"{layerName}\" n'existe pas. Ajoute-le dans les settings Unity.");
            enabled = false;
            return;
        }

        RegisterAllLayeredObjects();
        UpdateSpheres();
    }

    private void LateUpdate()
    {
        if (Camera.main != targetCamera)
        {
            targetCamera = Camera.main;
            cullingGroup.targetCamera = targetCamera;
        }
    }

    private void RegisterAllLayeredObjects()
    {
        GameObject[] allObjects = FindObjectsOfType<GameObject>(true);
        foreach (GameObject obj in allObjects)
        {
            if (obj.layer == layerMask)
                RegisterObject(obj);
        }
    }

    private void RegisterObject(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);

        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers)
            bounds.Encapsulate(r.bounds);

        var tracked = new TrackedObject
        {
            gameObject = obj,
            renderers = renderers,
            colliders = colliders,
            baseSphere = new BoundingSphere(bounds.center, bounds.extents.magnitude)
        };

        objects.Add(tracked);
    }

    private void UpdateSpheres()
    {
        spheres = new BoundingSphere[objects.Count];
        for (int i = 0; i < objects.Count; i++)
        {
            BoundingSphere sphere = objects[i].baseSphere;
            spheres[i] = new BoundingSphere(sphere.position, sphere.radius + margin);
        }

        cullingGroup.SetBoundingSpheres(spheres);
        cullingGroup.SetBoundingSphereCount(spheres.Length);
    }

    private void OnStateChanged(CullingGroupEvent evt)
    {
        bool visible = evt.isVisible;
        TrackedObject obj = objects[evt.index];

        foreach (var r in obj.renderers)
            r.enabled = visible;

        foreach (var c in obj.colliders)
            c.enabled = visible;
    }

    private void OnDestroy()
    {
        cullingGroup.Dispose();
    }
}
