using UnityEngine;
using System.Collections;

public class ExplodeFragments : MonoBehaviour
{
    public float explosionForce = 500f;
    public float explosionRadius = 5f;
    public float torqueForce = 50f;
    public float upModifier = 0.1f;
    public Vector3 explosionPosition;
    public float pushMultiplier = 200f;
    public Vector3 pushDirection = Vector3.up;

    public float resetDuration = 1.5f; // Durée du retour en secondes

    private Rigidbody[] fragments;
    private Vector3[] originalPositions;
    private Quaternion[] originalRotations;
    private bool exploded = false;
    private bool resetting = false;

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
        if (Input.GetKeyDown(KeyCode.J) && !resetting)
        {
            if (!exploded)
                Explode();
            else
                StartCoroutine(ResetFragmentsSmooth());

            exploded = !exploded;
        }
    }

    void Explode()
    {
        Vector3 normalizedPush = pushDirection.normalized;

        foreach (var rb in fragments)
        {
            rb.isKinematic = false;

            Vector3 localOffset = rb.transform.position - explosionPosition;
            float distance = localOffset.magnitude;
            float multiplier = Mathf.Clamp01(1f - (distance / explosionRadius));

            Vector3 appliedForce = normalizedPush * multiplier * pushMultiplier;
            rb.AddExplosionForce(explosionForce, explosionPosition, explosionRadius, upModifier);
            rb.AddForce(appliedForce, ForceMode.Impulse);

            Vector3 randomTorque = new Vector3(
                Random.Range(-torqueForce, torqueForce),
                Random.Range(-torqueForce, torqueForce),
                Random.Range(-torqueForce, torqueForce)
            );

            rb.AddTorque(randomTorque, ForceMode.Impulse);
        }
    }

    IEnumerator ResetFragmentsSmooth()
    {
        resetting = true;

        foreach (var rb in fragments)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        float elapsed = 0f;

        Vector3[] startPositions = new Vector3[fragments.Length];
        Quaternion[] startRotations = new Quaternion[fragments.Length];

        for (int i = 0; i < fragments.Length; i++)
        {
            startPositions[i] = fragments[i].transform.localPosition;
            startRotations[i] = fragments[i].transform.localRotation;
        }

        while (elapsed < resetDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / resetDuration);

            for (int i = 0; i < fragments.Length; i++)
            {
                fragments[i].transform.localPosition = Vector3.Lerp(startPositions[i], originalPositions[i], t);
                fragments[i].transform.localRotation = Quaternion.Slerp(startRotations[i], originalRotations[i], t);
            }

            yield return null;
        }

        resetting = false;
    }
}
