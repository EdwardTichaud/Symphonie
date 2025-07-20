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
    private bool exploded = false;
    private bool resetting = false;

    private Vector3[] originalScales;

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
            Explode();
        else
            StartCoroutine(ResetFragmentsSmooth());

        exploded = !exploded;
    }

    void Explode()
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

        Vector3 worldExplosionPos = transform.TransformPoint(localExplosionPosition);
        Vector3 worldPushDir = transform.TransformDirection(localPushDirection).normalized;

        foreach (var rb in fragments)
        {
            rb.isKinematic = false;

            Vector3 localOffset = rb.transform.position - worldExplosionPos;
            float distance = localOffset.magnitude;
            float multiplier = Mathf.Clamp01(1f - (distance / explosionRadius));

            Vector3 appliedForce = worldPushDir * multiplier * pushMultiplier;

            rb.AddExplosionForce(explosionForce, worldExplosionPos, explosionRadius, upModifier);
            rb.AddForce(appliedForce, ForceMode.Impulse);

            Vector3 randomTorque = new Vector3(
                Random.Range(-torqueForce, torqueForce),
                Random.Range(-torqueForce, torqueForce),
                Random.Range(-torqueForce, torqueForce)
            );

            rb.AddTorque(randomTorque, ForceMode.Impulse);
        }

        StartCoroutine(ExplosionTimeScaleSequence());
    }

    IEnumerator ExplosionTimeScaleSequence()
    {
        yield return ApplyFragmentSlowdown(1f, 0.01f);
        yield return ApplyFragmentSlowdown(0f, 0.5f);
        yield return ApplyFragmentSlowdown(1f, 0.5f);
        yield return ApplyFragmentSlowdown(0.2f, 1f);
        yield return ApplyFragmentSlowdown(1f, 2f);

        StartCoroutine(ShrinkFragments(1f));
    }

    IEnumerator ShrinkFragments(float shrinkDuration)
    {
        float elapsed = 0f;
        Vector3[] initialScales = new Vector3[fragments.Length];

        for (int i = 0; i < fragments.Length; i++)
        {
            initialScales[i] = fragments[i].transform.localScale;
        }

        while (elapsed < shrinkDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / shrinkDuration);

            for (int i = 0; i < fragments.Length; i++)
            {
                fragments[i].transform.localScale = Vector3.Lerp(initialScales[i], Vector3.zero, t);
            }

            yield return null;
        }
    }

    IEnumerator ApplyFragmentSlowdown(float slowFactor, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            foreach (var rb in fragments)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity *= slowFactor;
                    rb.angularVelocity *= slowFactor;
                }
            }

            yield return null;
        }
    }

    IEnumerator ResetFragmentsSmooth()
    {
        // Arrêter toutes les coroutines actives sur ce script
        StopAllCoroutines();

        // Remettre Time.timeScale à 1 au cas où une ApplyTimeScale était active
        Time.timeScale = 1f;

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
