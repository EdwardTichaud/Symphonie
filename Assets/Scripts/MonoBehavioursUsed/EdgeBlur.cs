using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[System.Serializable]
[VolumeComponentMenu("Post-processing/Custom/Edge Blur")]
public sealed class EdgeBlur : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    [Tooltip("Etendue du flou depuis les bords (0 = bords uniquement, 1 = jusqu'au centre).")]
    public ClampedFloatParameter edgeBlurAmount = new ClampedFloatParameter(0f, 0f, 1f);

    private static readonly int EdgeBlurAmountId = Shader.PropertyToID("_EdgeBlurAmount");
    private static readonly int InputTextureId = Shader.PropertyToID("_InputTexture");
    private Material material;

    public bool IsActive() => material != null && edgeBlurAmount.value > 0.0001f;

    public bool IsTileCompatible() => false;

    public override CustomPostProcessInjectionPoint injectionPoint =>
        CustomPostProcessInjectionPoint.AfterPostProcessBlurs;

    public override void Setup()
    {
        Shader shader = Shader.Find("Hidden/Symphonie/EdgeBlur");
        if (shader == null)
        {
            Debug.LogWarning("[EdgeBlur] Shader introuvable: Hidden/Symphonie/EdgeBlur");
            return;
        }

        material = CoreUtils.CreateEngineMaterial(shader);
    }

    public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
    {
        if (material == null)
            return;

        material.SetFloat(EdgeBlurAmountId, edgeBlurAmount.value);
        material.SetTexture(InputTextureId, source);
        HDUtils.DrawFullScreen(cmd, material, destination);
    }

    public override void Cleanup()
    {
        CoreUtils.Destroy(material);
    }
}
