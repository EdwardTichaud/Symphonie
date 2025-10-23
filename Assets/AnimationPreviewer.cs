// Assets/Editor/AnimationPreviewer.cs
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;
#endif

[ExecuteAlways]
public class AnimationPreviewer : MonoBehaviour
{
    [Header("Cible & Clips")]
    public Animator animator;          // Optionnel : si null, on cible ce GameObject (doit avoir un Animator)
    [Tooltip("Clip appliqué sur le corps (masqué par 'BodyMask')")]
    public AnimationClip bodyClip;
    [Tooltip("Clip appliqué sur le visage (masqué par 'FaceMask')")]
    public AnimationClip faceClip;

    [Header("Avatar Masks")]
    [Tooltip("Masque pour le layer 0 (corps)")]
    public AvatarMask bodyMask;
    [Tooltip("Masque pour le layer 1 (visage)")]
    public AvatarMask faceMask;

    [Header("Lecture")]
    [Range(0.1f, 3f)] public float speed = 1f;
    public bool loop = true;

    [HideInInspector] public float previewTime = 0f; // temps courant (s)
}

#if UNITY_EDITOR
// -------- HUB GLOBAL (lecture persistante, même si on change de sélection) --------
[InitializeOnLoad]
public static class AnimationPreviewHub
{
    public class Session
    {
        public System.Guid id;
        public GameObject go;

        public AnimationClip bodyClip;
        public AnimationClip faceClip;

        public AvatarMask bodyMask;
        public AvatarMask faceMask;

        public float speed;
        public bool loop;
        public float time;

        public double startEditorTime;
        public float startOffset;
        public bool followComponent;     // si true, on lit speed/loop depuis le composant quand il existe
        public AnimationPreviewer source;// composant (peut devenir null si objet détruit ou scène fermée)

        // Playables
        public PlayableGraph graph;
        public AnimationLayerMixerPlayable mixer;
        public AnimationClipPlayable bodyPlayable;
        public AnimationClipPlayable facePlayable;
        public bool graphValid;
    }

    static readonly List<Session> sessions = new List<Session>();
    static bool updateHooked = false;

    static AnimationPreviewHub()
    {
        EnsureHook();
        EditorApplication.quitting += StopAll;
        AssemblyReloadEvents.beforeAssemblyReload += StopAll;
    }

    public static System.Guid StartDetached(AnimationPreviewer comp, bool followComponent)
    {
        if (comp == null) return System.Guid.Empty;
        if (comp.bodyClip == null && comp.faceClip == null) return System.Guid.Empty;

        var go = (comp.animator != null ? comp.animator.gameObject : comp.gameObject);
        if (go == null) return System.Guid.Empty;
        if (go.GetComponent<Animator>() == null)
        {
            Debug.LogWarning("[AnimationPreviewer] Aucun Animator sur la cible — impossible de créer un PlayableGraph.");
            return System.Guid.Empty;
        }

        EnsureHook();

        var s = new Session
        {
            id = System.Guid.NewGuid(),
            go = go,
            bodyClip = comp.bodyClip,
            faceClip = comp.faceClip,
            bodyMask = comp.bodyMask,
            faceMask = comp.faceMask,
            speed = comp.speed,
            loop  = comp.loop,
            time  = Mathf.Max(0f, comp.previewTime),
            startEditorTime = EditorApplication.timeSinceStartup,
            startOffset = Mathf.Max(0f, comp.previewTime),
            followComponent = followComponent,
            source = comp,
            graphValid = false
        };

        if (!BuildGraph(s))
        {
            Debug.LogWarning("[AnimationPreviewer] Échec de création du graph.");
            return System.Guid.Empty;
        }

        sessions.Add(s);
        SampleSessionTime(s, s.time); // Échantillonner tout de suite
        return s.id;
    }

    public static void Stop(System.Guid id)
    {
        for (int i = sessions.Count - 1; i >= 0; i--)
        {
            if (sessions[i].id == id)
            {
                DestroyGraph(sessions[i]);
                sessions.RemoveAt(i);
            }
        }
        CleanupIfEmpty();
    }

    public static void StopAll()
    {
        foreach (var s in sessions) DestroyGraph(s);
        sessions.Clear();
        CleanupIfEmpty();
    }

