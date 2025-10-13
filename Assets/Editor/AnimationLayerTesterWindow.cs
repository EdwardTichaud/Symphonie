using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Fenêtre utilitaire pour tester rapidement des animations multi-couches et leurs transitions
/// directement dans l'éditeur, sans lancer le Play Mode. Les designers peuvent explorer les
/// différents layers, ajuster leurs poids, manipuler les paramètres du contrôleur et simuler
/// des transitions entre deux états pour valider les blendings.
/// </summary>
public class AnimationLayerTesterWindow : EditorWindow
{
    /// <summary>
    /// Représente un état jouable dans un layer précis (uniquement les states Motion -> AnimationClip).
    /// </summary>
    private class StateCache
    {
        public StateCache(string displayName, AnimatorState state, AnimationClip clip)
        {
            DisplayName = displayName;
            State = state;
            Clip = clip;
        }

        public string DisplayName { get; private set; }
        public AnimatorState State { get; private set; }
        public AnimationClip Clip { get; private set; }
    }

    private class LayerSelection
    {
        public LayerSelection(AnimatorControllerLayer controllerLayer, int layerIndex)
        {
            ControllerLayer = controllerLayer;
            LayerIndex = layerIndex;
            LayerName = controllerLayer.name;
            BlendingMode = controllerLayer.blendingMode;
            States = new List<StateCache>();
            SelectedStateIndex = -1;
            TransitionStateIndex = -1;
            ManualWeight = controllerLayer.defaultWeight;
        }

        public AnimatorControllerLayer ControllerLayer { get; private set; }
        public string LayerName { get; private set; }
        public AnimatorLayerBlendingMode BlendingMode { get; private set; }
        public int LayerIndex { get; private set; }
        public List<StateCache> States { get; private set; }
        public int SelectedStateIndex { get; set; }
        public int TransitionStateIndex { get; set; }
        public bool OverrideWeight { get; set; }
        public float ManualWeight { get; set; }

        public string SelectedStateName
        {
            get
            {
                return SelectedStateIndex >= 0 && SelectedStateIndex < States.Count
                    ? States[SelectedStateIndex].DisplayName
                    : null;
            }
        }

        public string TransitionStateName
        {
            get
            {
                return TransitionStateIndex >= 0 && TransitionStateIndex < States.Count
                    ? States[TransitionStateIndex].DisplayName
                    : null;
            }
        }

        public AnimationClip SelectedClip
        {
            get
            {
                return SelectedStateIndex >= 0 && SelectedStateIndex < States.Count
                    ? States[SelectedStateIndex].Clip
                    : null;
            }
        }

        public AnimationClip TransitionClip
        {
            get
            {
                return TransitionStateIndex >= 0 && TransitionStateIndex < States.Count
                    ? States[TransitionStateIndex].Clip
                    : null;
            }
        }

