using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;
using System;
using System.Collections.Generic;

public class PerformanceHud : MonoBehaviour
{
    [Header("Affichage")]
    public bool showBackground = true;
    public Vector2 origin = new Vector2(10, 60); // décalé pour ne pas chevaucher l'affichage des FPS
    public int fontSize = 20; // texte plus grand
    public float refreshRate = 0.5f; // secondes entre recalculs

    // Recorders par catégorie (CPU)
    ProfilerRecorder scriptsTime;
    ProfilerRecorder physicsTime;
    ProfilerRecorder renderTime;
    ProfilerRecorder animationTime;
    ProfilerRecorder audioTime;
    ProfilerRecorder uiTime;
    ProfilerRecorder gcAllocFrame;

    // Compteurs rendu
    ProfilerRecorder drawCalls;
    ProfilerRecorder batches;
    ProfilerRecorder triangles;
    ProfilerRecorder vertices;

    // Accumulateurs
    float timeSinceUpdate;
    float fpsSmoothed;
    float cpuMs_Scripts, cpuMs_Physics, cpuMs_Render, cpuMs_Animation, cpuMs_Audio, cpuMs_UI;
    float gcKbPerFrame;
    long drawCallsCount, batchesCount, trisCount, vertsCount;
    float gpuMs;

    // Style GUI
    GUIStyle labelStyle;
    GUIStyle titleStyle;

    void OnEnable()
    {
        // Temps CPU par catégorie (nanosecondes -> on convertit en ms)
        scriptsTime = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Time", 15);
        physicsTime = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Time", 15);
        renderTime = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Time", 15);
        animationTime = ProfilerRecorder.StartNew(ProfilerCategory.Animation, "Time", 15);
        audioTime = ProfilerRecorder.StartNew(ProfilerCategory.Audio, "Time", 15);
        uiTime = ProfilerRecorder.StartNew(ProfilerCategory.Gui, "Time", 15);

        // GC alloué par frame
        gcAllocFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 15);

        // Compteurs de rendu (si disponibles)
        TryStart(out drawCalls, ProfilerCategory.Render, "Draw Calls Count", 15);
        TryStart(out batches, ProfilerCategory.Render, "Batches Count", 15);
        TryStart(out triangles, ProfilerCategory.Render, "Triangles Count", 15);
        TryStart(out vertices, ProfilerCategory.Render, "Vertices Count", 15);

