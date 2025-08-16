using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Affiche un masque circulaire autour de la cible lorsque celle-ci est occultée par un obstacle.
/// </summary>
public class CameraOcclusionMask : MonoBehaviour
{
    [Header("Cible")] [SerializeField] private Transform target;
    [Header("Couches Obstacles")] [SerializeField] private LayerMask obstacleLayers = -1;

    [Header("Masque Visuel")] [SerializeField] private Camera maskCamera;
    [SerializeField] private GameObject maskObject;
    [Tooltip("Marge supplémentaire autour de la silhouette de la cible")]
    [SerializeField] private float sizePadding = 20f;

    [Header("Shader de masque circulaire")]
    [Tooltip("Shader appliqué aux obstacles afin d'y découper un cercle transparent")] 
    [SerializeField] private Shader occlusionShader;

    // Renderer principal de la cible utilisé pour calculer sa taille à l'écran
    private Renderer targetRenderer;

    private RectTransform maskRect;
    // Sauvegarde des matériaux originaux pour pouvoir rétablir l'affichage des obstacles
    private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    // Liste des renderers utilisant actuellement le shader de découpe
    private readonly List<Renderer> maskedRenderers = new List<Renderer>();

    private void Awake()
    {
        if (maskObject != null)
        {
            maskRect = maskObject.GetComponent<RectTransform>();
            maskObject.SetActive(false); // le masque est invisible par défaut
        }

        // Récupération du renderer de la cible pour estimer son empreinte à l'écran
        if (target != null)
            targetRenderer = target.GetComponentInChildren<Renderer>();

        // Si aucun shader n'est renseigné dans l'inspecteur, on tente de le charger par défaut
        if (occlusionShader == null)
            occlusionShader = Shader.Find("Custom/OcclusionCircleCutout");
    }

    private void LateUpdate()
    {
        if (target == null || maskRect == null || maskCamera == null) return;

        Vector3 dir = target.position - transform.position; // Direction caméra -> cible
        float distance = dir.magnitude; // Distance jusqu'à la cible
        dir.Normalize();

        // RaycastAll pour récupérer tous les obstacles entre la caméra et la cible
        RaycastHit[] hits = Physics.RaycastAll(transform.position, dir, distance, obstacleLayers);
        bool occluded = hits.Length > 0;

        if (occluded)
        {
            if (!maskObject.activeSelf)
                maskObject.SetActive(true);

            // Positionnement du masque autour de la cible
            Vector3 worldCenter = targetRenderer != null ? targetRenderer.bounds.center : target.position;
            Vector3 screenCenter = maskCamera.WorldToScreenPoint(worldCenter);
            screenCenter.x = Mathf.Clamp(screenCenter.x, 0, Screen.width);
            screenCenter.y = Mathf.Clamp(screenCenter.y, 0, Screen.height);
            maskRect.position = screenCenter;

            // Taille du masque proportionnelle à la zone occupée par la cible à l'écran
            float computedSize = 0f;
            if (targetRenderer != null)
            {
                Bounds b = targetRenderer.bounds;
                Vector3 c = b.center;
                Vector3 e = b.extents;

                // Calcul des 8 coins de la bounding box projetés à l'écran
                Vector3[] corners = new Vector3[8];
                int i = 0;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                            corners[i++] = maskCamera.WorldToScreenPoint(c + Vector3.Scale(e, new Vector3(x, y, z)));

                float minX = corners[0].x, maxX = corners[0].x;
                float minY = corners[0].y, maxY = corners[0].y;
                for (i = 1; i < 8; i++)
                {
                    Vector3 p = corners[i];
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                    minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                }

                float width = Mathf.Clamp(maxX - minX, 10f, Screen.width);
                float height = Mathf.Clamp(maxY - minY, 10f, Screen.height);
                computedSize = Mathf.Max(width, height);
            }

            maskRect.sizeDelta = Vector2.one * (computedSize + sizePadding);

            // Calcul des paramètres normalisés du masque pour le shader
            Vector2 maskCenter01 = new Vector2(screenCenter.x / Screen.width, screenCenter.y / Screen.height);
            float maskRadius01 = (maskRect.sizeDelta.x * 0.5f) / Screen.width;

            // Application du shader de découpe à chaque obstacle touché
            foreach (RaycastHit hit in hits)
            {
                Renderer rend = hit.collider.GetComponent<Renderer>();
                if (rend == null) continue;

                // Si l'obstacle n'a pas encore été traité, on sauvegarde son matériau et on applique le shader
                if (!originalMaterials.ContainsKey(rend))
                {
                    originalMaterials[rend] = rend.materials;

                    Material[] newMats = new Material[rend.materials.Length];
                    for (int m = 0; m < newMats.Length; m++)
                    {
                        Material baseMat = rend.materials[m];
                        Material mat = new Material(occlusionShader);
                        // Conservation de la texture principale si elle existe
                        if (baseMat.HasProperty("_MainTex"))
                            mat.SetTexture("_MainTex", baseMat.GetTexture("_MainTex"));
                        newMats[m] = mat;
                    }
                    rend.materials = newMats;
                    maskedRenderers.Add(rend);
                }

                // Mise à jour des propriétés du masque pour ce renderer
                foreach (Material mat in rend.materials)
                {
                    mat.SetVector("_MaskCenter", maskCenter01);
                    mat.SetFloat("_MaskRadius", maskRadius01);
                }
            }
        }
        else
        {
            if (maskObject.activeSelf)
                maskObject.SetActive(false);

            // Restauration des matériaux d'origine lorsque l'occlusion n'est plus présente
            foreach (Renderer rend in maskedRenderers)
            {
                if (rend != null && originalMaterials.ContainsKey(rend))
                    rend.materials = originalMaterials[rend];
            }
            maskedRenderers.Clear();
            originalMaterials.Clear();
        }
    }
}