        public int FindStateIndex(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
            {
                return -1;
            }

            for (int i = 0; i < States.Count; i++)
            {
                if (States[i].DisplayName == displayName)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    private struct LayerSelectionSnapshot
    {
        public string SelectedStateName;
        public string TransitionStateName;
        public bool OverrideWeight;
        public float ManualWeight;
    }

    private struct ParameterValue
    {
        public ParameterValue(AnimatorControllerParameter parameter)
        {
            Type = parameter.type;
            FloatValue = parameter.defaultFloat;
            IntValue = parameter.defaultInt;
            BoolValue = parameter.defaultBool;
            TriggerArmed = false;
        }

        public AnimatorControllerParameterType Type;
        public float FloatValue;
        public int IntValue;
        public bool BoolValue;
        public bool TriggerArmed;
    }

    // ------------------------- Données de configuration -------------------------
    [SerializeField] private Animator _targetAnimator;      // Animator ciblé dans la scène
    [SerializeField] private string _stateName = "Idle_World"; // Nom d'état recherché dans tous les layers (hérité pour rétro-compat)
    [SerializeField] private float _playbackSpeed = 1f;     // Vitesse de lecture (1 = vitesse réelle)
    [SerializeField] private bool _loop = true;             // Relancer la lecture automatiquement ?
    [SerializeField] private float _normalizedTime = 0f;    // Position courante normalisée (0-1)
    [SerializeField] private float _transitionNormalizedTime = 0f; // Position normalisée de la cible pour simuler la transition
    [SerializeField] private float _transitionBlend = 0f;    // Facteur de mélange entre l'état courant et la cible (0 = aucun blend)

    // ------------------------- État interne -------------------------
    private readonly List<LayerSelection> _layerSelections = new List<LayerSelection>(); // Informations détaillées par layer
    private readonly Dictionary<string, ParameterValue> _parameterValues = new Dictionary<string, ParameterValue>(); // Paramètres de l'Animator
    private readonly Dictionary<Transform, PoseSnapshot> _layerSamplePose = new Dictionary<Transform, PoseSnapshot>(); // Pose résultant d'un layer
    private readonly Dictionary<Transform, PoseSnapshot> _transitionSamplePose = new Dictionary<Transform, PoseSnapshot>(); // Pose temporaire utilisée pour les transitions
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

                _transitionNormalizedTime = EditorGUILayout.Slider(
                    new GUIContent(
                        "Position transition (0-1)",
                        "Position de lecture de l'état cible utilisée pour simuler un blend."),
                    _transitionNormalizedTime,
                    0f,
                    1f);

                _transitionBlend = EditorGUILayout.Slider(
                    new GUIContent(
                        "Progression transition",
                        "0 = uniquement l'état principal, 1 = uniquement l'état cible."),
                    _transitionBlend,
                    0f,
                    1f);

                if (change.changed)
                {
                    // Dès que quelque chose change on recalcule les correspondances pour rester cohérent.
                    RefreshControllerData();
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
            GUI.enabled = _targetAnimator != null;
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

        bool needResample = false;
        needResample |= DrawParametersPanel();
        needResample |= DrawLayersPanel();

        if (needResample && !_isPlaying)
        {
            // Dès que l'utilisateur modifie une sélection nous appliquons immédiatement la nouvelle pose.
            SampleAtNormalizedTime(_normalizedTime);
        }

        EditorGUILayout.HelpBox(
            "Sélectionne les états à prévisualiser par couche et ajuste les paramètres de " +
            "l'Animator pour tester rapidement les transitions hors Play Mode. La lecture " +
            "utilise AnimationMode, ce qui permet de rester en mode Édition.",
            MessageType.Info);
    }

    /// <summary>
    /// Affiche les paramètres exposés par l'Animator afin de tester les transitions dépendantes.
    /// </summary>
    private bool DrawParametersPanel()
    {
        bool changed = false;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Paramètres de l'Animator", EditorStyles.boldLabel);

            if (_targetAnimator == null)
            {
                EditorGUILayout.HelpBox(
                    "Aucun Animator sélectionné : impossible d'afficher les paramètres.",
                    MessageType.Info);
                return false;
            }

            AnimatorController controller = _targetAnimator.runtimeAnimatorController as AnimatorController;
            if (controller == null)
            {
                EditorGUILayout.HelpBox(
                    "Le runtimeAnimatorController ciblé n'est pas un AnimatorController.",
                    MessageType.Warning);
                return false;
            }

            if (controller.parameters == null || controller.parameters.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "Aucun paramètre défini dans ce contrôleur.",
                    MessageType.Info);
            }
            else
            {
                foreach (AnimatorControllerParameter parameter in controller.parameters)
                {
                    if (!_parameterValues.TryGetValue(parameter.name, out ParameterValue value))
                    {
                        value = new ParameterValue(parameter);
                        _parameterValues[parameter.name] = value;
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(parameter.name, GUILayout.Width(160f));

                        switch (parameter.type)
                        {
                            case AnimatorControllerParameterType.Float:
                                {
                                    using (EditorGUI.ChangeCheckScope scope = new EditorGUI.ChangeCheckScope())
                                    {
                                        float newValue = EditorGUILayout.FloatField(value.FloatValue);
                                        if (scope.changed)
                                        {
                                            value.FloatValue = newValue;
                                            changed = true;
                                            ApplyParameterToAnimator(parameter.name, value);
                                        }
                                    }
                                }
                                break;
                            case AnimatorControllerParameterType.Int:
                                {
                                    using (EditorGUI.ChangeCheckScope scope = new EditorGUI.ChangeCheckScope())
                                    {
                                        int newValue = EditorGUILayout.IntField(value.IntValue);
                                        if (scope.changed)
                                        {
                                            value.IntValue = newValue;
                                            changed = true;
                                            ApplyParameterToAnimator(parameter.name, value);
                                        }
                                    }
                                }
                                break;
                            case AnimatorControllerParameterType.Bool:
                                {
                                    using (EditorGUI.ChangeCheckScope scope = new EditorGUI.ChangeCheckScope())
                                    {
                                        bool newValue = EditorGUILayout.Toggle(value.BoolValue);
                                        if (scope.changed)
                                        {
                                            value.BoolValue = newValue;
                                            changed = true;
                                            ApplyParameterToAnimator(parameter.name, value);
                                        }
                                    }
                                }
                                break;
                            case AnimatorControllerParameterType.Trigger:
                                {
                                    if (GUILayout.Button("Déclencher", GUILayout.Width(100f)))
                                    {
                                        value.TriggerArmed = true;
                                        changed = true;
                                        ApplyParameterToAnimator(parameter.name, value);
                                    }

                                    if (GUILayout.Button("Réinitialiser", GUILayout.Width(100f)))
                                    {
                                        value.TriggerArmed = false;
                                        changed = true;
                                        ApplyParameterToAnimator(parameter.name, value);
                                    }
                                }
                                break;
                        }

                        _parameterValues[parameter.name] = value;
                    }
                }
            }
        }

        return changed;
    }

