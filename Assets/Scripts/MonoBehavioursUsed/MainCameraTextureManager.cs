using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gère l'affichage de la RenderTexture sur la MainCamera.
/// Ce script doit être placé sur l'objet qui contient le RawImage devant la caméra.
/// </summary>
public class MainCameraTextureManager : MonoBehaviour
{
    [Header("RawImage affichant la vue")]
    [SerializeField] private RawImage cameraDisplay;

    [Header("RenderTextures")]
    [SerializeField] private RenderTexture worldView;
    [SerializeField] private RenderTexture battleView;

    private void Awake()
    {
        // Si aucun RawImage n'est assigné, on tente de le récupérer automatiquement
        if (cameraDisplay == null)
            cameraDisplay = GetComponentInChildren<RawImage>();

        // Par défaut on affiche la vue du monde
        if (cameraDisplay != null && worldView != null)
            cameraDisplay.texture = worldView;
    }

    /// <summary>
    /// Applique la RenderTexture du monde sur le RawImage.
    /// </summary>
    public void ShowWorldView()
    {
        if (cameraDisplay != null && worldView != null)
            cameraDisplay.texture = worldView;
    }

    /// <summary>
    /// Applique la RenderTexture de combat sur le RawImage.
    /// </summary>
    public void ShowBattleView()
    {
        if (cameraDisplay != null && battleView != null)
            cameraDisplay.texture = battleView;
    }
}
