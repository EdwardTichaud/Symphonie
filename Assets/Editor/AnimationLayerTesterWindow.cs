using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Fenêtre utilitaire pour tester rapidement des états d'animation hors Play Mode
/// en synchronisant toutes les couches partageant le même nom d'état.
/// </summary>
public class AnimationLayerTesterWindow : EditorWindow
{
    /// <summary>
    /// Représente un état trouvé dans un layer précis (uniquement les states Motion -> AnimationClip).
    /// </summary>
    private struct LayerState
    {
        public LayerState(string layerName, AnimationClip clip)
        {
            LayerName = layerName;
            Clip = clip;
        }

        public string LayerName { get; private set; }
        public AnimationClip Clip { get; private set; }
    }

    // ------------------------- Données de configuration -------------------------
    [SerializeField] private Animator _targetAnimator;      // Animator ciblé dans la scène
    [SerializeField] private string _stateName = "Idle_World"; // Nom d'état recherché dans tous les layers
    [SerializeField] private float _playbackSpeed = 1f;     // Vitesse de lecture (1 = vitesse réelle)
    [SerializeField] private bool _loop = true;             // Relancer la lecture automatiquement ?
    [SerializeField] private float _normalizedTime = 0f;    // Position courante normalisée (0-1)

    // ------------------------- État interne -------------------------
    private readonly List<LayerState> _matches = new List<LayerState>(); // Cache des états trouvés
    private bool _isPlaying;                                // Flag de lecture en cours ?
    private double _playStartEditorTime;                    // Temps de départ dans l'éditeur
    private float _playStartNormalized;                     // Position de départ pour la lecture

    // Permet de garder l'AnimationMode ouvert pendant la lecture pour éviter les resets
    private bool _animationModeOwner;

    [MenuItem("Symphonie/Outils Animation/Testeur de layers", priority = 50)]
    private static void OpenWindow()
    {
        AnimationLayerTesterWindow window = GetWindow<AnimationLayerTesterWindow>();
        window.titleContent = new GUIContent("Testeur d'animations");
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate; // Tick régulier pour la lecture
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        StopPlayback(true); // Toujours s'assurer de libérer AnimationMode
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Cible", EditorStyles.boldLabel);
            using (EditorGUI.ChangeCheckScope change = new EditorGUI.ChangeCheckScope())
            {
                _targetAnimator = (Animator)EditorGUILayout.ObjectField(
                    new GUIContent("Animator", "Animator présent dans la scène à tester"),
                    _targetAnimator,
                    typeof(Animator),
                    true);

                _stateName = EditorGUILayout.TextField(
                    new GUIContent("Nom d'état", "Nom exact de l'état recherché dans chaque layer"),
                    _stateName);

                _playbackSpeed = EditorGUILayout.FloatField(
                    new GUIContent("Vitesse", "Facteur de vitesse de lecture (1 = vitesse originale)"),
                    _playbackSpeed);

                _loop = EditorGUILayout.Toggle(
                    new GUIContent("Boucler", "Relancer automatiquement quand la fin est atteinte"),
                    _loop);

                _normalizedTime = EditorGUILayout.Slider(
                    new GUIContent("Position (0-1)", "Position normalisée dans le cycle d'animation"),
                    _normalizedTime,
                    0f,
                    1f);

                if (change.changed)
                {
                    // Dès que quelque chose change on recalcule les correspondances pour rester cohérent.
                    RefreshMatches();
                    if (!_isPlaying)
                    {
                        SampleAtNormalizedTime(_normalizedTime);
                    }
                }
            }
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = _targetAnimator != null && !string.IsNullOrEmpty(_stateName);
            if (!_isPlaying)
            {
                if (GUILayout.Button("▶️ Lancer"))
                {
                    StartPlayback();
                }
            }
            else
            {
                if (GUILayout.Button("⏸️ Pause"))
                {
                    PausePlayback();
                }

                if (GUILayout.Button("⏹️ Stop"))
                {
                    StopPlayback(true);
                }
            }
            GUI.enabled = true;
        }

        EditorGUILayout.Space();

        DrawMatchesList();