        // Styles d'écriture, avec une taille de police plus grande pour la lisibilité
        labelStyle = new GUIStyle { fontSize = fontSize, normal = { textColor = Color.white } };
        titleStyle = new GUIStyle(labelStyle) { fontStyle = FontStyle.Bold };
    }

    void OnDisable()
    {
        scriptsTime.Dispose();
        physicsTime.Dispose();
        renderTime.Dispose();
        animationTime.Dispose();
        audioTime.Dispose();
        uiTime.Dispose();
        gcAllocFrame.Dispose();

        drawCalls.Dispose();
        batches.Dispose();
        triangles.Dispose();
        vertices.Dispose();
    }

    void Update()
    {
        timeSinceUpdate += Time.unscaledDeltaTime;

        // FPS lissé (EMA simple)
        float instant = 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-6f);
        fpsSmoothed = Mathf.Lerp(fpsSmoothed, instant, 0.1f);

        if (timeSinceUpdate >= refreshRate)
        {
            timeSinceUpdate = 0f;

            cpuMs_Scripts = NsToMs(LastValueSafe(scriptsTime));
            cpuMs_Physics = NsToMs(LastValueSafe(physicsTime));
            cpuMs_Render = NsToMs(LastValueSafe(renderTime));
            cpuMs_Animation = NsToMs(LastValueSafe(animationTime));
            cpuMs_Audio = NsToMs(LastValueSafe(audioTime));
            cpuMs_UI = NsToMs(LastValueSafe(uiTime));

            gcKbPerFrame = LastValueSafe(gcAllocFrame) / 1024f;

            drawCallsCount = LastValueSafe(drawCalls);
            batchesCount = LastValueSafe(batches);
            trisCount = LastValueSafe(triangles);
            vertsCount = LastValueSafe(vertices);

            // GPU frame time (ms) via FrameTimingManager (si dispo)
            gpuMs = SampleGpuMs();
        }
    }

    void OnGUI()
    {
        // On part de l'origine définie, placée plus bas pour éviter le texte du FPS
        float x = origin.x;
        float y = origin.y;
        float line = labelStyle.lineHeight + 2f;
        float boxW = 380f;
        float boxH = 12 * line + 10f;

        if (showBackground)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Box(new Rect(x - 6, y - 6, boxW, boxH), GUIContent.none);
            GUI.color = Color.white;
        }

        //GUI.Label(new Rect(x, y, boxW, line), "PERF HUD", titleStyle); y += line;

        GUI.Label(new Rect(x, y, boxW, line), $"FPS: {fpsSmoothed:F1}"); y += line;

        // CPU per-category
        var catMs = new List<(string, float)> {
            ("Scripts",   cpuMs_Scripts),
            ("Physics",   cpuMs_Physics),
            ("Render( CPU )",    cpuMs_Render),
            ("Animation", cpuMs_Animation),
            ("Audio",     cpuMs_Audio),
            ("UI",        cpuMs_UI),
        };

        // Trouver le plus gros consommateur
        (string topName, float topMs) = TopConsumer(catMs);

        GUI.Label(new Rect(x, y, boxW, line), $"Top CPU: {topName}  {topMs:F2} ms"); y += line;

        // Barres simples (ms) pour chaque catégorie
        float barMax = Mathf.Max(16f, MaxMs(catMs)); // échelle min 16ms (~60fps)
        foreach (var (name, ms) in catMs)
        {
            DrawBar(ref y, name, ms, barMax, x, boxW, line);
        }

        y += 4f;

        // GPU / Rendu
        GUI.Label(new Rect(x, y, boxW, line), $"GPU frame: {(gpuMs > 0f ? gpuMs.ToString("F2") : "n/a")} ms"); y += line;
        if (drawCallsCount > 0) { GUI.Label(new Rect(x, y, boxW, line), $"Draw Calls: {drawCallsCount}    Batches: {batchesCount}"); y += line; }
        if (trisCount > 0) { GUI.Label(new Rect(x, y, boxW, line), $"Triangles: {FormatBig(trisCount)}    Vertices: {FormatBig(vertsCount)}"); y += line; }

        // Mémoire/GC
        GUI.Label(new Rect(x, y, boxW, line), $"GC/frame: {gcKbPerFrame:F1} KB"); y += line;

        // Conseils succincts si FPS bas
        if (fpsSmoothed < 25f)
        {
            y += 4f;
            GUI.Label(new Rect(x, y, boxW, line), "Tips:", titleStyle); y += line;
            GUI.Label(new Rect(x, y, boxW, line), " Si CPU bound: réduire Scripts/Physics/UI (moins dUpdate, pooling, moins devents)."); y += line;
            GUI.Label(new Rect(x, y, boxW, line), " Si Render/GPU haut: baisser post-process, ombres, LOD, rés., lumières dynamiques."); y += line;
            GUI.Label(new Rect(x, y, boxW, line), " GC élevé: éviter new() en Update, String.Format, LINQ allocs, etc."); y += line;
        }
    }

    // ---------- Helpers ----------

    static void TryStart(out ProfilerRecorder rec, ProfilerCategory cat, string statName, int cap = 15)
    {
        try { rec = ProfilerRecorder.StartNew(cat, statName, cap); }
        catch { rec = default; }
    }

    static long LastValueSafe(ProfilerRecorder rec)
    {
        if (!rec.Valid) return 0;
        return rec.LastValue;
    }

    static float NsToMs(long ns) => ns > 0 ? ns / 1_000_000f : 0f;

    static (string, float) TopConsumer(List<(string name, float ms)> list)
    {
        string best = "";
        float bestMs = 0f;
        foreach (var (n, v) in list)
            if (v > bestMs) { bestMs = v; best = n; }
        return (best, bestMs);
    }

    static float MaxMs(List<(string name, float ms)> list)
    {
        float m = 0f;
        foreach (var (_, v) in list) if (v > m) m = v;
        return m;
    }

    static string FormatBig(long v)
    {
        if (v >= 1_000_000_000) return (v / 1_000_000_000f).ToString("0.#") + "B";
        if (v >= 1_000_000) return (v / 1_000_000f).ToString("0.#") + "M";
        if (v >= 1_000) return (v / 1_000f).ToString("0.#") + "K";
        return v.ToString();
    }

    static float SampleGpuMs()
    {
        // Note: nécessite "Enable Frame Timing Stats" (Project Settings > Player) pour de meilleurs résultats
        FrameTimingManager.CaptureFrameTimings();
        FrameTiming[] ft = new FrameTiming[1];
        uint count = FrameTimingManager.GetLatestTimings(1, ft);
        if (count > 0)
        {
            // gpuFrameTime est exprimé en ms directement
            return (float)ft[0].gpuFrameTime;
        }
        return -1f;
    }

    void DrawBar(ref float y, string name, float ms, float max, float x, float width, float line)
    {
        float barW = Mathf.Clamp01(ms / Mathf.Max(0.001f, max)) * (width - 120f);
        // Fond
        Color prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        GUI.Box(new Rect(x, y + 2f, width - 10f, line - 4f), GUIContent.none);
        GUI.color = prev;

        // Barre
        GUI.color = new Color(1f, 1f, 1f, 0.6f);
        GUI.Box(new Rect(x + 115f, y + 3f, barW, line - 6f), GUIContent.none);
        GUI.color = prev;

        GUI.Label(new Rect(x, y, 110f, line), $"{name}:", labelStyle);
        GUI.Label(new Rect(x + 115f + barW + 6f, y, 90f, line), $"{ms:F2} ms", labelStyle);
        y += line;
    }
}
