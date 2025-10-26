using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RuntimeFractureExploder
///  - Préfracture à la volée le Mesh de ce GameObject en 'targetPieces' morceaux (groupes de triangles)
///  - Applique une explosion vers le haut + dispersion + spin aléatoire
///  - 100% runtime, sans code éditeur.
/// </summary>
[DisallowMultipleComponent]
public class RuntimeFractureExploder : MonoBehaviour
{
    [Header("Source Mesh")]
    [Tooltip("Si vide, cherche MeshFilter/MeshRenderer sur ce GameObject.")]
    public MeshFilter sourceMeshFilter;
    public MeshRenderer sourceMeshRenderer;

    [Header("Fracture")]
    [Tooltip("Nombre approximatif de morceaux (borné par le nb de triangles).")]
    [Min(1)] public int targetPieces = 200;
    [Tooltip("Mélange l'ordre des triangles pour répartir visuellement les morceaux.")]
    public bool shuffleTriangles = true;
    [Tooltip("Épaisseur factice des morceaux (0 = faces sans volume).")]
    [Min(0)] public float fakeThickness = 0f;
    [Tooltip("Parent des morceaux (par défaut : ce transform).")]
    public Transform piecesParent;

    [Header("Physique des morceaux")]
    [Tooltip("Ajoute un Rigidbody sur chaque morceau.")]
    public bool addRigidbody = true;
    [Min(0.001f)] public float pieceMass = 0.2f;
    [Tooltip("Ajoute un BoxCollider par morceau (léger).")]
    public bool addBoxCollider = true;
    [Tooltip("Ajoute un MeshCollider convex (⚠️ coûteux si beaucoup de morceaux).")]
    public bool addMeshColliderConvex = false;

    [Header("Explosion")]
    [Tooltip("Impulsion verticale de base.")]
    public float upForce = 12f;
    [Tooltip("Dispersion aléatoire (impulsion).")]
    public float randomSpread = 4f;
    [Tooltip("Modificateur vertical d'AddExplosionForce.")]
    public float upwardsModifier = 0.8f;

    [Header("Spin")]
    [Tooltip("Impulsion de couple initiale (coup de clé).")]
    public float spinImpulse = 6f;
    [Tooltip("Vitesse angulaire continue cible (rad/s). 10 ≈ 95 RPM.")]
    public Vector2 spinAngularSpeedRange = new Vector2(10f, 18f);
    [Tooltip("Vitesse angulaire max autorisée par Unity (augmente pour des spins rapides).")]
    public float maxAngularVelocity = 50f;

    [Header("Temporalité")]
    [Tooltip("Délai (s) avant l'explosion après fracture.")]
    public float delayBeforeExplode = 0.03f;
    [Tooltip("Applique une 2e impulsion après la première pour un look plus organique.")]
    public bool secondKick = true;
    [Tooltip("Délai (s) avant la 2e impulsion.")]
    public float secondKickDelay = 0.12f;
    [Tooltip("Coefficient appliqué aux forces du 2e kick (0.3–1).")]
    [Range(0f, 2f)] public float secondKickScale = 0.6f;

    [Header("Cycle de vie")]
    [Tooltip("Détruit chaque morceau après X secondes (<=0 : ne pas détruire).")]
    public float autoDestroyAfter = 6f;

    [Header("Déclenchement")]
    [Tooltip("Coche en Play pour déclencher (one-shot).")]
    public bool triggerNow;

    bool _done;

    // ========================= Unity =========================
    void Reset()
    {
        sourceMeshFilter = GetComponent<MeshFilter>();
        sourceMeshRenderer = GetComponent<MeshRenderer>();
        piecesParent = transform;
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (triggerNow && !_done)
        {
            triggerNow = false;
            Trigger();
        }
    }

    // ========================= API =========================
    /// <summary> Préfracture immédiatement puis déclenche l’explosion selon les réglages. </summary>
    public void Trigger()
    {
        if (_done) _done = false; // autorise relance si tu régénères avant
        if (!ValidateSource()) return;

        var rbs = PreFractureRuntime();

        if (delayBeforeExplode <= 0f) ApplyExplosion(rbs, 1f);
        else StartCoroutine(Co_Explode(rbs));
    }

