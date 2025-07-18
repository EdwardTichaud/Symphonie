using UnityEngine;

[ExecuteAlways]
public class ExplodeFragments : MonoBehaviour
{
    public float force = 500f;
    public float radius = 5f;
    public float upwardsModifier = 0.5f;

    public bool triggered = false;

    void Update()
    {
        if(triggered)
        {
            triggered = false;
            Explode();
        }
    }

    public void Explode()
    {
        Rigidbody[] fragments = GetComponentsInChildren<Rigidbody>();
        foreach (var rb in fragments)
        {
            rb.AddExplosionForce(force, transform.position, radius, upwardsModifier, ForceMode.Impulse);
        }
    }
}
