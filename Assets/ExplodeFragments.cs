using UnityEngine;

public class FragmentExplosionToggle : MonoBehaviour
{
    public float explosionForce = 500f;
    public float explosionRadius = 5f;
    public float torqueForce = 50f;
    public float upModifier = 0.1f;
    public Vector3 explosionPosition;

    private Rigidbody[] fragments;
    private Vector3[] originalPositions;
    private Quaternion[] originalRotations;
    private bool exploded = false;

    void Start()
    {
        fragments = GetComponentsInChildren<Rigidbody>();
        originalPositions = new Vector3[fragments.Length];
        originalRotations = new Quaternion[fragments.Length];

        for (int i = 0; i < fragments.Length; i++)
        {
            originalPositions[i] = fragments[i].transform.localPosition;
            originalRotations[i] = fragments[i].transform.localRotation;
            fragments[i].isKinematic = true;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            if (!exploded)
            {
                Explode();
            }
            else
            {
                ResetFragments();
            }

            exploded = !exploded;
        }
    }

    void Explode()
    {
        foreach (var rb in fragments)
        {
            rb.isKinematic = false;
            rb.AddExplosionForce(explosionForce, explosionPosition, explosionRadius, upModifier);

            Vector3 randomTorque = new Vector3(
                Random.Range(-torqueForce, torqueForce),
                Random.Range(-torqueForce, torqueForce),
                Random.Range(-torqueForce, torqueForce)
            );

            rb.AddTorque(randomTorque, ForceMode.Impulse);
        }
    }

    void ResetFragments()
    {
        for (int i = 0; i < fragments.Length; i++)
        {
            fragments[i].isKinematic = true;
            fragments[i].transform.localPosition = originalPositions[i];
            fragments[i].transform.localRotation = originalRotations[i];
            fragments[i].linearVelocity = Vector3.zero;
            fragments[i].angularVelocity = Vector3.zero;
        }
    }
}
