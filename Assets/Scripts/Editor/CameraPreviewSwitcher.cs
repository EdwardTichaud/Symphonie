using UnityEngine;
using UnityEditor;

/// <summary>
/// Outils d'aperçu rapide pour afficher les caméras dans la GameView.
/// Permet de basculer entre la caméra du monde et celle de combat
/// sans perdre les RenderTextures assignées.
/// </summary>
public static class CameraPreviewSwitcher
{
    // Sauvegardes temporaires des RenderTextures afin de pouvoir les restaurer.
    private static RenderTexture worldSavedRT;
    private static RenderTexture battleSavedRT;

    /// <summary>
    /// Affiche la caméra principale (MainCamera) directement dans la GameView.
    /// </summary>
    [MenuItem("Tools/Camera Preview/Aperçu World Camera")]
    public static void PreviewWorldCamera()
    {
        Camera world = FindCameraWithTag("MainCamera");
        Camera battle = FindCameraWithTag("BattleCamera");

        if (world == null)
        {
            Debug.LogWarning("[CameraPreview] Caméra 'MainCamera' introuvable.");
            return;
        }

        // Restaure la RT de la BattleCamera si elle avait été désassignée
        if (battle != null && battle.targetTexture == null && battleSavedRT != null)
        {
            battle.targetTexture = battleSavedRT;
        }

        // Sauvegarde puis désactive la RT pour afficher directement dans la GameView
        if (world.targetTexture != null)
        {
            worldSavedRT = world.targetTexture;
            world.targetTexture = null;
        }

        Debug.Log("[CameraPreview] Aperçu World Camera activé.");
    }

    /// <summary>
    /// Affiche la BattleCamera directement dans la GameView.
    /// </summary>
    [MenuItem("Tools/Camera Preview/Aperçu Battle Camera")]
    public static void PreviewBattleCamera()
    {
        Camera world = FindCameraWithTag("MainCamera");
        Camera battle = FindCameraWithTag("BattleCamera");

        if (battle == null)
        {
            Debug.LogWarning("[CameraPreview] Caméra 'BattleCamera' introuvable.");
            return;
        }

        // Restaure la RT de la WorldCamera si besoin
        if (world != null && world.targetTexture == null && worldSavedRT != null)
        {
            world.targetTexture = worldSavedRT;
        }

        // Sauvegarde puis désactive la RT pour afficher la BattleCamera
        if (battle.targetTexture != null)
        {
            battleSavedRT = battle.targetTexture;
            battle.targetTexture = null;
        }

        Debug.Log("[CameraPreview] Aperçu Battle Camera activé.");
    }

    /// <summary>
    /// Restaure les RenderTextures précédemment sauvegardées sur les caméras.
    /// </summary>
    [MenuItem("Tools/Camera Preview/Restaurer les RenderTextures")]
    public static void RestoreRenderTextures()
    {
        Camera world = FindCameraWithTag("MainCamera");
        Camera battle = FindCameraWithTag("BattleCamera");

        if (world != null && worldSavedRT != null)
        {
            world.targetTexture = worldSavedRT;
            worldSavedRT = null;
        }

        if (battle != null && battleSavedRT != null)
        {
            battle.targetTexture = battleSavedRT;
            battleSavedRT = null;
        }

        Debug.Log("[CameraPreview] RenderTextures restaurées.");
    }

    /// <summary>
    /// Cherche une caméra par son tag, même si elle est désactivée.
    /// </summary>
    private static Camera FindCameraWithTag(string tag)
    {
        Camera[] allCams = Resources.FindObjectsOfTypeAll<Camera>();
        foreach (Camera cam in allCams)
        {
            if (cam.CompareTag(tag))
                return cam;
        }
        return null;
    }
}
