using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ExplodeChildren : MonoBehaviour
{
    [Header("Explosion settings")]
    [Tooltip("Force verticale appliquée (impulsion).")]
    public float upForce = 6f;
    [Tooltip("Dispersion aléatoire (impulsion)")]
    public float randomSpread = 2f;
    [Tooltip("Torque aléatoire appliqué pour rotation.")]
    public float randomTorque = 2f;
    [Tooltip("Constrainte d'augmentation (si >0, simule poussée vers le haut).")]
    public float upwardsModifier = 0.5f;

    [Header("Rigidbody / Collider")]
    [Tooltip("Ajouter un Rigidbody si l'enfant n'en a pas")]
    public bool addRigidbodyIfMissing = true;
    [Tooltip("Ajouter un Collider (Box) si l'enfant n'en a pas")]
    public bool addBoxColliderIfMissing = true;
    [Tooltip("Masse des Rigidbodies ajoutés")]
    public float rbMass = 1f;

    [Header("Lifecycle")]
    [Tooltip("Détruire les morceaux après X secondes (<=0 = ne pas détruire)")]
    public float destroyAfter = 5f;

    [Header("Trigger")]
    [Tooltip("Si true dans l'inspector, déclenche l'explosion (one-shot).")]
    public bool explodeNow = false;

    bool hasExploded = false;

    void Update()
    {
        // Optionnel : déclenche depuis l'inspector (une seule fois)
        if (explodeNow && !hasExploded)
        {
            Explode();
            hasExploded = true;
            explodeNow = false;
        }
    }

    /// <summary>
    /// Appelle l'explosion depuis du code.
    /// </summary>
    public void Explode()
    {
        StartCoroutine(DoExplode());
    }

    IEnumerator DoExplode()
    {
        // Récupère snapshot des enfants (évite problème si on modifie la hiérarchie)
        List<Transform> children = new List<Transform>();
        foreach (Transform child in transform)
            children.Add(child);

        // Si pas d'enfant -> rien
        if (children.Count == 0) yield break;

        foreach (Transform child in children)
        {
            if (child == null) continue;

            // Désolidariser du parent
            child.SetParent(null, true);

            // Trouver ou ajouter Rigidbody
            Rigidbody rb = child.GetComponent<Rigidbody>();
            if (rb == null && addRigidbodyIfMissing)
            {
                rb = child.gameObject.AddComponent<Rigidbody>();
                rb.mass = rbMass;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }

            // Ajouter collider simple si nécessaire (sinon l'objet pourrait passer à travers)
            Collider col = child.GetComponent<Collider>();
            if (col == null && addBoxColliderIfMissing)
            {
                // On ajoute un BoxCollider approximatif basé sur le Renderer bounds si présent
                Renderer rend = child.GetComponentInChildren<Renderer>();
                BoxCollider bc = child.gameObject.AddComponent<BoxCollider>();
                if (rend != null)
                {
                    bc.center = child.InverseTransformPoint(rend.bounds.center);
                    bc.size = child.InverseTransformVector(rend.bounds.size);
                }
            }

            // Appliquer forces si on a un Rigidbody
            if (rb != null)
            {
                // Force verticale + dispersion aléatoire
                Vector3 random = Random.insideUnitSphere * randomSpread;
                Vector3 force = (Vector3.up * upForce) + random;

                // Impulsion
                rb.AddForce(force, ForceMode.Impulse);

                // Ajout d'un petit AddExplosionForce pour accentuer l'effet (optionnel)
                // On place l'origine un peu sous l'objet pour favoriser la poussée vers le haut
                Vector3 explosionPos = child.position - Vector3.up * 0.5f;
                float explosionForce = upForce * 0.5f + random.magnitude;
                float explosionRadius = 2f;
                rb.AddExplosionForce(explosionForce, explosionPos, explosionRadius, upwardsModifier, ForceMode.Impulse);

                // Couple aléatoire
                rb.AddTorque(Random.onUnitSphere * randomTorque, ForceMode.Impulse);
            }
            else
            {
                // Fallback si pas de physics : on anime la position via coroutine pour déplacement vers le haut
                StartCoroutine(LiftTransformTemporarily(child));
            }

            // Optionnel : destruction programmée
            if (destroyAfter > 0f)
            {
                Destroy(child.gameObject, destroyAfter);
            }
        }

        yield return null;
    }

    // Fallback si pas de rigidbody (simple translation + rotation sur le transform)
    IEnumerator LiftTransformTemporarily(Transform t)
    {
        float duration = Mathf.Max(1f, destroyAfter > 0 ? Mathf.Min(destroyAfter, 3f) : 2f);
        Vector3 start = t.position;
        Vector3 end = start + Vector3.up * (upForce * 0.6f) + Random.insideUnitSphere * (randomSpread * 0.5f);
        Quaternion startRot = t.rotation;
        Quaternion endRot = startRot * Quaternion.Euler(Random.onUnitSphere * (randomTorque * 10f));

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float k = elapsed / duration;
            // ease out
            t.position = Vector3.Lerp(start, end, 1f - Mathf.Pow(1f - k, 2f));
            t.rotation = Quaternion.Slerp(startRot, endRot, k);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // Pour tester depuis le menu contextuel
    [ContextMenu("Explode Now")]
    private void ContextExplode()
    {
        Explode();
    }
}