    static void EnsureHook()
    {
        if (updateHooked) return;
        EditorApplication.update += Update;
        updateHooked = true;
    }

    static void CleanupIfEmpty()
    {
        if (sessions.Count == 0)
        {
            // Rien de spécial à fermer côté AnimationMode (on n'utilise plus SampleAnimationClip)
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }

    static void Update()
    {
        if (sessions.Count == 0) return;

        double now = EditorApplication.timeSinceStartup;

        for (int i = sessions.Count - 1; i >= 0; i--)
        {
            var s = sessions[i];

            // Nettoyage si cible n’existe plus
            if (s.go == null)
            {
                DestroyGraph(s);
                sessions.RemoveAt(i);
                continue;
            }

            // Si on suit le composant et qu’il existe encore, récupérer speed/loop/time en direct
            if (s.followComponent && s.source != null)
            {
                s.speed = Mathf.Max(0.01f, s.source.speed);
                s.loop  = s.source.loop;
            }

            // Temps global
            double elapsed = (now - s.startEditorTime) * s.speed;
            float t = s.startOffset + (float)elapsed;

            // On ne boucle pas "globalement" : on boucle par clip (chacun peut avoir une longueur différente).
            s.time = t;
            SampleSessionTime(s, s.time);

            // Stop auto si non-loop et toutes les sources ont atteint la fin
            if (!s.loop && ReachedEnd(s))
            {
                DestroyGraph(s);
                sessions.RemoveAt(i);
            }
        }

        if (sessions.Count == 0)
            CleanupIfEmpty();
    }

    static bool BuildGraph(Session s)
    {
        try
        {
            var animator = s.go.GetComponent<Animator>();
            if (animator == null) return false;

            s.graph = PlayableGraph.Create($"AnimationPreviewHub_{s.id}");
            s.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            var output = AnimationPlayableOutput.Create(s.graph, "AnimationPreviewOutput", animator);

            // Cas 1/ clip unique
            if (s.faceClip == null || s.faceClip.length <= 0f)
            {
                s.bodyPlayable = AnimationClipPlayable.Create(s.graph, s.bodyClip);
                s.bodyPlayable.SetApplyFootIK(false);
                s.bodyPlayable.SetApplyPlayableIK(false);

                s.mixer = AnimationLayerMixerPlayable.Create(s.graph, 1);
                s.graph.Connect(s.bodyPlayable, 0, s.mixer, 0);
                s.mixer.SetInputWeight(0, 1f);
                if (s.bodyMask != null) s.mixer.SetLayerMaskFromAvatarMask((uint)0, s.bodyMask);

                output.SetSourcePlayable(s.mixer);
            }
            // Cas 2/ deux clips (Body + Face)
            else
            {
                // Body peut être null, on gère aussi
                if (s.bodyClip != null && s.bodyClip.length > 0f)
                {
                    s.bodyPlayable = AnimationClipPlayable.Create(s.graph, s.bodyClip);
                    s.bodyPlayable.SetApplyFootIK(false);
                    s.bodyPlayable.SetApplyPlayableIK(false);
                }

                s.facePlayable = AnimationClipPlayable.Create(s.graph, s.faceClip);
                s.facePlayable.SetApplyFootIK(false);
                s.facePlayable.SetApplyPlayableIK(false);

                int inputCount = (s.bodyPlayable.IsValid() ? 1 : 0) + 1; // + face

                s.mixer = AnimationLayerMixerPlayable.Create(s.graph, Mathf.Max(2, inputCount));
                int slot = 0;
                if (s.bodyPlayable.IsValid())
                {
                    s.graph.Connect(s.bodyPlayable, 0, s.mixer, slot);
                    s.mixer.SetInputWeight(slot, 1f);
                    if (s.bodyMask != null) s.mixer.SetLayerMaskFromAvatarMask((uint)slot, s.bodyMask);
                    slot++;
                }

                s.graph.Connect(s.facePlayable, 0, s.mixer, slot);
                s.mixer.SetInputWeight(slot, 1f);
                if (s.faceMask != null) s.mixer.SetLayerMaskFromAvatarMask((uint)slot, s.faceMask);

                output.SetSourcePlayable(s.mixer);
            }

            s.graph.Play(); // on jouera manuellement via Evaluate(0)
            s.graphValid = true;
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            s.graphValid = false;
            return false;
        }
    }

    static void DestroyGraph(Session s)
    {
        if (s.graphValid && s.graph.IsValid())
        {
            try { s.graph.Destroy(); }
            catch { /* ignore */ }
        }
        s.graphValid = false;
    }

    static void SampleSessionTime(Session s, float globalT)
    {
        if (!s.graphValid) return;

        // Définir les temps par playable (bouclage par clip si loop = true)
        if (s.bodyPlayable.IsValid())
        {
            float len = Mathf.Max(0f, s.bodyClip != null ? s.bodyClip.length : 0f);
            double t = (len > 0f)
                ? (s.loop ? Mathf.Repeat(globalT, len) : Mathf.Min(globalT, len))
                : globalT;
            s.bodyPlayable.SetTime(t);
        }

        if (s.facePlayable.IsValid())
        {
            float len = Mathf.Max(0f, s.faceClip != null ? s.faceClip.length : 0f);
            double t = (len > 0f)
                ? (s.loop ? Mathf.Repeat(globalT, len) : Mathf.Min(globalT, len))
                : globalT;
            s.facePlayable.SetTime(t);
        }

        // Évalue sans avancer (0 delta) : on pousse la pose
        s.graph.Evaluate(0f);

        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();

        // si on suit le composant, pousser time dans l’Inspector pour le scrub visuel
        if (s.followComponent && s.source != null)
        {
            s.source.previewTime = globalT;
            EditorUtility.SetDirty(s.source);
        }
    }

    static bool ReachedEnd(Session s)
    {
        bool bodyEnded = true, faceEnded = true;

        if (s.bodyPlayable.IsValid() && s.bodyClip != null && s.bodyClip.length > 0f)
            bodyEnded = s.time >= s.bodyClip.length - 1e-4f;

        if (s.facePlayable.IsValid() && s.faceClip != null && s.faceClip.length > 0f)
            faceEnded = s.time >= s.faceClip.length - 1e-4f;

        // si un seul clip, on respecte sa fin
        if (s.bodyPlayable.IsValid() && !s.facePlayable.IsValid()) return bodyEnded;
        if (!s.bodyPlayable.IsValid() && s.facePlayable.IsValid()) return faceEnded;

        // sinon, fin quand les deux ont atteint la fin
        return bodyEnded && faceEnded;
    }
}

// -------- INSPECTOR PERSONNALISÉ --------
[CustomEditor(typeof(AnimationPreviewer))]
public class AnimationPreviewerEditor : Editor
{
    AnimationPreviewer P;
    bool isLocalPlaying;
    double startTimeEditor;
    float startOffset;