    // ========================= Routines =========================
    System.Collections.IEnumerator Co_Explode(List<Rigidbody> rbs)
    {
        yield return new WaitForSeconds(delayBeforeExplode);
        ApplyExplosion(rbs, 1f);

        if (secondKick)
        {
            yield return new WaitForSeconds(secondKickDelay);
            ApplyExplosion(rbs, Mathf.Max(0f, secondKickScale));
        }
    }

    // ========================= Implémentation =========================
    bool ValidateSource()
    {
        if (!sourceMeshFilter) sourceMeshFilter = GetComponent<MeshFilter>();
        if (!sourceMeshRenderer) sourceMeshRenderer = GetComponent<MeshRenderer>();

        if (!sourceMeshFilter || !sourceMeshFilter.sharedMesh || !sourceMeshRenderer)
        {
            Debug.LogError("[RuntimeFractureExploder] MeshFilter/MeshRenderer manquant.");
            return false;
        }
        if (!piecesParent) piecesParent = transform;
        return true;
    }

    List<Rigidbody> PreFractureRuntime()
    {
        var mesh = sourceMeshFilter.sharedMesh;
        var tris = mesh.triangles;
        var verts = mesh.vertices;
        var norms = mesh.normals;
        var uvs = mesh.uv;

        // Normal map fallback si absent
        if (norms == null || norms.Length != verts.Length)
        {
            var temp = Instantiate(mesh);
            temp.RecalculateNormals();
            norms = temp.normals;
            Destroy(temp);
        }

        int triCount = tris.Length / 3;
        int pieces = Mathf.Clamp(targetPieces <= 0 ? triCount : targetPieces, 1, triCount);

        // Index des triangles 0..triCount-1
        var triIndices = new List<int>(triCount);
        for (int i = 0; i < triCount; i++) triIndices.Add(i);
        if (shuffleTriangles) FisherYatesShuffle(triIndices);

        int trisPerPiece = Mathf.Max(1, triCount / pieces);
        var chunks = new List<List<int>>(pieces);
        int cursor = 0;
        while (cursor < triCount)
        {
            int take = Mathf.Min(trisPerPiece, triCount - cursor);
            var list = new List<int>(take * 3);
            for (int t = 0; t < take; t++)
            {
                int triId = triIndices[cursor + t];
                int b = triId * 3;
                list.Add(tris[b]);
                list.Add(tris[b + 1]);
                list.Add(tris[b + 2]);
            }
            chunks.Add(list);
            cursor += take;
        }

        // Cache la version intacte, on garde le transform
        sourceMeshRenderer.enabled = false;

        var createdRigidbodies = new List<Rigidbody>(chunks.Count);

        // Génère chaque morceau
        for (int c = 0; c < chunks.Count; c++)
        {
            var triList = chunks[c];

            var localVerts = new List<Vector3>(triList.Count);
            var localNorms = new List<Vector3>(triList.Count);
            var localUVs = new List<Vector2>(triList.Count);
            var localTris = new List<int>(triList.Count);

            var map = new Dictionary<int, int>();
            for (int i = 0; i < triList.Count; i++)
            {
                int src = triList[i];
                if (!map.TryGetValue(src, out int newIdx))
                {
                    newIdx = localVerts.Count;
                    map[src] = newIdx;
                    localVerts.Add(verts[src]);
                    localNorms.Add(norms != null && norms.Length == verts.Length ? norms[src] : Vector3.up);
                    localUVs.Add(uvs != null && uvs.Length == verts.Length ? uvs[src] : Vector2.zero);
                }
                localTris.Add(newIdx);
            }

            // Épaisseur factice (faces arrière + translation)
            if (fakeThickness > 0f)
            {
                int countBefore = localVerts.Count;
                Vector3 avgN = Vector3.zero;
                for (int i = 0; i < countBefore; i++) avgN += localNorms[i];
                avgN = avgN.sqrMagnitude < 1e-6f ? Vector3.up : avgN.normalized;

                for (int i = 0; i < countBefore; i++)
                {
                    localVerts.Add(localVerts[i] + avgN * fakeThickness);
                    localNorms.Add(-localNorms[i]);
                    localUVs.Add(localUVs[i]);
                }
                int trisBefore = localTris.Count;
                for (int i = 0; i < trisBefore; i += 3)
                {
                    int a = localTris[i] + countBefore;
                    int b = localTris[i + 1] + countBefore;
                    int c2 = localTris[i + 2] + countBefore;
                    localTris.Add(c2); localTris.Add(b); localTris.Add(a);
                }
            }

            var pieceMesh = new Mesh
            {
                indexFormat = (localVerts.Count > 65000)
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16
            };
            pieceMesh.SetVertices(localVerts);
            pieceMesh.SetNormals(localNorms);
            pieceMesh.SetUVs(0, localUVs);
            pieceMesh.SetTriangles(localTris, 0);
            pieceMesh.RecalculateBounds();
            if (norms == null || norms.Length != verts.Length) pieceMesh.RecalculateNormals();

            var go = new GameObject($"FractPiece_{c:D4}");
            go.transform.SetParent(piecesParent ? piecesParent : transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = pieceMesh;
            mr.sharedMaterials = sourceMeshRenderer.sharedMaterials;

            if (addBoxCollider) go.AddComponent<BoxCollider>();
            if (addMeshColliderConvex)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = pieceMesh;
                mc.convex = true;
            }

            Rigidbody rb = null;
            if (addRigidbody)
            {
                rb = go.AddComponent<Rigidbody>();
                rb.mass = pieceMass;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.maxAngularVelocity = Mathf.Max(maxAngularVelocity, 50f);
            }

            if (autoDestroyAfter > 0f) Destroy(go, autoDestroyAfter);
            if (rb) createdRigidbodies.Add(rb);
        }

        return createdRigidbodies;
    }

