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
        public LayerState(
            string layerName,
            AnimationClip clip,
            float weight,
            AnimatorLayerBlendingMode blendingMode,
            int layerIndex)
        {
            LayerName = layerName;
            Clip = clip;
            Weight = weight;
            BlendingMode = blendingMode;
            LayerIndex = layerIndex;
        }

        public string LayerName { get; private set; }
        public AnimationClip Clip { get; private set; }
        public float Weight { get; private set; }
        public AnimatorLayerBlendingMode BlendingMode { get; private set; }
        public int LayerIndex { get; private set; }
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
            // On mémorise les anciennes valeurs pour détecter précisément les changements
            // effectués par l'utilisateur, même lorsque la lecture est déjà en cours.
            Animator previousAnimator = _targetAnimator;
            string previousStateName = _stateName;
            float previousPlaybackSpeed = _playbackSpeed;
            bool previousLoop = _loop;
            float previousNormalizedTime = _normalizedTime;
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
                    if (_isPlaying)
                    {
                        // Pendant la lecture on veut appliquer immédiatement les changements sans devoir
                        // redémarrer manuellement : on resynchronise la timeline et on réévalue la pose.
                        HandleChangesWhilePlaying(
                            previousAnimator,
                            previousStateName,
                            previousPlaybackSpeed,
                            previousLoop,
                            previousNormalizedTime);
                    }
                    else
                    {
                        // Hors lecture on se contente d'échantillonner la nouvelle pose une seule fois.
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
                    float displayWeight = match.Weight;
                    if (_targetAnimator != null && EditorApplication.isPlaying)
                    {
                        // Pendant le Play Mode on reflète les poids runtime réels pour faciliter le debug.
                        displayWeight = Mathf.Clamp01(_targetAnimator.GetLayerWeight(match.LayerIndex));
                    }

                    string label = string.Format(
                        "Layer {0} - {1} (poids {2:0.###}, mode {3})",
                        match.LayerIndex,
                        match.LayerName,
                        displayWeight,
                        match.BlendingMode);
                    EditorGUILayout.LabelField(label, GUILayout.Width(260f));
                    EditorGUILayout.ObjectField(match.Clip, typeof(AnimationClip), false);
                }
            }
        }
    }

    /// <summary>
    /// Applique les modifications faites dans l'inspecteur pendant que la lecture est active.
    /// </summary>
    private void HandleChangesWhilePlaying(
        Animator previousAnimator,
        string previousStateName,
        float previousPlaybackSpeed,
        bool previousLoop,
        float previousNormalizedTime)
    {
        if (_matches.Count == 0)
        {
            // Si les nouvelles données ne trouvent plus aucun état valide on arrête la lecture pour
            // éviter de rester bloqué dans un état incohérent.
            StopPlayback(true);
            EditorUtility.DisplayDialog(
                "Pas de correspondances",
                "Les paramètres actuels ne correspondent à aucun état jouable. La lecture est arrêtée.",
                "Compris");
            return;
        }

        // On vérifie quels paramètres ont réellement changé afin d'ajuster l'ancre temporelle
        // correctement. Chaque modification doit être prise en compte pour éviter un saut brutal.
        bool animatorChanged = previousAnimator != _targetAnimator;
        bool stateChanged = previousStateName != _stateName;
        bool speedChanged = !Mathf.Approximately(previousPlaybackSpeed, _playbackSpeed);
        bool loopChanged = previousLoop != _loop;
        bool normalizedChanged = !Mathf.Approximately(previousNormalizedTime, _normalizedTime);

        // Les changements impactant directement la position dans le clip (changement de state,
        // d'Animator ou de temps normalisé) requièrent une resynchronisation immédiate.
        bool requiresResync = animatorChanged || stateChanged || normalizedChanged || speedChanged;

        if (loopChanged && !_loop)
        {
            // En mode non bouclé on garde une valeur 0-1 stricte pour ne pas dépasser la fin du clip.
            _normalizedTime = Mathf.Clamp01(_normalizedTime);
        }

        if (requiresResync)
        {
            // On redéfinit l'ancre temporelle à partir de la nouvelle configuration pour que la
            // progression continue de manière fluide après le changement.
            _normalizedTime = Mathf.Clamp01(_normalizedTime);
            _playStartNormalized = _normalizedTime;
            _playStartEditorTime = EditorApplication.timeSinceStartup;
        }

        // Quoi qu'il arrive on ré-échantillonne la pose courante pour refléter les derniers réglages.
        SampleAtNormalizedTime(_normalizedTime);
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
                    float weight = ResolveLayerWeight(layer, i);
                    _matches.Add(new LayerState(layer.name, clip, weight, layer.blendingMode, i));
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

        // On capture une photographie complète de la pose actuelle pour pouvoir revenir
        // à l'état initial entre deux layers. Cela permet de simuler un vrai système
        // de blending en utilisant les poids configurés sur l'Animator.
        Transform[] hierarchy = go.GetComponentsInChildren<Transform>(true);
        Dictionary<Transform, PoseSnapshot> basePose = CapturePose(hierarchy, null);
        Dictionary<Transform, PoseSnapshot> finalPose = ClonePose(basePose);
        Dictionary<Transform, PoseSnapshot> sampledPose = CapturePose(hierarchy, null);

        foreach (LayerState match in _matches)
        {
            AnimationClip clip = match.Clip;
            if (clip == null)
            {
                continue;
            }

            float weight = Mathf.Clamp01(match.Weight);
            if (_targetAnimator != null && EditorApplication.isPlaying)
            {
                // On privilégie le poids runtime pour suivre les variations dynamiques éventuelles.
                weight = Mathf.Clamp01(_targetAnimator.GetLayerWeight(match.LayerIndex));
            }
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
            BlendPoses(hierarchy, finalPose, basePose, sampledPose, weight, match.BlendingMode);
        }

        // Application finale de la pose résultante sur l'Animator ciblé.
        ApplyPose(hierarchy, finalPose);

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
