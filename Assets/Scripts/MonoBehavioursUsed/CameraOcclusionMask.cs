using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor; // <- il manquait le point-virgule ici
#endif

/// <summary>
/// Cône d'occlusion (apex = caméra, base = cible) pour sélectionner UN renderer :
/// celui qui coupe le rayon central et est le plus proche de la cible parmi
/// les objets qui intersectent le cône.
/// Le centre du masque = projection écran de la CIBLE (+ offsets).
/// Le masque peut être :
/// - une forme TEXTURÉE fournie via Image/RawImage (alpha), ou
/// - une ellipse/cercle (fallback si pas de texture).
/// </summary>
[DisallowMultipleComponent]
public class CameraOcclusionMask_ConeLookAtTarget_UIAligned : MonoBehaviour
{
    // --- CIBLE & CAM ---
    [Header("Cible & Caméra")]
    [SerializeField] private Transform target;
    [SerializeField] private Camera maskCamera;

    // --- CÔNE ---
    [Header("Cône (caméra → cible)")]
    [Tooltip("Rayon du cône au niveau de la cible (unités monde).")]
    [SerializeField, Min(0f)] private float coneBaseRadius = 5f;
    [Tooltip("Nombre d'échantillons (sphères) pour approximer le cône.")]
    [SerializeField, Range(3, 24)] private int coneSamples = 10;
    [Tooltip("Layers considérés comme obstacles visuels.")]
    [SerializeField] private LayerMask obstacleLayers = ~0;

    // --- RAYON CENTRAL ---
    [Header("Raycast (axe central)")]
    [Tooltip("Autoriser le hit des faces arrière (utile pour planes/quads).")]
    [SerializeField] private bool hitBackfaces = true;
    [Tooltip("Petit offset depuis la caméra pour éviter de démarrer 'dans' une surface.")]
    [SerializeField, Min(0f)] private float raycastSkin = 0.01f;

    // --- MASQUE (TAILLE si pas d'Image/RawImage) ---
    [Header("Taille du masque (pixels écran, si pas de Graphic)")]
    [SerializeField, Min(1f)] private float maskWidthPx = 260f;
    [SerializeField, Min(1f)] private float maskHeightPx = 260f;
    [SerializeField] private bool lockAspectCircle = false;

    // --- OFFSETS ---
    [Header("Offsets centre du masque")]
    [Tooltip("Décalage monde appliqué au point de visée sur la cible (m).")]
    [SerializeField] private Vector3 worldOffset = Vector3.zero;
    [Tooltip("Décalage du centre du masque en unités UI (avant Canvas Scaler).")]
    [SerializeField] private Vector2 screenOffsetUI = Vector2.zero;

    // --- UI OPTIONNELLE (forme du masque) ---
    [Header("UI (optionnel) : forme du masque")]
    [Tooltip("Associe une Image OU une RawImage dont l'alpha définit la forme du masque.")]
    [SerializeField] private Graphic maskGraphic; // Image OU RawImage
    private RectTransform maskRect;              // si maskGraphic fourni
    private Canvas rootCanvas;                   // canvas parent si UI

    // --- SHADER ---
    [Header("Shader d'occlusion")]
    [Tooltip("TransparentCutout. Supporte _MaskCenter/_MaskRadiusXY et (optionnel) _MaskTex.")]
    [SerializeField] private Shader occlusionShader;
    [Range(0f, 1f)] public float maskTextureCutoff = 0.5f; // seuil alpha pour discard

    // --- internes ---
    private Renderer targetRenderer;
    private readonly Dictionary<Renderer, Material[]> originalMaterials = new();
    private Renderer currentMasked; // un seul à la fois

    // ---------- Lifecycle ----------
    private void Awake()
    {
        if (!maskCamera) maskCamera = Camera.main;
        if (target) targetRenderer = target.GetComponentInChildren<Renderer>();
        if (!occlusionShader) occlusionShader = Shader.Find("Custom/OcclusionCircleCutout");
        if (maskGraphic)
        {
            maskRect = maskGraphic.rectTransform;
            rootCanvas = maskRect.GetComponentInParent<Canvas>();
        }
    }

