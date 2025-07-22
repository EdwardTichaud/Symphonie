using UnityEngine;
using System.Collections;

public class ExplodeFragments : MonoBehaviour
{
    public float explosionForce = 500f;
    public float explosionRadius = 5f;
    public float torqueForce = 10f;
    public float upModifier = 0.1f;
    public Vector3 localExplosionPosition = Vector3.zero;
    public float pushMultiplier = 20f;
    public Vector3 localPushDirection = Vector3.up;
    public float resetDuration = 0.3f;

    private Rigidbody[] fragments;
    private Vector3[] originalPositions;
    private Quaternion[] originalRotations;
    private Vector3[] originalScales;

    private bool exploded = false;
    private bool resetting = false;

    void Start()
    {
        fragments = GetComponentsInChildren<Rigidbody>();
        originalPositions = new Vector3[fragments.Length];
        originalRotations = new Quaternion[fragments.Length];
        originalScales = new Vector3[fragments.Length];

        for (int i = 0; i < fragments.Length; i++)
        {
            originalPositions[i] = fragments[i].transform.localPosition;
            originalRotations[i] = fragments[i].transform.localRotation;
            originalScales[i] = fragments[i].transform.localScale;
            fragments[i].isKinematic = true;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.J) && !resetting)
        {
            ExplodeFragmentsMethod();
        }
    }

    public void ExplodeFragmentsMethod()
    {
        if (!exploded)
            StartCoroutine(FissureAndExplode());
        else
            StartCoroutine(ResetFragmentsSmooth());

        exploded = !exploded;
    }

    public IEnumerator FissureAndExplode()
    {
        if (!exploded)
        {
            Vector3 worldExplosionPos = transform.TransformPoint(localExplosionPosition);
            Vector3 worldPushDir = transform.TransformDirection(localPushDirection).normalized;

            // Libérer les fragments
            foreach (var rb in fragments)
            {
                rb.isKinematic = false;
            }

            // 1. Fissuration : petit burst
            foreach (var rb in fragments)
            {
                rb.AddExplosionForce(explosionForce * 0.05f, worldExplosionPos, explosionRadius, upModifier);
                rb.AddForce(worldPushDir * pushMultiplier * 0.05f, ForceMode.Impulse);
            }
            yield return new WaitForSeconds(2f);

            // 2. Stop mouvement
            foreach (var rb in fragments)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            yield return new WaitForSeconds(2f);

            // 3. Explosion finale
            foreach (var rb in fragments)
            {
                rb.AddExplosionForce(explosionForce, worldExplosionPos, explosionRadius, upModifier);
                rb.AddForce(worldPushDir * pushMultiplier, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * torqueForce, ForceMode.Impulse);
            }
        }

        // Lancer la routine de shrink si besoin ici...
    }

    IEnumerator ResetFragmentsSmooth()
    {
        StopAllCoroutines();
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
        Vector3[] startScales = new Vector3[fragments.Length];

        for (int i = 0; i < fragments.Length; i++)
        {
            startPositions[i] = fragments[i].transform.localPosition;
            startRotations[i] = fragments[i].transform.localRotation;
            startScales[i] = fragments[i].transform.localScale;
        }

        while (elapsed < resetDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / resetDuration);

            for (int i = 0; i < fragments.Length; i++)
            {
                fragments[i].transform.localPosition = Vector3.Lerp(startPositions[i], originalPositions[i], t);
                fragments[i].transform.localRotation = Quaternion.Slerp(startRotations[i], originalRotations[i], t);
                fragments[i].transform.localScale = Vector3.Lerp(startScales[i], originalScales[i], t);
            }

            yield return null;
        }

        resetting = false;
    }
}
