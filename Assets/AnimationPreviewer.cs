// Assets/Editor/AnimationPreviewer.cs
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;
#endif

[ExecuteAlways]
public class AnimationPreviewer : MonoBehaviour
{
    [Header("Cible & Clip")]
    public Animator animator;          // Optionnel : si null, on cible ce GameObject
    public AnimationClip clip;

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
        public System.Guid id;               // Identifiant unique pour manipuler la session dans le Hub.
        public GameObject go;                // Cible réelle qui reçoit l'échantillonnage de l'animation.
        public AnimationClip clip;           // Clip actuellement joué.
        public float speed;                  // Vitesse de lecture effective.
        public bool loop;                    // Mode boucle actif ?
        public float time;                   // Temps courant d'échantillonnage (en secondes).
        public double startEditorTime;       // Temps Editor au moment de la dernière resynchronisation.
        public float startOffset;            // Offset initial (permet de reprendre depuis previewTime).
        public bool followComponent;         // Si true, on lit dynamiquement les paramètres exposés par le composant.
        public AnimationPreviewer source;    // Référence vers le composant d'origine (peut devenir null si détruit).
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
        if (comp == null || comp.clip == null) return System.Guid.Empty;

        EnsureHook();
        if (!AnimationMode.InAnimationMode())
            AnimationMode.StartAnimationMode();

        var go = comp.animator != null ? comp.animator.gameObject : comp.gameObject;

        var s = new Session
        {
            id = System.Guid.NewGuid(),
            go = go,
            clip = comp.clip,
            speed = comp.speed,
            loop  = comp.loop,
            time  = Mathf.Clamp(comp.previewTime, 0f, comp.clip.length),
            startEditorTime = EditorApplication.timeSinceStartup,
            startOffset = Mathf.Clamp(comp.previewTime, 0f, comp.clip.length),
            followComponent = followComponent,
            source = comp
        };
        sessions.Add(s);