    private void LateUpdate()
    {
        // Désactive l'occlusion si une Timeline est en cours de lecture pour
        // empêcher la visibilité du joueur à travers les obstacles pendant les cinématiques.
        if (TimelineManager.Instance != null && TimelineManager.Instance.IsTimelinePlaying)
        {
            ClearAll();
            return;
        }

        if (!target || !maskCamera) { ClearAll(); return; }

        // --- Géométrie de base ---
        Vector3 camPos = maskCamera.transform.position;
        Vector3 targetCenterWorld = (targetRenderer ? targetRenderer.bounds.center : target.position) + worldOffset;

        Vector3 seg = targetCenterWorld - camPos;
        float segLen = seg.magnitude;
        if (segLen < 1e-4f) { ClearAll(); return; }
        Vector3 dir = seg / segLen;

        // 1) CANDIDATS DANS LE CÔNE (OverlapSphere)
        var candidates = new HashSet<Renderer>();
        int samples = Mathf.Max(3, coneSamples);
        for (int i = 1; i <= samples; i++)
        {
            float t = (float)i / samples;
            Vector3 c = camPos + dir * (segLen * t);
            float r = coneBaseRadius * t;

            var cols = Physics.OverlapSphere(c, r, obstacleLayers, QueryTriggerInteraction.Ignore);
            if (cols == null) continue;
            foreach (var col in cols)
            {
                if (!col) continue;
                if (target && col.transform.IsChildOf(target)) continue;
                var rend = col.GetComponentInParent<Renderer>();
                if (rend) candidates.Add(rend);
            }
        }

        // 2) RAYON CENTRAL (caméra→cible), on garde le hit candidate le plus proche de la cible
        Vector3 origin = camPos + dir * raycastSkin;
        float maxDist = Mathf.Max(0f, segLen - raycastSkin);

        bool prev = Physics.queriesHitBackfaces;
        if (hitBackfaces) Physics.queriesHitBackfaces = true;
        var hits = Physics.RaycastAll(origin, dir, maxDist, obstacleLayers, QueryTriggerInteraction.Ignore);
        if (hitBackfaces) Physics.queriesHitBackfaces = prev;

        Renderer chosen = null;
        float bestDist = -1f;
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (!h.collider) continue;
            var rend = h.collider.GetComponentInParent<Renderer>();
            if (!rend) continue;
            if (!candidates.Contains(rend)) continue;
            if (target && h.collider.transform.IsChildOf(target)) continue;
            if (h.distance > bestDist) { bestDist = h.distance; chosen = rend; }
        }
        if (!chosen) { ClearAll(); return; }

        // 3) CENTRE DU MASQUE = projection écran de la cible
        // -> écran pixels (depuis la caméra, respecte camera.rect), + offset UI (converti en px)
        Vector3 vp = maskCamera.WorldToViewportPoint(targetCenterWorld);
        vp.x = Mathf.Clamp01(vp.x); vp.y = Mathf.Clamp01(vp.y);
        Vector3 screenFromCam = maskCamera.ViewportToScreenPoint(vp);

        float scale = (rootCanvas ? rootCanvas.scaleFactor : 1f);
        Vector2 offsetPx = screenOffsetUI * Mathf.Max(0.0001f, scale);
        Vector2 screenPt = new Vector2(screenFromCam.x + offsetPx.x, screenFromCam.y + offsetPx.y);

        // Si UI : positionne l'élément (sa texture sert de shape)
        if (maskRect && rootCanvas)
            maskRect.anchoredPosition = ScreenToCanvasAnchoredPos(rootCanvas, screenPt);

        // 4) Paramètres pour le shader (repère ÉCRAN GLOBAL 0..1 + flip Y, comme le shader)
        // Centre
        Vector2 maskCenter01 = new(
            screenPt.x / Mathf.Max(1f, Screen.width),
            screenPt.y / Mathf.Max(1f, Screen.height)
        );
#if UNITY_2019_1_OR_NEWER
        bool yFlip = SystemInfo.graphicsUVStartsAtTop;
#else
        bool yFlip = true;
#endif
        if (yFlip) maskCenter01.y = 1f - maskCenter01.y;

