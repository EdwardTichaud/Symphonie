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
    // Position d'affichage afin d'éviter tout chevauchement
    public Vector2 origin = new Vector2(10, 350);

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
        // Style du texte plus grand pour une meilleure visibilité
        GUIStyle style = new GUIStyle { fontSize = 20, normal = { textColor = Color.white } };
        float x = origin.x;
        float y = origin.y;
        GUI.Label(new Rect(x, y, 400, 30), $"FPS: {fps:F1}", style); y += 30;
        GUI.Label(new Rect(x, y, 400, 30), $"GPU Shadows: {gpuShadows:F2} ms", style); y += 30;
        GUI.Label(new Rect(x, y, 400, 30), $"GPU Opaque: {gpuOpaque:F2} ms", style); y += 30;
        GUI.Label(new Rect(x, y, 400, 30), $"GPU Transparent: {gpuTransparent:F2} ms", style); y += 30;
        GUI.Label(new Rect(x, y, 400, 30), $"GPU PostFX: {gpuPostFX:F2} ms", style); y += 30;
    }
}
