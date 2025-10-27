using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Anime une propriété shader (par défaut "_DissolveStrength") sur tous les Renderers
/// dans un rayon donné, du niveau courant jusqu'au target, en duration secondes.
/// Appeler TriggerDissolve() pour lancer l'effet.
/// </summary>
[DisallowMultipleComponent]
public class AreaDissolveTrigger : MonoBehaviour
{
    [Header("Zone")]
    [Tooltip("Centre de la recherche. Laisse vide pour utiliser ce transform.")]
    public Transform centerOverride;
    [Tooltip("Rayon de recherche en mètres.")]
    public float radius = 200f;
    [Tooltip("Filtre des layers (Only Renderers sur ces layers).")]
    public LayerMask layerMask = ~0;
    [Tooltip("Inclure les Renderers inactifs dans la hiérarchie.")]
    public bool includeInactive = false;

    [Header("Animation")]
    [Tooltip("Nom de la propriété float dans le shader.")]
    public string propertyName = "_DissolveStrength";
    [Tooltip("Valeur cible (1 = dissoudre complètement).")]
    [Range(0f, 1f)] public float targetValue = 1f;
    [Tooltip("Durée de l'animation (secondes).")]
    public float duration = 2f;
    [Tooltip("Courbe d'interpolation (x = temps normalisé, y = poids).")]
    public AnimationCurve easing = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Options")]
    [Tooltip("Limiter le nombre maximum de renderers traités (0 = illimité).")]
    public int maxRenderers = 0;
    [Tooltip("Ne modifier que les matériaux qui possèdent la propriété.")]
    public bool onlyIfHasProperty = true;

    // --- internes ---
    int _propID;
    Coroutine _running;

    // Appelle ceci pour lancer le dissolve
    public void TriggerDissolve()
    {
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(Co_Dissolve(targetValue, duration));
    }

    // Optionnel : retour à 0
    public void TriggerUndissolve(float backDuration = 2f)
    {
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(Co_Dissolve(0f, backDuration));
    }

    IEnumerator Co_Dissolve(float toValue, float d)
    {
        _propID = Shader.PropertyToID(propertyName);

        // 1) Récupère les Renderers dans le rayon
        var list = CollectRenderersInRadius();

        // 2) Snapshot des valeurs de départ
        var entries = new List<Entry>(list.Count);
        foreach (var r in list)
        {
            if (!r) continue;

            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0) continue;

            // Vérifie le layer
            if (((1 << r.gameObject.layer) & layerMask.value) == 0) continue;

            // Si demandé, skip si aucun mat n'a la propriété
            if (onlyIfHasProperty)
            {
                bool anyHas = false;
                foreach (var m in mats) { if (m && m.HasProperty(_propID)) { anyHas = true; break; } }
                if (!anyHas) continue;
            }

            // On lit une valeur de départ "raisonnable"
            float start = 0f;
            bool foundStart = false;
            foreach (var m in mats)
            {
                if (m && m.HasProperty(_propID))
                {
                    start = m.GetFloat(_propID);
                    foundStart = true;
                    break;
                }
            }
            if (!foundStart) start = 0f;

            entries.Add(new Entry { renderer = r, start = start });
            if (maxRenderers > 0 && entries.Count >= maxRenderers) break;
        }

        // 3) Tween
        float t = 0f;
        // MatPropertyBlock réutilisable
        var mpb = new MaterialPropertyBlock();

        while (t < d)
        {
            float k = d > 0f ? t / d : 1f;
            float w = Mathf.Clamp01(easing.Evaluate(k));
            float v = Mathf.LerpUnclamped(0f, 1f, w); // on interpole 0..1 puis on remappe par start->target

            foreach (var e in entries)
            {
                if (!e.renderer) continue;

                // valeur courante entre start et toValue
                float cur = Mathf.Lerp(e.start, toValue, v);

                // Applique via MPB sur CHAQUE sous-mat (important pour SRP)
                int subCount = e.renderer.sharedMaterials?.Length ?? 1;
                for (int i = 0; i < subCount; i++)
                {
                    e.renderer.GetPropertyBlock(mpb, i);
                    mpb.SetFloat(_propID, cur);
                    e.renderer.SetPropertyBlock(mpb, i);
                }
            }

            t += Time.deltaTime;
            yield return null;
        }

        // 4) Dernière passe à la valeur cible
        foreach (var e in entries)
        {
            if (!e.renderer) continue;
            int subCount = e.renderer.sharedMaterials?.Length ?? 1;
            for (int i = 0; i < subCount; i++)
            {
                e.renderer.GetPropertyBlock(mpb, i);
                mpb.SetFloat(_propID, toValue);
                e.renderer.SetPropertyBlock(mpb, i);
            }
        }

        _running = null;
    }

    struct Entry
    {
        public Renderer renderer;
        public float start;
    }

    List<Renderer> CollectRenderersInRadius()
    {
        var list = new List<Renderer>(256);
        Vector3 center = centerOverride ? centerOverride.position : transform.position;
        float r2 = radius * radius;

        // On parcourt tous les renderers (inclure inactifs si demandé)
        var renderers = includeInactive
            ? Resources.FindObjectsOfTypeAll<Renderer>()   // inclut assets editor ; filtre plus bas
            : FindObjectsOfType<Renderer>(false);

        foreach (var r in renderers)
        {
            if (r == null) continue;

            // Filtre éditeur pour Resources.FindObjectsOfTypeAll
            if (includeInactive)
            {
                // ignorer ceux pas dans la scène
                if (r.gameObject.scene.IsValid() == false) continue;
            }

            // Distance au centre via bounds (plus juste)
            var b = r.bounds;
            float dist2 = (b.ClosestPoint(center) - center).sqrMagnitude;
            if (dist2 <= r2)
            {
                list.Add(r);
            }
        }
        return list;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.25f);
        Vector3 center = centerOverride ? centerOverride.position : transform.position;
        Gizmos.DrawSphere(center, 0.25f);
        Gizmos.DrawWireSphere(center, radius);
    }
}
