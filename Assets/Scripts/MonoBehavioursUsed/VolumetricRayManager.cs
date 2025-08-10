using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Configuration rapide des effets de brume volumétrique et des rayons lumineux selon la
/// "recette 2 minutes" décrite dans la documentation. Ce script peut être placé dans la scène
/// principale pour automatiser les paramètres nécessaires.
/// </summary>
public class VolumetricRayManager : MonoBehaviour
{
    [Tooltip("Caméra cible. Si laissé vide, la caméra principale sera utilisée.")]
    public Camera targetCamera;

    [Tooltip("Lumière directionnelle représentant le soleil. Si laissé vide, RenderSettings.sun sera utilisée.")]
    public Light sunLight;

    void Awake()
    {
        // Récupération des références si elles ne sont pas renseignées dans l'inspecteur
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (sunLight == null)
            sunLight = RenderSettings.sun;

        // Application des réglages
        SetupCamera();
        SetupSun();
        SetupGlobalFog();
        SetupLocalFog();
    }

    /// <summary>
    /// Active les options nécessaires sur la caméra pour rendre la volumétrie, les ombres
    /// et la diffusion atmosphérique.
    /// </summary>
    void SetupCamera()
    {
        if (targetCamera == null)
            return;

        var hdCam = targetCamera.GetComponent<HDAdditionalCameraData>();
        if (hdCam != null)
        {
            // Autorise les paramètres personnalisés
            hdCam.customRenderingSettings = true;

            // Active les Volumetrics, les Ombres et l'Atmospheric Scattering
            hdCam.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.Volumetrics, true);
            hdCam.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.Shadow, true);
            hdCam.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.AtmosphericScattering, true);
        }
    }

    /// <summary>
    /// Configure la lumière directionnelle : intensité élevée, volumétrie et ombres activées
    /// avec une résolution suffisante.
    /// </summary>
    void SetupSun()
    {
        if (sunLight == null)
            return;

        // Intensité en Lux pour obtenir des rayons visibles
        sunLight.intensity = 80000f;

        var hdLight = sunLight.GetComponent<HDAdditionalLightData>();
        if (hdLight != null)
        {
            // Active la volumétrie sur la lumière
            hdLight.EnableVolumetric(true);
            hdLight.volumetricDimmer = 1.5f; // Multiplier 1.5 = valeur sûre
            hdLight.shadowDimmer = 1f;
            hdLight.shadowResolution = 2048; // Résolution suffisante pour les ombres du feuillage
        }
    }

    /// <summary>
    /// Crée un Volume global avec des réglages de brume permettant aux rayons de se dessiner.
    /// </summary>
    void SetupGlobalFog()
    {
        // Création d'un objet Volume global s'il n'existe pas déjà
        GameObject volumeGO = new GameObject("Global Volumetric Fog");
        var volume = volumeGO.AddComponent<Volume>();
        volume.isGlobal = true;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.profile = profile;

        // Ajout et configuration du composant Fog
        var fog = profile.Add<Fog>(true);
        fog.enabled.Override(true);
        fog.meanFreePath.Override(90f); // Distance d'atténuation ~90 => bonne brume générale
        fog.albedo.Override(new Color32(0x11, 0x11, 0x11, 0xFF)); // Teinte très sombre
        fog.anisotropy.Override(0.85f); // Scattering orienté vers l'avant
        fog.directionalLightsOnly.Override(true); // Seules les lumières directionnelles affectent la brume
        fog.denoisingMode.Override(FogDenoisingMode.Gaussian); // Lissage gaussien des volumes
    }

    /// <summary>
    /// Place un volume de brume local (DensityVolume) entre le soleil et le sol pour renforcer
    /// les faisceaux de lumière.
    /// </summary>
    void SetupLocalFog()
    {
        GameObject localFog = new GameObject("Local Volumetric Fog");
        var density = localFog.AddComponent<DensityVolume>();

        // Taille étroite mais haute pour simuler la brume entre les arbres
        density.parameters.size = new Vector3(10f, 20f, 10f);
        density.parameters.meanFreePath = 12f; // Distance / densité de la brume
        density.parameters.anisotropy = 0.9f; // Scattering très orienté vers l'avant
        density.parameters.blendDistance = 3f; // Transition douce
    }
}
