using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Configure automatiquement la "Glow-Up Scene" pour obtenir le rendu stylisé décrit dans la documentation.
/// Tout est généré en C# afin d'éviter une scène trop lourde à maintenir.
/// </summary>
public class GlowUpSceneSetup : MonoBehaviour
{
    [Header("Paramètres de profondeur de champ")]
    [Tooltip("Distance actuelle entre la caméra et Lucian en mètres.")]
    public float distanceLucian = 10f; // valeur par défaut pour calculer le DOF

    private void Start()
    {
        // 1) Caméra principale ------------------------------------------------------
        // On récupère ou crée la caméra principale puis on applique les réglages
        Camera cam = Camera.main;
        if (cam == null)
        {
            cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
        }

        // Ajout des données HDRP pour accéder aux paramètres physiques
        HDAdditionalCameraData hdCam = cam.gameObject.GetComponent<HDAdditionalCameraData>();
        if (hdCam == null)
            hdCam = cam.gameObject.AddComponent<HDAdditionalCameraData>();

        // Paramètres d'exposition manuelle (ISO, Shutter, Aperture, Compensation)
        hdCam.physicalParameters.exposureMode = HDPhysicalCameraExposureMode.Manual;
        hdCam.physicalParameters.iso = 100f;
        hdCam.physicalParameters.shutterSpeed = 1f / 60f;
        hdCam.physicalParameters.aperture = 5.6f;
        hdCam.physicalParameters.compensation = 0f;

        // L'éclairage ambiant doit suivre le ciel dynamique pour conserver un rendu naturel
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;

        // Petit spot très doux attaché à la caméra pour "caresser" Lucian
        GameObject muninLightObj = new GameObject("Munin Light");
        muninLightObj.transform.SetParent(cam.transform);
        muninLightObj.transform.localPosition = Vector3.zero;
        muninLightObj.transform.localRotation = Quaternion.identity;
        Light muninLight = muninLightObj.AddComponent<Light>();
        muninLight.type = LightType.Spot;
        muninLight.shadows = LightShadows.None;
        HDAdditionalLightData hdMunin = muninLightObj.AddComponent<HDAdditionalLightData>();
        hdMunin.lightUnit = LightUnit.Lux;
        hdMunin.intensity = 80f; // 50–100 lux
        hdMunin.spotAngle = 30f;

        // 2) Volume de post-traitement ---------------------------------------------
        // On crée un Volume global qui contiendra tous les effets graphiques.
        Volume volume = cam.gameObject.GetComponent<Volume>();
        if (volume == null)
        {
            volume = cam.gameObject.AddComponent<Volume>();
            volume.isGlobal = true; // Effets appliqués sur toute la scène
        }

        // Si aucun profil n'est présent, on en crée un nouveau
        if (volume.profile == null)
            volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        VolumeProfile profile = volume.profile;

        // Tonemapping ACES ---------------------------------------------------------
        Tonemapping tonemapping = profile.Add<Tonemapping>(false);
        tonemapping.mode.value = TonemappingMode.ACES;

        // Bloom doux ---------------------------------------------------------------
        Bloom bloom = profile.Add<Bloom>(false);
        bloom.intensity.value = 0.3f; // entre 0.2 et 0.35
        bloom.threshold.value = 1.2f; // entre 1.1 et 1.3
        bloom.scatter.value = 0.7f;   // diffusion

        // Vignette légère ----------------------------------------------------------
        Vignette vignette = profile.Add<Vignette>(false);
        vignette.intensity.value = 0.18f;
        vignette.smoothness.value = 0.4f;

        // Aberration chromatique ---------------------------------------------------
        ChromaticAberration ca = profile.Add<ChromaticAberration>(false);
        ca.intensity.value = 0.03f;

        // Profondeur de champ ------------------------------------------------------
        DepthOfField dof = profile.Add<DepthOfField>(false);
        dof.focusMode.value = DepthOfFieldMode.UsePhysicalCamera;
        dof.focusDistance.value = distanceLucian + 2f; // focus légèrement derrière Lucian
        dof.aperture.value = 4f;
        dof.bladeCount.value = 7;

        // Occlusion ambiante -------------------------------------------------------
        ScreenSpaceAmbientOcclusion ssao = profile.Add<ScreenSpaceAmbientOcclusion>(false);
        ssao.intensity.value = 0.7f;
        ssao.radius.value = 0.35f;
        ssao.quality.value = ScalableSettingLevelParameter.Level.High;

        // Global Illumination optionnelle (désactivée par défaut pour rester stylisée)
        ScreenSpaceGlobalIllumination ssgi = profile.Add<ScreenSpaceGlobalIllumination>(false);
        ssgi.intensity.value = 0.4f;
        ssgi.active = false; // l'utilisateur peut l'activer dans l'inspecteur si besoin

        // Color grading ------------------------------------------------------------
        WhiteBalance wb = profile.Add<WhiteBalance>(false);
        wb.temperature.value = -10f; // légère correction vers le froid

        ColorAdjustments colorAdj = profile.Add<ColorAdjustments>(false);
        colorAdj.lift.value = new Vector4(0.97f, 0.98f, 1f, 0f);
        colorAdj.gamma.value = new Vector4(1.02f, 1.02f, 1.02f, 0f);
        colorAdj.gain.value = new Vector4(1.05f, 1.05f, 1.05f, 0f);
        colorAdj.saturation.value = 1.1f;

        // 3) Ciel et ambiance ------------------------------------------------------
        VisualEnvironment visualEnv = profile.Add<VisualEnvironment>(false);
        visualEnv.skyType.value = (int)SkyType.PhysicallyBased;

        PhysicallyBasedSky sky = profile.Add<PhysicallyBasedSky>(false);
        sky.sunSize.value = 0.5f;

        IndirectLightingController indirect = profile.Add<IndirectLightingController>(false);
        indirect.indirectDiffuseLightingMultiplier.value = 1.1f;
        indirect.indirectSpecularLightingMultiplier.value = 0.9f;

        // 4) Brume & Volumétriques -------------------------------------------------
        Fog fog = profile.Add<Fog>(false);
        fog.enabled.value = true;
        fog.fogType.value = FogType.Volumetric;
        fog.baseFogDistance.value = 100f; // moyenne entre 80 et 120 m
        fog.anisotropy.value = 0.6f;
        fog.baseHeight.value = 0f;
        fog.heightAttenuation.value = 0.15f;

        // 5) Lumière directionnelle principale ------------------------------------
        GameObject lightObj = new GameObject("Main Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        HDAdditionalLightData hdLight = lightObj.AddComponent<HDAdditionalLightData>();
        hdLight.lightUnit = LightUnit.Lux;
        hdLight.intensity = 80000f; // moyenne entre 70k et 90k
        hdLight.shadowResolution = 4096; // Shadowmap 4k
        hdLight.enableContactShadows = true;
        hdLight.SetShadowCascades(4); // 4 cascades
        hdLight.shadowDistance = 100f;
        hdLight.SetColor(Color.white, 5800f); // temp 6500K -> 5800K

        // 6) Lumières secondaires : Fill & Rim -------------------------------------
        // Deux Area Lights pour déboucher les ombres sans créer d'ombres supplémentaires
        for (int i = 0; i < 2; i++)
        {
            GameObject fillObj = new GameObject($"Fill Light {i + 1}");
            Light fillLight = fillObj.AddComponent<Light>();
            fillLight.type = LightType.Rectangle; // Area Light HDRP
            fillLight.color = new Color(0.8f, 0.9f, 1f); // teinte froide
            fillLight.shadows = LightShadows.None; // pas d'ombres pour la fill

            // Données HDRP pour gérer l'intensité en lux
            HDAdditionalLightData hdFill = fillObj.AddComponent<HDAdditionalLightData>();
            hdFill.lightUnit = LightUnit.Lux;
            hdFill.intensity = 400f; // entre 200 et 600 lux

            // Position symétrique autour de la zone centrale
            fillObj.transform.position = new Vector3(i == 0 ? -5f : 5f, 3f, -3f);
            fillObj.transform.LookAt(Vector3.zero);
        }

        // Spot latéral pour détacher le personnage avec une teinte complémentaire
        GameObject rimObj = new GameObject("Rim Light");
        Light rimLight = rimObj.AddComponent<Light>();
        rimLight.type = LightType.Spot;
        rimLight.color = new Color(1f, 0.85f, 0.7f); // légèrement chaude
        rimLight.spotAngle = 20f; // angle serré
        rimLight.shadows = LightShadows.None;

        HDAdditionalLightData hdRim = rimObj.AddComponent<HDAdditionalLightData>();
        hdRim.lightUnit = LightUnit.Lux;
        hdRim.intensity = 4000f; // 2–5k lux

        rimObj.transform.position = new Vector3(0f, 3f, 4f);
        rimObj.transform.LookAt(Vector3.zero);

        // 7) Reflection Probe ------------------------------------------------------
        GameObject probeObj = new GameObject("Global Reflection Probe");
        ReflectionProbe probe = probeObj.AddComponent<ReflectionProbe>();
        probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
        probe.boxProjection = true;
        probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
        probe.intensity = 0.8f;

        // 8) Motion Blur minimal (ajouté pour les dash/teleport) --------------------
        MotionBlur motionBlur = profile.Add<MotionBlur>(false);
        motionBlur.intensity.value = 0f; // pas d'effet par défaut
        motionBlur.shutterAngle.value = 0.2f;
    }
}
