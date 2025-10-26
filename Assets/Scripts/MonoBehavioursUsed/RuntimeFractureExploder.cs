using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Préfracture à la volée le Mesh de ce GameObject en "targetPieces" morceaux (groupes de triangles),
/// puis applique une explosion (forces + torque). 100% runtime (Play Mode).
/// </summary>
[DisallowMultipleComponent]
public class RuntimeFractureExploder : MonoBehaviour
{
    [Header("Source Mesh")]
    [Tooltip("Si vide, cherche MeshFilter/MeshRenderer sur ce GameObject.")]
    public MeshFilter sourceMeshFilter;
    public MeshRenderer sourceMeshRenderer;

    [Header("Fracture")]
    [Tooltip("Nombre approximatif de morceaux. Sera borné au nombre de triangles du mesh.")]
    [Min(1)] public int targetPieces = 200;
    [Tooltip("Ajoute une petite épaisseur artificielle (extrude le morceau le long de sa normale moyenne).")]
    [Min(0)] public float fakeThickness = 0f;
    [Tooltip("Mélanger les triangles avant partition (meilleure répartition visuelle).")]
    public bool shuffleTriangles = true;
    [Tooltip("Parent des morceaux (par défaut: ce GameObject).")]
    public Transform piecesParent;

    [Header("Physique & Explosion")]
    public bool addRigidbody = true;
    public float pieceMass = 0.2f;
    public bool addBoxCollider = true;       // léger & suffisant la plupart du temps
    public bool addMeshColliderConvex = false; // ⚠️ coûteux si beaucoup de pièces
    [Tooltip("Détruit les morceaux après X sec (<=0 : garde).")]
    public float autoDestroyAfter = 6f;

    [Space]
    [Tooltip("Délai avant explosion après la fracture.")]
    public float delayBeforeExplode = 0.05f;
    [Tooltip("Impulsion verticale de base.")]
    public float upForce = 8f;
    [Tooltip("Dispersion aléatoire.")]
    public float randomSpread = 3f;
    [Tooltip("Couple aléatoire appliqué.")]
    public float randomTorque = 4f;
    [Tooltip("Modificateur vertical d'AddExplosionForce.")]
    public float upwardsModifier = 0.7f;

    [Header("Contrôle")]
    [Tooltip("Coche en Play pour déclencher (one-shot).")]
    public bool triggerNow;

    bool _done;

    void Reset()
    {
        sourceMeshFilter = GetComponent<MeshFilter>();
        sourceMeshRenderer = GetComponent<MeshRenderer>();
        piecesParent = transform;
    }

    void Update()
    {
        if (Application.isPlaying && triggerNow && !_done)
        {
            triggerNow = false;
            Trigger();
        }
    }

    /// <summary> Déclenche préfracturation puis explosion. </summary>
    public void Trigger()
    {
        if (_done) return;
        if (!ValidateSource()) return;

        // 1) Fracture → renvoie la liste des rbs créés (peut être vide si addRigidbody=false)
        var rbs = PreFractureRuntime();

        // 2) Explosion après un léger délai
        if (delayBeforeExplode <= 0f) ApplyExplosion(rbs);
        else StartCoroutine(ExplodeNextFrame(rbs, delayBeforeExplode));

        _done = true;
    }

    System.Collections.IEnumerator ExplodeNextFrame(List<Rigidbody> rbs, float delay)
    {
        yield return new WaitForSeconds(delay);
        ApplyExplosion(rbs);
    }

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

        if (norms == null || norms.Length != verts.Length)
        {
            // crée une copie pour recalculer (évite d'écraser le mesh partagé)
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

        // Partition en "pieces" groupes d'environ triCount/pieces triangles
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

        // Désactive l'objet intact (juste le renderer ; on garde le transform)
        sourceMeshRenderer.enabled = false;

        var createdRigidbodies = new List<Rigidbody>(chunks.Count);

        // Génération de chaque morceau
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

            // Épaisseur artificielle (optionnelle)
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
                // faces arrières (winding inversé)
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
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
            if (autoDestroyAfter > 0f) Destroy(go, autoDestroyAfter);
            if (rb) createdRigidbodies.Add(rb);
        }

        return createdRigidbodies;
    }

    void ApplyExplosion(List<Rigidbody> rbs)
    {
        // Point d’explosion: centre des bounds du mesh source en monde
        var worldCenter = GetWorldBoundsCenter();

        // Applique forces
        if (rbs != null && rbs.Count > 0)
        {
            float radius = Mathf.Max(0.5f, GetApproxRadius());
            foreach (var rb in rbs)
            {
                if (!rb) continue;
                Vector3 random = Random.insideUnitSphere * randomSpread;
                Vector3 up = Vector3.up * upForce;
                rb.AddForce(up + random, ForceMode.Impulse);
                rb.AddExplosionForce(upForce * 0.6f + random.magnitude, worldCenter, radius, upwardsModifier, ForceMode.Impulse);
                rb.AddTorque(Random.onUnitSphere * randomTorque, ForceMode.Impulse);
            }
        }
    }

    Vector3 GetWorldBoundsCenter()
    {
        var mf = sourceMeshFilter;
        var mesh = mf.sharedMesh;
        var b = mesh.bounds; // en local
        return mf.transform.TransformPoint(b.center);
    }

    float GetApproxRadius()
    {
        var b = sourceMeshFilter.sharedMesh.bounds.size;
        float r = Mathf.Max(b.x, Mathf.Max(b.y, b.z)) * 0.6f * Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
        return r;
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