        // Taille (si Graphic fourni : on prend sa taille visuelle ; sinon fallback)
        float wpx, hpx;
        if (maskRect) // forme UI → utilise sa taille
        {
            // anchored size en unités UI -> px en multipliant par scaleFactor
            wpx = Mathf.Max(1f, maskRect.rect.width * scale);
            hpx = Mathf.Max(1f, maskRect.rect.height * scale);
        }
        else
        {
            wpx = Mathf.Max(1f, maskWidthPx);
            hpx = Mathf.Max(1f, lockAspectCircle ? maskWidthPx : maskHeightPx);
        }

        float rx = wpx * 0.5f;
        float ry = hpx * 0.5f;

        // Rayons normalisés ÉCRAN GLOBAL
        Vector2 maskRadius01XY = new(
            rx / Mathf.Max(1f, Screen.width),
            ry / Mathf.Max(1f, Screen.height)
        );
        float maskRadiusFallback = Mathf.Max(maskRadius01XY.x, maskRadius01XY.y);

        // 5) Appliquer / MAJ sur le renderer choisi
        if (currentMasked != chosen)
        {
            if (currentMasked) Restore(currentMasked);

            if (!originalMaterials.ContainsKey(chosen))
                originalMaterials[chosen] = chosen.materials;

            var mats = new Material[chosen.materials.Length];
            for (int m = 0; m < mats.Length; m++)
            {
                var baseMat = chosen.materials[m];
                var mat = new Material(occlusionShader);
                if (baseMat && baseMat.HasProperty("_MainTex"))
                    mat.SetTexture("_MainTex", baseMat.GetTexture("_MainTex"));
                mats[m] = mat;
            }
            chosen.materials = mats;
            currentMasked = chosen;
        }

        if (currentMasked)
        {
            var mats = currentMasked.materials;
            Texture maskTex = null;
            if (maskGraphic) maskTex = maskGraphic.mainTexture;

            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i]; if (!mat) continue;

                mat.SetVector("_MaskCenter", maskCenter01);

                if (maskTex) // mode "forme texturée"
                {
                    mat.SetTexture("_MaskTex", maskTex);
                    mat.SetFloat("_MaskUseTex", 1f);
                    mat.SetFloat("_MaskTexCutoff", maskTextureCutoff);
                    // rayon XY malgré tout (sert à projeter l'écran dans l'espace du masque)
                    mat.SetVector("_MaskRadiusXY", maskRadius01XY);
                }
                else // fallback ellipse/cercle
                {
                    mat.SetFloat("_MaskUseTex", 0f);
                    if (mat.HasProperty("_MaskRadiusXY"))
                        mat.SetVector("_MaskRadiusXY", maskRadius01XY);
                    else
                        mat.SetFloat("_MaskRadius", maskRadiusFallback);
                }
            }
        }
    }

    private void OnDisable() => ClearAll();

    private void ClearAll()
    {
        if (currentMasked) Restore(currentMasked);
        currentMasked = null;
        originalMaterials.Clear();
    }

    private void Restore(Renderer r)
    {
        if (r && originalMaterials.TryGetValue(r, out var mats))
            r.materials = mats;
    }

    // ---------- Helpers ----------
    /// Convertit un point écran (pixels) en anchoredPosition locale dans le Canvas,
    /// pour Overlay / ScreenSpaceCamera / WorldSpace.
    private static Vector2 ScreenToCanvasAnchoredPos(Canvas canvas, Vector2 screenPt)
    {
        var canvasRect = (RectTransform)canvas.transform;
        Camera cam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPt, cam, out var local);
        return local;
    }

#if UNITY_EDITOR
    // Visualisation du cône (optionnel)
    private void OnDrawGizmosSelected()
    {
        if (!target || !maskCamera) return;
        Vector3 camPos = maskCamera.transform.position;
        Vector3 tgt = target.position + worldOffset;
        Vector3 seg = tgt - camPos;
        float len = seg.magnitude;
        if (len < 1e-4f) return;
        Vector3 dir = seg / len;

        Handles.color = new Color(1f, 0.7f, 0f, 0.6f);
        int samples = Mathf.Max(3, coneSamples);
        for (int i = 1; i <= samples; i++)
        {
            float t = (float)i / samples;
            Vector3 c = camPos + dir * (len * t);
            float r = coneBaseRadius * t;
            Handles.DrawWireDisc(c, dir, r);
        }
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(camPos, tgt);
    }
#endif
}
