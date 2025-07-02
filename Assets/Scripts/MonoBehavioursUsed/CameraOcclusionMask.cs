using UnityEngine;
using UnityEngine.UI;

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

        Vector3 dir = target.position - transform.position;
        bool occluded = Physics.Raycast(transform.position, dir.normalized, dir.magnitude, obstacleLayers);

        if (occluded)
        {
            if (!maskObject.activeSelf)
                maskObject.SetActive(true);

            Vector3 screenPos = maskCamera.WorldToScreenPoint(target.position);
            maskRect.position = screenPos;
            maskRect.sizeDelta = Vector2.one * maskSize;
        }
        else
        {
            if (maskObject.activeSelf)
                maskObject.SetActive(false);
        }
    }
}
