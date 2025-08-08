using UnityEngine;
using UnityEngine.Rendering;

public class RenderingLayerHelper
{
    public static string[] GetRenderingLayerNames()
    {
        return GraphicsSettings.renderingLayerNames;
    }
}