    // ID de la session détachée si active pour ce composant
    System.Guid detachedId = System.Guid.Empty;

    // Local graph pour l’aperçu attaché
    PlayableGraph localGraph;
    AnimationLayerMixerPlayable localMixer;
    AnimationClipPlayable localBodyPlayable;
    AnimationClipPlayable localFacePlayable;
    bool localGraphValid;

    void OnEnable()
    {
        P = (AnimationPreviewer)target;
        EditorApplication.update += OnEditorUpdate;
        localGraphValid = false;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;

        if (isLocalPlaying) StopLocal(resetPose:false);
        DestroyLocalGraph();

        // les sessions détachées continuent de vivre (comportement voulu)
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("animator"));
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Clips", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bodyClip"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("faceClip"));

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Avatar Masks", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bodyMask"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("faceMask"));

        using (new EditorGUI.DisabledGroupScope(P.bodyClip == null && P.faceClip == null))
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("speed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("loop"));

            float maxLen = Mathf.Max(
                P.bodyClip != null ? P.bodyClip.length : 0f,
                P.faceClip != null ? P.faceClip.length : 0f,
                0.01f
            );

            float newTime = EditorGUILayout.Slider("Temps (s)", P.previewTime, 0f, maxLen);
            if (!isLocalPlaying && detachedId == System.Guid.Empty && Mathf.Abs(newTime - P.previewTime) > 0.0001f)
            {
                P.previewTime = Mathf.Clamp(newTime, 0f, maxLen);
                // un simple sample local au temps choisi
                EnsureLocalGraph();
                SampleLocal(P.previewTime);
            }

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                // Lecture locale (s’arrête si on change de sélection)
                if (!isLocalPlaying)
                {
                    if (GUILayout.Button("▶️ Aperçu (attaché)"))
                        StartLocal();
                }
                else
                {
                    if (GUILayout.Button("⏸️ Pause"))
                        PauseLocal();

                    if (GUILayout.Button("⏹️ Stop"))
                        StopLocal(resetPose: true);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                // Lecture détachée (persiste au changement de sélection)
                bool hasDetached = detachedId != System.Guid.Empty;
                if (!hasDetached)
                {
                    if (GUILayout.Button("🎬 Lecture détachée (persiste)"))
                        detachedId = AnimationPreviewHub.StartDetached(P, followComponent: true);
                }
                else
                {
                    if (GUILayout.Button("⏹️ Stop détachée"))
                    {
                        AnimationPreviewHub.Stop(detachedId);
                        detachedId = System.Guid.Empty;
                    }
                }

                if (GUILayout.Button("🧹 Stop TOUT (Hub)"))
                {
                    AnimationPreviewHub.StopAll();
                    detachedId = System.Guid.Empty;
                }
            }
        }

        EditorGUILayout.HelpBox(
            "Deux layers sont lus en parallèle via un AnimationLayerMixerPlayable :\n" +
            "• Layer 0: BodyClip (maské par BodyMask)\n" +
            "• Layer 1: FaceClip (maské par FaceMask)\n" +
            "Astuce : si un clip est nul, l’autre est lu seul.", MessageType.Info);

        if ((P.animator == null ? P.gameObject.GetComponent<Animator>() : P.animator) == null)
        {
            EditorGUILayout.HelpBox("Aucun Animator détecté sur la cible. Ajoute un Animator pour que l’aperçu fonctionne.", MessageType.Warning);
        }

        serializedObject.ApplyModifiedProperties();
    }

    void OnEditorUpdate()
    {
        if (!isLocalPlaying || P == null) return;
        if (P.bodyClip == null && P.faceClip == null) return;

        double elapsed = (EditorApplication.timeSinceStartup - startTimeEditor) * Mathf.Max(0.01f, P.speed);
        float t = startOffset + (float)elapsed;

        P.previewTime = t;
        SampleLocal(P.previewTime);

        // Stop auto si non loop et fin atteinte
        if (!P.loop && LocalReachedEnd(t))
            PauseLocal();
    }

    // ----- Graph local (attaché à cet Inspector) -----
    void EnsureLocalGraph()
    {
        if (localGraphValid) return;

        var go = (P.animator != null ? P.animator.gameObject : P.gameObject);
        if (go == null) return;
        var anim = go.GetComponent<Animator>();
        if (anim == null)
        {
            Debug.LogWarning("[AnimationPreviewer] Aucun Animator sur la cible — impossible de créer un PlayableGraph (local).");
            return;
        }

        localGraph = PlayableGraph.Create("AnimationPreviewer_Local");
        localGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var output = AnimationPlayableOutput.Create(localGraph, "LocalOutput", anim);

        if (P.faceClip == null || P.faceClip.length <= 0f)
        {
            if (P.bodyClip == null || P.bodyClip.length <= 0f)
            {
                localGraphValid = false;
                return;
            }

            localBodyPlayable = AnimationClipPlayable.Create(localGraph, P.bodyClip);
            localBodyPlayable.SetApplyFootIK(false);
            localBodyPlayable.SetApplyPlayableIK(false);

            localMixer = AnimationLayerMixerPlayable.Create(localGraph, 1);
            localGraph.Connect(localBodyPlayable, 0, localMixer, 0);
            localMixer.SetInputWeight(0, 1f);
            if (P.bodyMask != null) localMixer.SetLayerMaskFromAvatarMask((uint)0, P.bodyMask);

            output.SetSourcePlayable(localMixer);
        }
        else
        {
            if (P.bodyClip != null && P.bodyClip.length > 0f)
            {
                localBodyPlayable = AnimationClipPlayable.Create(localGraph, P.bodyClip);
                localBodyPlayable.SetApplyFootIK(false);
                localBodyPlayable.SetApplyPlayableIK(false);
            }

            localFacePlayable = AnimationClipPlayable.Create(localGraph, P.faceClip);
            localFacePlayable.SetApplyFootIK(false);
            localFacePlayable.SetApplyPlayableIK(false);

            int inputs = (localBodyPlayable.IsValid() ? 1 : 0) + 1;
            localMixer = AnimationLayerMixerPlayable.Create(localGraph, Mathf.Max(2, inputs));

            int slot = 0;
            if (localBodyPlayable.IsValid())
            {
                localGraph.Connect(localBodyPlayable, 0, localMixer, slot);
                localMixer.SetInputWeight(slot, 1f);
                if (P.bodyMask != null) localMixer.SetLayerMaskFromAvatarMask((uint)slot, P.bodyMask);
                slot++;
            }

            localGraph.Connect(localFacePlayable, 0, localMixer, slot);
            localMixer.SetInputWeight(slot, 1f);
            if (P.faceMask != null) localMixer.SetLayerMaskFromAvatarMask((uint)slot, P.faceMask);

            output.SetSourcePlayable(localMixer);
        }

        localGraph.Play();
        localGraphValid = true;
    }

    void DestroyLocalGraph()
    {
        if (localGraphValid && localGraph.IsValid())
        {
            try { localGraph.Destroy(); } catch { /* ignore */ }
        }
        localGraphValid = false;
    }

    void StartLocal()
    {
        if (P.bodyClip == null && P.faceClip == null) return;

        EnsureLocalGraph();
        if (!localGraphValid) return;

        startTimeEditor = EditorApplication.timeSinceStartup;

        float maxLen = Mathf.Max(
            P.bodyClip != null ? P.bodyClip.length : 0f,
            P.faceClip != null ? P.faceClip.length : 0f,
            0f
        );

        startOffset = Mathf.Clamp(P.previewTime, 0f, Mathf.Max(0.0001f, maxLen));
        isLocalPlaying = true;

        SampleLocal(P.previewTime);
    }

    void PauseLocal()  { isLocalPlaying = false; }

    void StopLocal(bool resetPose)
    {
        isLocalPlaying = false;
        if (resetPose)
        {
            // Pour "remettre au repos", on détruit le graph (plus sûr en edit)
            DestroyLocalGraph();
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }

    bool LocalReachedEnd(float globalT)
    {
        bool bodyEnded = true, faceEnded = true;

        if (localBodyPlayable.IsValid() && P.bodyClip != null && P.bodyClip.length > 0f)
            bodyEnded = globalT >= P.bodyClip.length - 1e-4f;

        if (localFacePlayable.IsValid() && P.faceClip != null && P.faceClip.length > 0f)
            faceEnded = globalT >= P.faceClip.length - 1e-4f;

        if (localBodyPlayable.IsValid() && !localFacePlayable.IsValid()) return bodyEnded;
        if (!localBodyPlayable.IsValid() && localFacePlayable.IsValid()) return faceEnded;

        return bodyEnded && faceEnded;
    }

    void SampleLocal(float globalT)
    {
        EnsureLocalGraph();
        if (!localGraphValid) return;

        if (localBodyPlayable.IsValid() && P.bodyClip != null)
        {
            float len = Mathf.Max(0f, P.bodyClip.length);
            double t = (len > 0f)
                ? (P.loop ? Mathf.Repeat(globalT, len) : Mathf.Min(globalT, len))
                : globalT;
            localBodyPlayable.SetTime(t);
        }

        if (localFacePlayable.IsValid() && P.faceClip != null)
        {
            float len = Mathf.Max(0f, P.faceClip.length);
            double t = (len > 0f)
                ? (P.loop ? Mathf.Repeat(globalT, len) : Mathf.Min(globalT, len))
                : globalT;
            localFacePlayable.SetTime(t);
        }

        localGraph.Evaluate(0f);

        SceneView.RepaintAll();
        EditorUtility.SetDirty(P);
        EditorApplication.QueuePlayerLoopUpdate();
    }
}
#endif
