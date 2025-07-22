using UnityEngine;
using System.Collections;

[ExecuteAlways]
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
        CacheFragmentsData();
    }

    void OnEnable()
    {
        CacheFragmentsData();
    }

    private void CacheFragmentsData()
    {
        fragments = GetComponentsInChildren<Rigidbody>();
        if (fragments.Length == 0) return;

        originalPositions = new Vector3[fragments.Length];
        originalRotations = new Quaternion[fragments.Length];
        originalScales = new Vector3[fragments.Length];

        for (int i = 0; i < fragments.Length; i++)
        {
            originalPositions[i] = fragments[i].transform.localPosition;
            originalRotations[i] = fragments[i].transform.localRotation;
            originalScales[i] = fragments[i].transform.localScale;
            if (Application.isPlaying)
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
        Vector3 worldExplosionPos = transform.TransformPoint(localExplosionPosition);
        Vector3 worldPushDir = transform.TransformDirection(localPushDirection).normalized;

        foreach (var rb in fragments)
            rb.isKinematic = false;

        // Fissuration
        foreach (var rb in fragments)
        {
            Vector3 dir = (rb.transform.position - worldExplosionPos).normalized;
            Vector3 finalDir = (dir + worldPushDir).normalized;
            rb.AddForce(finalDir * (explosionForce * 0.1f), ForceMode.Impulse);
        }

        yield return new WaitForSeconds(0.2f);

        foreach (var rb in fragments)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        yield return new WaitForSeconds(2f);

        // Explosion finale
        foreach (var rb in fragments)
        {
            Vector3 dir = (rb.transform.position - worldExplosionPos).normalized;
            Vector3 finalDir = (dir + worldPushDir).normalized;
            rb.AddForce(finalDir * explosionForce, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * torqueForce, ForceMode.Impulse);
        }
    }

    private IEnumerator ResetFragmentsSmooth()
    {
        StopAllCoroutines();
        resetting = true;

        Vector3[] startPositions = new Vector3[fragments.Length];
        Quaternion[] startRotations = new Quaternion[fragments.Length];
        Vector3[] startScales = new Vector3[fragments.Length];

        for (int i = 0; i < fragments.Length; i++)
        {
            startPositions[i] = fragments[i].transform.localPosition;
            startRotations[i] = fragments[i].transform.localRotation;
            startScales[i] = fragments[i].transform.localScale;
            fragments[i].linearVelocity = Vector3.zero;
            fragments[i].angularVelocity = Vector3.zero;
        }

        float elapsed = 0f;
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

        // Snap final pour �viter tout d�calage
        for (int i = 0; i < fragments.Length; i++)
        {
            fragments[i].transform.localPosition = originalPositions[i];
            fragments[i].transform.localRotation = originalRotations[i];
            fragments[i].transform.localScale = originalScales[i];
            fragments[i].isKinematic = true;
            fragments[i].linearVelocity = Vector3.zero;
            fragments[i].angularVelocity = Vector3.zero;
        }

        resetting = false;
    }
}
