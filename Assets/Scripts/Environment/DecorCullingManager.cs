using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

public class DecorCullingManager : MonoBehaviour
{
    public static DecorCullingManager Instance { get; private set; }
    public static bool HasInstance => Instance != null;

    [Tooltip("Marge de sécurité autour des décors (en mètres).")]
    public float margin = 5f;

    private readonly List<DecorCullingObject> objects = new();
    private CullingGroup cullingGroup;
    private BoundingSphere[] spheres = new BoundingSphere[0];
    private Camera targetCamera;

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
    }

    private void LateUpdate()
    {
        if (Camera.main != targetCamera)
        {
            targetCamera = Camera.main;
            cullingGroup.targetCamera = targetCamera;
        }
    }

    public void RegisterObject(DecorCullingObject obj)
    {
        if (!objects.Contains(obj))
        {
            objects.Add(obj);
            UpdateSpheres();
        }
    }

    public void UnregisterObject(DecorCullingObject obj)
    {
        int index = objects.IndexOf(obj);
        if (index >= 0)
        {
            objects.RemoveAt(index);
            UpdateSpheres();
        }
    }

    private void UpdateSpheres()
    {
        spheres = new BoundingSphere[objects.Count];
        for (int i = 0; i < objects.Count; i++)
        {
            spheres[i] = objects[i].GetSphere(margin);
        }
        cullingGroup.SetBoundingSpheres(spheres);
        cullingGroup.SetBoundingSphereCount(objects.Count);
    }

    private void OnStateChanged(CullingGroupEvent evt)
    {
        bool visible = evt.isVisible;
        objects[evt.index].SetVisible(visible);
    }

    private void OnDestroy()
    {
        cullingGroup.Dispose();
    }
}
