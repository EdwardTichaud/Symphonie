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
    [SerializeField] private float maskSize = 250f;

    private RectTransform maskRect;
    // Liste des Renderers rendus invisibles afin de pouvoir les réafficher ensuite
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();

    private void Awake()
    {
        if (maskObject != null)
        {
            maskRect = maskObject.GetComponent<RectTransform>();
            maskObject.SetActive(false);
        }
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
            Vector3 screenPos = maskCamera.WorldToScreenPoint(target.position);
            maskRect.position = screenPos;
            maskRect.sizeDelta = Vector2.one * maskSize;

            // Désactivation des renderers des obstacles détectés
            foreach (RaycastHit hit in hits)
            {
                Renderer rend = hit.collider.GetComponent<Renderer>();
                if (rend != null && !hiddenRenderers.Contains(rend))
                {
                    rend.enabled = false; // On masque l'obstacle
                    hiddenRenderers.Add(rend); // On garde une trace pour le réactiver plus tard
                }
            }
        }
        else
        {
            if (maskObject.activeSelf)
                maskObject.SetActive(false);
        }

        // Réactivation des obstacles qui ne sont plus occultants
        for (int i = hiddenRenderers.Count - 1; i >= 0; i--)
        {
            Renderer rend = hiddenRenderers[i];
            bool stillHidden = false;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponent<Renderer>() == rend)
                {
                    stillHidden = true;
                    break;
                }
            }

            if (!stillHidden)
            {
                if (rend != null)
                    rend.enabled = true; // On réaffiche l'obstacle
                hiddenRenderers.RemoveAt(i);
            }
        }
    }
}