        // Échantillonner tout de suite
        Sample(s, s.time);
        return s.id;
    }

    public static void Stop(System.Guid id)
    {
        sessions.RemoveAll(s => s.id == id);
        CleanupIfEmpty();
    }

    public static void StopAll()
    {
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
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }

    static void Update()
    {
        if (sessions.Count == 0) return;

        double now = EditorApplication.timeSinceStartup;

        // Copie locale pour éviter modif pendant itération
        for (int i = sessions.Count - 1; i >= 0; i--)
        {
            var s = sessions[i];

            // Nettoyage si cible/clip n’existent plus
            if (s.go == null || s.clip == null)
            {
                sessions.RemoveAt(i);
                continue;
            }

            // Si on suit le composant et qu’il existe encore, récupérer speed/loop/time en direct
            bool resyncedFromComponent = false; // Indique si un changement manuel doit être appliqué immédiatement.

            if (s.followComponent && s.source != null)
            {
                // Synchronise la vitesse et la boucle pour refléter instantanément les modifications Inspector.
                s.speed = Mathf.Max(0.01f, s.source.speed);
                s.loop  = s.source.loop;

                // Vérifie si la cible (Animator / GameObject) a changé pendant la lecture.
                GameObject desiredGo = s.source.animator != null ? s.source.animator.gameObject : s.source.gameObject;
                if (desiredGo != s.go)
                {
                    s.go = desiredGo;
                    resyncedFromComponent = true;
                }

                // Autorise le remplacement du clip "à chaud".
                AnimationClip desiredClip = s.source.clip;
                if (desiredClip != s.clip)
                {
                    if (desiredClip == null)
                    {
                        // Si le clip est effacé pendant la lecture, on stoppe proprement la session.
                        sessions.RemoveAt(i);
                        continue;
                    }

                    s.clip = desiredClip;
                    // On recale immédiatement le temps de lecture pour rester dans la durée valide du nouveau clip.
                    s.time = Mathf.Clamp(s.source.previewTime, 0f, s.clip.length);
                    resyncedFromComponent = true;
                }

                // Permet de forcer la position de lecture depuis le champ previewTime.
                float desiredTime = Mathf.Clamp(s.source.previewTime, 0f, s.clip.length);
                if (Mathf.Abs(desiredTime - s.time) > 0.0001f)
                {
                    s.time = desiredTime;
                    resyncedFromComponent = true;
                }

                if (resyncedFromComponent)
                {
                    // On redémarre l'horloge d'Editor pour éviter les sauts lorsque l'utilisateur ajuste les valeurs.
                    s.startEditorTime = now;
                    s.startOffset = s.time;

                    // On pousse immédiatement l'état synchronisé pour que la scène reflète instantanément la modification.
                    Sample(s, s.time);

                    // Comme on vient de ré-échantillonner, on passe à la session suivante pour éviter un double calcul.
                    if (!s.loop && s.time >= s.clip.length - 1e-4f)
                        sessions.RemoveAt(i);
                    continue;
                }
            }

            double elapsed = (now - s.startEditorTime) * s.speed;
            float t = s.startOffset + (float)elapsed;

            if (s.loop && s.clip.length > 0f)
                t = Mathf.Repeat(t, s.clip.length);
            else
                t = Mathf.Min(t, s.clip.length);

            s.time = t;
            Sample(s, s.time);

            // Stop auto si non-loop et fin
            if (!s.loop && s.time >= s.clip.length - 1e-4f)
            {
                sessions.RemoveAt(i);
            }
        }

        if (sessions.Count == 0)
            CleanupIfEmpty();
    }

    static void Sample(Session s, float time)
    {
        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(s.go, s.clip, Mathf.Clamp(time, 0f, s.clip.length));
        AnimationMode.EndSampling();

        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();

        // si on suit le composant, pousser time dans l’Inspector pour le scrub visuel
        if (s.followComponent && s.source != null)
        {
            s.source.previewTime = time;
            EditorUtility.SetDirty(s.source);
        }
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

    void OnEnable()
    {
        P = (AnimationPreviewer)target;
        EditorApplication.update += OnEditorUpdate;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;

        // ⚠️ On NE stoppe PAS les sessions détachées ici (elles doivent survivre au changement de sélection).
        // On stoppe juste la lecture locale si elle était active.
        if (isLocalPlaying) StopLocal(resetPose: false);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("animator"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("clip"));

        using (new EditorGUI.DisabledGroupScope(P.clip == null))
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("speed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("loop"));

            float length = P.clip != null ? P.clip.length : 1f;
            float newTime = EditorGUILayout.Slider("Temps (s)", P.previewTime, 0f, Mathf.Max(0.01f, length));
            if (!isLocalPlaying && detachedId == System.Guid.Empty && Mathf.Abs(newTime - P.previewTime) > 0.0001f)
            {
                P.previewTime = Mathf.Clamp(newTime, 0f, length);
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

        EditorGUILayout.HelpBox("La 'Lecture détachée' continue même si tu changes d’objet sélectionné. Utilise 'Stop détachée' ou 'Stop TOUT' pour arrêter.", MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }

    void OnEditorUpdate()
    {
        if (!isLocalPlaying || P == null || P.clip == null)
            return;

        double elapsed = (EditorApplication.timeSinceStartup - startTimeEditor) * P.speed;
        float t = startOffset + (float)elapsed;

        if (P.loop && P.clip.length > 0f)
            t = Mathf.Repeat(t, P.clip.length);
        else
            t = Mathf.Min(t, P.clip.length);

        P.previewTime = t;
        SampleLocal(P.previewTime);

        if (!P.loop && P.previewTime >= P.clip.length - 1e-4f)
            PauseLocal();
    }

    // ----- Lecture locale (attachée à cet Inspector) -----
    void StartLocal()
    {
        if (P.clip == null) return;

        if (!AnimationMode.InAnimationMode())
            AnimationMode.StartAnimationMode();

        startTimeEditor = EditorApplication.timeSinceStartup;
        startOffset = Mathf.Clamp(P.previewTime, 0f, P.clip.length);
        isLocalPlaying = true;

        SampleLocal(P.previewTime);
    }

    void PauseLocal()  { isLocalPlaying = false; }

    void StopLocal(bool resetPose)
    {
        isLocalPlaying = false;

        // Ne ferme AnimationMode que si aucun Hub ne tourne
        // (le Hub se charge de l’ouvrir/fermer selon ses sessions)
        if (resetPose && AnimationMode.InAnimationMode())
        {
            AnimationMode.StopAnimationMode();
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }

    void SampleLocal(float time)
    {
        if (P.clip == null) return;
        var go = (P.animator != null ? P.animator.gameObject : P.gameObject);

        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(go, P.clip, Mathf.Clamp(time, 0f, P.clip.length));
        AnimationMode.EndSampling();

        SceneView.RepaintAll();
        EditorUtility.SetDirty(P);
        EditorApplication.QueuePlayerLoopUpdate();
    }
}
#endif