    void ApplyExplosion(List<Rigidbody> rbs, float forceScale)
    {
        if (rbs == null || rbs.Count == 0) return;

        var worldCenter = GetWorldBoundsCenter();
        float radius = Mathf.Max(0.5f, GetApproxRadius());

        foreach (var rb in rbs)
        {
            if (!rb) continue;

            // Qualité physique
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.maxAngularVelocity = Mathf.Max(rb.maxAngularVelocity, maxAngularVelocity);

            // Reset pour un burst net
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // 1) Force vers le haut + dispersion
            Vector3 random = Random.insideUnitSphere * randomSpread * forceScale;
            Vector3 upward = Vector3.up * upForce * forceScale;
            rb.AddForce(upward + random, ForceMode.Impulse);

            // 2) Souffle radial doux depuis le centre du mesh
            float extra = (upForce * 0.6f + random.magnitude) * forceScale;
            rb.AddExplosionForce(extra, worldCenter, radius, upwardsModifier, ForceMode.Impulse);

            // 3) Spin aléatoire fort (impulsion + vitesse continue)
            Vector3 axis = Random.onUnitSphere.normalized;
            rb.AddTorque(axis * (spinImpulse * forceScale), ForceMode.Impulse);

            float w = Random.Range(spinAngularSpeedRange.x, spinAngularSpeedRange.y) * forceScale;
            rb.angularVelocity = axis * w;
        }
    }

    Vector3 GetWorldBoundsCenter()
    {
        var mf = sourceMeshFilter;
        var mesh = mf.sharedMesh;
        return mf.transform.TransformPoint(mesh.bounds.center);
    }

    float GetApproxRadius()
    {
        var s = sourceMeshFilter.sharedMesh.bounds.size;
        float rLocal = Mathf.Max(s.x, Mathf.Max(s.y, s.z)) * 0.6f;
        float scale = Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
        return rLocal * Mathf.Max(0.01f, scale);
    }

    static void FisherYatesShuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