    /// <summary>
    /// Affiche la liste des layers et permet de sélectionner les états à évaluer.
    /// </summary>
    private bool DrawLayersPanel()
    {
        bool changed = false;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Layers affectés", EditorStyles.boldLabel);

            if (_targetAnimator == null)
            {
                EditorGUILayout.HelpBox("Aucun Animator sélectionné.", MessageType.Warning);
                return false;
            }

            if (_layerSelections.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Aucun layer ne contient encore d'état Motion -> AnimationClip utilisable.",
                    MessageType.Info);
                return false;
            }

            foreach (LayerSelection selection in _layerSelections)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        string.Format(
                            "Layer {0} - {1} (mode {2})",
                            selection.LayerIndex,
                            selection.LayerName,
                            selection.BlendingMode),
                        EditorStyles.boldLabel);
                }

                if (selection.States.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "Aucun état animé avec un AnimationClip direct dans ce layer.",
                        MessageType.Warning);
                    continue;
                }

                if (selection.SelectedStateIndex < 0 && selection.States.Count > 0)
                {
                    selection.SelectedStateIndex = 0;
                }

                if (selection.TransitionStateIndex >= selection.States.Count)
                {
                    selection.TransitionStateIndex = -1;
                }

                string[] stateNames = selection.States.Select(s => s.DisplayName).ToArray();

                using (EditorGUI.ChangeCheckScope stateChange = new EditorGUI.ChangeCheckScope())
                {
                    int newIndex = EditorGUILayout.Popup(
                        new GUIContent(
                            "État principal",
                            "Clip qui servira de base pour calculer la pose du layer."),
                        Mathf.Max(0, selection.SelectedStateIndex),
                        stateNames);

                    if (stateChange.changed)
                    {
                        selection.SelectedStateIndex = newIndex;
                        changed = true;
                    }
                }

                using (EditorGUI.ChangeCheckScope transitionChange = new EditorGUI.ChangeCheckScope())
                {
                    string[] transitionOptions = new string[stateNames.Length + 1];
                    transitionOptions[0] = "(Aucune transition)";
                    for (int i = 0; i < stateNames.Length; i++)
                    {
                        transitionOptions[i + 1] = stateNames[i];
                    }

                    int displayIndex = selection.TransitionStateIndex >= 0
                        ? selection.TransitionStateIndex + 1
                        : 0;

                    int newTransitionIndex = EditorGUILayout.Popup(
                        new GUIContent(
                            "État cible",
                            "Sélection facultative d'un second état pour simuler un blend."),
                        displayIndex,
                        transitionOptions);

                    if (transitionChange.changed)
                    {
                        selection.TransitionStateIndex = newTransitionIndex - 1;
                        changed = true;
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    float resolvedWeight = ResolveLayerWeight(selection.ControllerLayer, selection.LayerIndex);

                    using (EditorGUI.ChangeCheckScope overrideScope = new EditorGUI.ChangeCheckScope())
                    {
                        bool newOverride = EditorGUILayout.Toggle(
                            new GUIContent(
                                "Forcer le poids",
                                "Permet de tester un poids différent de celui défini sur l'Animator."),
                            selection.OverrideWeight,
                            GUILayout.Width(130f));

                        if (overrideScope.changed)
                        {
                            selection.OverrideWeight = newOverride;
                            changed = true;
                        }
                    }

                    using (EditorGUI.DisabledScope disabled = new EditorGUI.DisabledScope(!selection.OverrideWeight))
                    {
                        using (EditorGUI.ChangeCheckScope weightScope = new EditorGUI.ChangeCheckScope())
                        {
                            float newWeight = EditorGUILayout.Slider(
                                new GUIContent("Poids manuel", "0 = layer ignoré, 1 = layer à poids plein."),
                                selection.ManualWeight,
                                0f,
                                1f);

                            if (weightScope.changed)
                            {
                                selection.ManualWeight = newWeight;
                                changed = true;
                            }
                        }
                    }

                    EditorGUILayout.LabelField(
                        string.Format(
                            "Poids effectif : {0:0.###}",
                            selection.OverrideWeight ? selection.ManualWeight : resolvedWeight),
                        GUILayout.Width(160f));
                }

                EditorGUILayout.Space(6f);
            }
        }

        return changed;
    }

    /// <summary>
    /// Démarre la lecture et mémorise les informations nécessaires.
    /// </summary>
    private void StartPlayback()
    {
        RefreshControllerData();
        if (!HasPlayableStates())
        {
            EditorUtility.DisplayDialog(
                "Pas de correspondances",
                "Impossible de trouver un état jouable dans les layers de l'Animator.",
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
        if (_targetAnimator == null || !HasPlayableStates())
        {
            return;
        }

        EnsureAnimationMode();

        GameObject go = _targetAnimator.gameObject;

        // On capture une photographie complète de la pose actuelle pour pouvoir revenir
        // à l'état initial entre deux layers. Cela permet de simuler un vrai système
        // de blending en utilisant les poids configurés sur l'Animator.
        Transform[] hierarchy = go.GetComponentsInChildren<Transform>(true);
        Dictionary<Transform, PoseSnapshot> basePose = CapturePose(hierarchy, null);
        Dictionary<Transform, PoseSnapshot> finalPose = ClonePose(basePose);
        Dictionary<Transform, PoseSnapshot> sampledPose = CapturePose(hierarchy, null);

        foreach (LayerSelection selection in _layerSelections)
        {
            AnimationClip clip = selection.SelectedClip;
            if (clip == null)
            {
                continue;
            }

            float weight = selection.OverrideWeight
                ? Mathf.Clamp01(selection.ManualWeight)
                : ResolveLayerWeight(selection.ControllerLayer, selection.LayerIndex);

            if (weight <= 0f)
            {
                // Un layer sans influence n'a pas besoin d'être traité.
                continue;
            }

            float clipLength = Mathf.Max(clip.length, 0.0001f); // On évite les divisions par zéro.
            float localTime = Mathf.Clamp01(normalizedTime) * clipLength;

            // On restaure la pose d'origine pour obtenir l'influence pure du layer courant.
            ApplyPose(hierarchy, basePose);

            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(go, clip, localTime);
            AnimationMode.EndSampling();

            // On capture la pose générée par ce clip puis on la mélange avec le résultat
            // cumulé en respectant le poids du layer et son mode de blending.
            CapturePose(hierarchy, sampledPose);
            CopyPose(sampledPose, _layerSamplePose);

            AnimationClip transitionClip = selection.TransitionClip;
            float transitionBlend = Mathf.Clamp01(_transitionBlend);
            if (transitionClip != null && transitionBlend > 0f)
            {
                float transitionLength = Mathf.Max(transitionClip.length, 0.0001f);
                float transitionTime = Mathf.Clamp01(_transitionNormalizedTime) * transitionLength;

                ApplyPose(hierarchy, basePose);

                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(go, transitionClip, transitionTime);
                AnimationMode.EndSampling();

                CapturePose(hierarchy, sampledPose);
                CopyPose(sampledPose, _transitionSamplePose);

                InterpolatePoses(hierarchy, _layerSamplePose, _transitionSamplePose, transitionBlend);
            }

            BlendPoses(hierarchy, finalPose, basePose, _layerSamplePose, weight, selection.BlendingMode);
        }

        // Application finale de la pose résultante sur l'Animator ciblé.
        ApplyPose(hierarchy, finalPose);

        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }

    /// <summary>
    /// Rafraîchit la description des layers et synchronise les paramètres exposés par l'Animator.
    /// </summary>
    private void RefreshControllerData()
    {
        Dictionary<int, LayerSelectionSnapshot> snapshot = CreateLayerSnapshot();
        _layerSelections.Clear();
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
            LayerSelection selection = new LayerSelection(layer, i);

            CollectLayerStates(layer.stateMachine, string.Empty, selection.States);

            if (snapshot.TryGetValue(i, out LayerSelectionSnapshot previous))
            {
                selection.SelectedStateIndex = selection.FindStateIndex(previous.SelectedStateName);
                if (selection.SelectedStateIndex < 0 && selection.States.Count > 0)
                {
                    int fallbackPrevious = selection.FindStateIndex(_stateName);
                    selection.SelectedStateIndex = fallbackPrevious >= 0 ? fallbackPrevious : 0;
                }

                selection.TransitionStateIndex = selection.FindStateIndex(previous.TransitionStateName);
                selection.OverrideWeight = previous.OverrideWeight;
                selection.ManualWeight = Mathf.Clamp01(previous.ManualWeight);
            }
            else if (selection.States.Count > 0)
            {
                int fallback = selection.FindStateIndex(_stateName);
                selection.SelectedStateIndex = fallback >= 0 ? fallback : 0;
                selection.TransitionStateIndex = -1;
            }
            else
            {
                selection.SelectedStateIndex = -1;
                selection.TransitionStateIndex = -1;
            }

            _layerSelections.Add(selection);
        }

        SynchronizeParameters(controller);
    }

    /// <summary>
    /// Collecte récursivement tous les états Motion -> AnimationClip d'un state machine.
    /// </summary>
    private static void CollectLayerStates(AnimatorStateMachine machine, string parentPath, List<StateCache> results)
    {
        ChildAnimatorState[] states = machine.states;
        for (int i = 0; i < states.Length; i++)
        {
            AnimatorState state = states[i].state;
            if (state == null)
            {
                continue;
            }

            AnimationClip clip = state.motion as AnimationClip;
            if (clip != null)
            {
                string displayName = string.IsNullOrEmpty(parentPath)
                    ? state.name
                    : parentPath + "/" + state.name;
                results.Add(new StateCache(displayName, state, clip));
            }
        }

        ChildAnimatorStateMachine[] stateMachines = machine.stateMachines;
        for (int i = 0; i < stateMachines.Length; i++)
        {
            AnimatorStateMachine child = stateMachines[i].stateMachine;
            if (child == null)
            {
                continue;
            }

            string childPath = string.IsNullOrEmpty(parentPath)
                ? child.name
                : parentPath + "/" + child.name;
            CollectLayerStates(child, childPath, results);
        }
    }

    /// <summary>
    /// Synchronise le cache local des paramètres avec l'AnimatorController pour préserver les valeurs.
    /// </summary>
    private void SynchronizeParameters(AnimatorController controller)
    {
        if (controller == null)
        {
            return;
        }

        HashSet<string> seenParameters = new HashSet<string>();

        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            seenParameters.Add(parameter.name);

            if (!_parameterValues.TryGetValue(parameter.name, out ParameterValue value))
            {
                value = new ParameterValue(parameter);
            }
            else
            {
                value.Type = parameter.type;
            }

            _parameterValues[parameter.name] = value;
        }

        List<string> keysToRemove = new List<string>();
        foreach (KeyValuePair<string, ParameterValue> kvp in _parameterValues)
        {
            if (!seenParameters.Contains(kvp.Key))
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        for (int i = 0; i < keysToRemove.Count; i++)
        {
            _parameterValues.Remove(keysToRemove[i]);
        }
    }

    /// <summary>
    /// Conserve les sélections actuelles pour les restaurer après un rafraîchissement.
    /// </summary>
    private Dictionary<int, LayerSelectionSnapshot> CreateLayerSnapshot()
    {
        Dictionary<int, LayerSelectionSnapshot> snapshot = new Dictionary<int, LayerSelectionSnapshot>(_layerSelections.Count);
        for (int i = 0; i < _layerSelections.Count; i++)
        {
            LayerSelection selection = _layerSelections[i];
            snapshot[selection.LayerIndex] = new LayerSelectionSnapshot
            {
                SelectedStateName = selection.SelectedStateName,
                TransitionStateName = selection.TransitionStateName,
                OverrideWeight = selection.OverrideWeight,
                ManualWeight = selection.ManualWeight,
            };
        }

        return snapshot;
    }

    /// <summary>
    /// Applique immédiatement la valeur d'un paramètre sur l'Animator ciblé.
    /// </summary>
    private void ApplyParameterToAnimator(string parameterName, ParameterValue value)
    {
        if (_targetAnimator == null)
        {
            return;
        }

        switch (value.Type)
        {
            case AnimatorControllerParameterType.Float:
                _targetAnimator.SetFloat(parameterName, value.FloatValue);
                break;
            case AnimatorControllerParameterType.Int:
                _targetAnimator.SetInteger(parameterName, value.IntValue);
                break;
            case AnimatorControllerParameterType.Bool:
                _targetAnimator.SetBool(parameterName, value.BoolValue);
                break;
            case AnimatorControllerParameterType.Trigger:
                if (value.TriggerArmed)
                {
                    _targetAnimator.SetTrigger(parameterName);
                }
                else
                {
                    _targetAnimator.ResetTrigger(parameterName);
                }

                break;
        }
    }

    /// <summary>
    /// Retourne vrai si au moins un layer dispose d'un état jouable.
    /// </summary>
    private bool HasPlayableStates()
    {
        for (int i = 0; i < _layerSelections.Count; i++)
        {
            if (_layerSelections[i].SelectedClip != null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tick éditor utilisé uniquement pendant la lecture.
    /// </summary>
    private void OnEditorUpdate()
    {
        if (!_isPlaying || !HasPlayableStates())
        {
            return;
        }

        LayerSelection referenceSelection = _layerSelections.FirstOrDefault(selection => selection.SelectedClip != null);
        if (referenceSelection == null)
        {
            return;
        }

        AnimationClip referenceClip = referenceSelection.SelectedClip;
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

    /// <summary>
    /// Récupère le poids effectif pour un layer donné. En mode édition on se base
    /// principalement sur le poids par défaut défini dans l'AnimatorController, mais
    /// si l'Animator est en cours de lecture on tente de lire le poids runtime pour
    /// refléter d'éventuels réglages effectués par des scripts.
    /// </summary>
    private float ResolveLayerWeight(AnimatorControllerLayer layer, int layerIndex)
    {
        float weight = layer.defaultWeight;

        if (!EditorApplication.isPlaying && layer != null && layer.name == "Layer Body")
        {
            // Dans le testeur on souhaite pouvoir manipuler directement la couche Body même si
            // son poids par défaut est configuré à 0 dans l'Animator. En forçant un poids de 1
            // lorsque l'on n'est pas en Play Mode, on garantit que les poses du corps restent
            // visibles sans intervention manuelle de l'utilisateur.
            weight = 1f;
        }

        weight = Mathf.Clamp01(weight);

        if (_targetAnimator != null && EditorApplication.isPlaying)
        {
            // En Play Mode l'Animator renvoie les poids runtime configurés par le jeu.
            weight = Mathf.Clamp01(_targetAnimator.GetLayerWeight(layerIndex));
        }

        return weight;
    }

    /// <summary>
    /// Structure simple utilisée pour conserver la pose locale d'un Transform.
    /// </summary>
    private struct PoseSnapshot
    {
        public PoseSnapshot(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }

    /// <summary>
    /// Capture la pose de tous les transforms fournis afin de pouvoir la restaurer plus tard.
    /// </summary>
    private static Dictionary<Transform, PoseSnapshot> CapturePose(Transform[] transforms, Dictionary<Transform, PoseSnapshot> buffer)
    {
        Dictionary<Transform, PoseSnapshot> pose = buffer ?? new Dictionary<Transform, PoseSnapshot>(transforms.Length);
        pose.Clear();

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            pose[t] = new PoseSnapshot(t.localPosition, t.localRotation, t.localScale);
        }

        return pose;
    }

    /// <summary>
    /// Crée une copie profonde d'un dictionnaire de poses pour servir de base au blending.
    /// </summary>
    private static Dictionary<Transform, PoseSnapshot> ClonePose(Dictionary<Transform, PoseSnapshot> source)
    {
        Dictionary<Transform, PoseSnapshot> clone = new Dictionary<Transform, PoseSnapshot>(source.Count);
        foreach (KeyValuePair<Transform, PoseSnapshot> kvp in source)
        {
            clone[kvp.Key] = kvp.Value;
        }

        return clone;
    }

    /// <summary>
    /// Copie le contenu d'une pose dans un dictionnaire réutilisable afin de limiter les allocations.
    /// </summary>
    private static void CopyPose(Dictionary<Transform, PoseSnapshot> source, Dictionary<Transform, PoseSnapshot> destination)
    {
        destination.Clear();

        foreach (KeyValuePair<Transform, PoseSnapshot> kvp in source)
        {
            destination[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Applique la pose fournie sur l'ensemble de la hiérarchie pour la rendre visible dans la scène.
    /// </summary>
    private static void ApplyPose(Transform[] transforms, Dictionary<Transform, PoseSnapshot> pose)
    {
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            PoseSnapshot snapshot;
            if (!pose.TryGetValue(t, out snapshot))
            {
                continue;
            }

            t.localPosition = snapshot.Position;
            t.localRotation = snapshot.Rotation;
            t.localScale = snapshot.Scale;
        }
    }

    /// <summary>
    /// Mélange deux poses en fonction d'un facteur de progression et stocke le résultat dans fromPose.
    /// </summary>
    private static void InterpolatePoses(
        Transform[] transforms,
        Dictionary<Transform, PoseSnapshot> fromPose,
        Dictionary<Transform, PoseSnapshot> toPose,
        float t)
    {
        t = Mathf.Clamp01(t);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];
            PoseSnapshot from;
            PoseSnapshot to;
            if (!fromPose.TryGetValue(transform, out from) || !toPose.TryGetValue(transform, out to))
            {
                continue;
            }

            PoseSnapshot blended;
            blended.Position = Vector3.Lerp(from.Position, to.Position, t);
            blended.Scale = Vector3.Lerp(from.Scale, to.Scale, t);
            blended.Rotation = Quaternion.Slerp(from.Rotation, to.Rotation, t);

            fromPose[transform] = blended;
        }
    }

    /// <summary>
    /// Mélange la pose en cours avec celle issue d'un layer spécifique en respectant son poids et
    /// son mode de blending (Override ou Additive).
    /// </summary>
    private static void BlendPoses(
        Transform[] transforms,
        Dictionary<Transform, PoseSnapshot> finalPose,
        Dictionary<Transform, PoseSnapshot> basePose,
        Dictionary<Transform, PoseSnapshot> sampledPose,
        float weight,
        AnimatorLayerBlendingMode blendingMode)
    {
        if (weight <= 0f)
        {
            return;
        }

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];

            PoseSnapshot previous = finalPose[t];
            PoseSnapshot baseSnapshot = basePose[t];
            PoseSnapshot sampled = sampledPose[t];

            PoseSnapshot blended;
            if (blendingMode == AnimatorLayerBlendingMode.Additive)
            {
                // Les layers additifs ajoutent un delta par rapport à la pose de base.
                Vector3 deltaPosition = sampled.Position - baseSnapshot.Position;
                Vector3 deltaScale = sampled.Scale - baseSnapshot.Scale;
                Quaternion deltaRotation = Quaternion.Inverse(baseSnapshot.Rotation) * sampled.Rotation;

                blended.Position = previous.Position + deltaPosition * weight;
                blended.Scale = previous.Scale + deltaScale * weight;
                blended.Rotation = previous.Rotation * Quaternion.Slerp(Quaternion.identity, deltaRotation, weight);
            }
            else
            {
                // Les layers Override interpolent directement vers la pose du clip.
                blended.Position = Vector3.Lerp(previous.Position, sampled.Position, weight);
                blended.Scale = Vector3.Lerp(previous.Scale, sampled.Scale, weight);
                blended.Rotation = Quaternion.Slerp(previous.Rotation, sampled.Rotation, weight);
            }

            finalPose[t] = blended;
        }
    }
}
