using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Renderer))]
public class DecorCullingObject : MonoBehaviour
{
    private Renderer[] renderers;
    private Collider[] colliders;
    private BoundingSphere baseSphere;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);

        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);
            baseSphere = new BoundingSphere(bounds.center, bounds.extents.magnitude);
        }
        else
        {
            baseSphere = new BoundingSphere(transform.position, 1f);
        }
    }

    private void OnEnable()
    {
        if (DecorCullingManager.HasInstance)
            DecorCullingManager.Instance.RegisterObject(this);
    }

    private void OnDisable()
    {
        if (DecorCullingManager.HasInstance)
            DecorCullingManager.Instance.UnregisterObject(this);
    }

    public BoundingSphere GetSphere(float margin)
    {
        return new BoundingSphere(baseSphere.position, baseSphere.radius + margin);
    }

    public void SetVisible(bool visible)
    {
        foreach (var r in renderers)
            r.enabled = visible;
        foreach (var c in colliders)
            c.enabled = visible;
    }
}
