using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;

public class GPUCategoryHud : MonoBehaviour
{
    private Recorder recShadows;
    private Recorder recOpaque;
    private Recorder recTransparent;
    private Recorder recPostFX;
    private float fps;
    private float gpuShadows, gpuOpaque, gpuTransparent, gpuPostFX;

    void OnEnable()
    {
        // Récupération des recorders GPU (noms issus du Profiler Unity)
        recShadows = Recorder.Get("Shadows.RenderLoopJob");
        recOpaque = Recorder.Get("RenderLoop.Draw");
        recTransparent = Recorder.Get("RenderLoop.DrawTransparent");
        recPostFX = Recorder.Get("PostLateUpdate.RenderPostProcessing");
    }

    void Update()
    {
        fps = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

        gpuShadows = recShadows?.elapsedNanoseconds / 1_000_000f ?? 0f;
        gpuOpaque = recOpaque?.elapsedNanoseconds / 1_000_000f ?? 0f;
        gpuTransparent = recTransparent?.elapsedNanoseconds / 1_000_000f ?? 0f;
        gpuPostFX = recPostFX?.elapsedNanoseconds / 1_000_000f ?? 0f;
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle { fontSize = 16, normal = { textColor = Color.white } };
        float y = 10;
        GUI.Label(new Rect(10, y, 400, 25), $"FPS: {fps:F1}", style); y += 25;
        GUI.Label(new Rect(10, y, 400, 25), $"GPU Shadows: {gpuShadows:F2} ms", style); y += 25;
        GUI.Label(new Rect(10, y, 400, 25), $"GPU Opaque: {gpuOpaque:F2} ms", style); y += 25;
        GUI.Label(new Rect(10, y, 400, 25), $"GPU Transparent: {gpuTransparent:F2} ms", style); y += 25;
        GUI.Label(new Rect(10, y, 400, 25), $"GPU PostFX: {gpuPostFX:F2} ms", style); y += 25;
    }
}