        EditorGUILayout.HelpBox(
            "Saisis le nom d'un état présent dans ton Animator. Toutes les couches " +
            "contenant un état Motion -> AnimationClip avec ce nom seront évaluées ensemble. " +
            "La lecture utilise AnimationMode, ce qui permet de tester hors Play Mode.",
            MessageType.Info);
    }

    /// <summary>
    /// Affiche la liste des correspondances trouvées pour aider au debug.
    /// </summary>
    private void DrawMatchesList()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Layers affectés", EditorStyles.boldLabel);

            if (_targetAnimator == null)
            {
                EditorGUILayout.HelpBox("Aucun Animator sélectionné.", MessageType.Warning);
                return;
            }

            if (_matches.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Aucun état ne correspond au nom indiqué. Vérifie l'orthographe et que " +
                    "chaque state cible contient directement un AnimationClip.",
                    MessageType.Info);
                return;
            }

            foreach (LayerState match in _matches)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(match.LayerName, GUILayout.Width(160f));
                    EditorGUILayout.ObjectField(match.Clip, typeof(AnimationClip), false);
                }
            }
        }
    }

    /// <summary>
    /// Démarre la lecture et mémorise les informations nécessaires.
    /// </summary>
    private void StartPlayback()
    {
        RefreshMatches();
        if (_matches.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Pas de correspondances",
                "Impossible de trouver un état avec ce nom dans les layers de l'Animator.",
                "Compris");
            return;
        }

        _playStartEditorTime = EditorApplication.timeSinceStartup;
        _playStartNormalized = _normalizedTime;
        _isPlaying = true;

        EnsureAnimationMode();
        SampleAtNormalizedTime(_normalizedTime);
    }

    /// <summary>
    /// Met la lecture en pause mais conserve la pose actuelle.
    /// </summary>
    private void PausePlayback()
    {
        _isPlaying = false;
        // On conserve AnimationMode ouvert pour garder la pose dans la scène.
    }

    /// <summary>
    /// Arrête complètement la lecture et optionnellement réinitialise la pose.
    /// </summary>
    private void StopPlayback(bool resetPose)
    {
        _isPlaying = false;
        if (resetPose && _animationModeOwner)
        {
            AnimationMode.StopAnimationMode();
            _animationModeOwner = false;
        }
    }

    /// <summary>
    /// Rafraîchit la liste des états correspondant au nom demandé.
    /// </summary>
    private void RefreshMatches()
    {
        _matches.Clear();
        if (_targetAnimator == null)
        {
            return;
        }

        AnimatorController controller = _targetAnimator.runtimeAnimatorController as AnimatorController;
        if (controller == null)
        {
            Debug.LogWarning("Le runtimeAnimatorController ciblé n'est pas un AnimatorController.");
            return;
        }

        for (int i = 0; i < controller.layers.Length; i++)
        {
            AnimatorControllerLayer layer = controller.layers[i];
            AnimatorState state;
            if (TryFindState(layer.stateMachine, _stateName, out state))
            {
                AnimationClip clip = state.motion as AnimationClip;
                if (clip != null)
                {
                    _matches.Add(new LayerState(layer.name, clip));
                }
                else
                {
                    Debug.LogWarning(string.Format("L'état '{0}' dans le layer '{1}' n'est pas lié à un AnimationClip direct (BlendTree non géré).", _stateName, layer.name));
                }
            }
        }
    }

    /// <summary>
    /// Recherche récursive d'un état dans un state machine donné.
    /// </summary>
    private static bool TryFindState(AnimatorStateMachine machine, string name, out AnimatorState state)
    {
        // Parcourt d'abord les states simples.
        ChildAnimatorState[] states = machine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state != null && states[i].state.name == name)
            {
                state = states[i].state;
                return true;
            }
        }

        // Puis on descend dans les sous state machines.
        ChildAnimatorStateMachine[] stateMachines = machine.stateMachines;
        for (int i = 0; i < stateMachines.Length; i++)
        {
            if (TryFindState(stateMachines[i].stateMachine, name, out state))
            {
                return true;
            }
        }

        state = null;
        return false;
    }

    /// <summary>
    /// Assure que l'AnimationMode est actif pour pouvoir sampler des clips hors Play Mode.
    /// </summary>
    private void EnsureAnimationMode()
    {
        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
            _animationModeOwner = true;
        }
    }

    /// <summary>
    /// Applique les clips correspondant à la position normalisée demandée.
    /// </summary>
    private void SampleAtNormalizedTime(float normalizedTime)
    {
        if (_targetAnimator == null || _matches.Count == 0)
        {
            return;
        }

        EnsureAnimationMode();

        GameObject go = _targetAnimator.gameObject;
        AnimationMode.BeginSampling();
        foreach (LayerState match in _matches)
        {
            AnimationClip clip = match.Clip;
            if (clip == null)
            {
                continue;
            }

            float clipLength = Mathf.Max(clip.length, 0.0001f); // On évite les divisions par zéro.
            float localTime = Mathf.Clamp01(normalizedTime) * clipLength;
            AnimationMode.SampleAnimationClip(go, clip, localTime);
        }
        AnimationMode.EndSampling();

        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }

    /// <summary>
    /// Tick éditor utilisé uniquement pendant la lecture.
    /// </summary>
    private void OnEditorUpdate()
    {
        if (!_isPlaying || _matches.Count == 0)
        {
            return;
        }

        AnimationClip referenceClip = _matches[0].Clip;
        if (referenceClip == null)
        {
            return;
        }

        float referenceLength = Mathf.Max(referenceClip.length, 0.0001f);
        float elapsed = (float)((EditorApplication.timeSinceStartup - _playStartEditorTime) * _playbackSpeed);
        float deltaNormalized = elapsed / referenceLength;

        float newNormalized = _playStartNormalized + deltaNormalized;
        if (_loop)
        {
            newNormalized = Mathf.Repeat(newNormalized, 1f);
        }
        else
        {
            newNormalized = Mathf.Clamp01(newNormalized);
            if (Mathf.Approximately(newNormalized, 1f))
            {
                // Si on atteint la fin en mode non bouclé on stoppe pour éviter de continuer les updates.
                StopPlayback(false);
            }
        }

        _normalizedTime = newNormalized;
        SampleAtNormalizedTime(_normalizedTime);
    }
}
