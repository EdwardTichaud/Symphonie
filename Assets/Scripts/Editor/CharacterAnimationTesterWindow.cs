using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fenêtre d'éditeur permettant de tester les transitions paramétriques du
/// <see cref="CharacterAnimationController"/> sans lancer le mode Play.
/// L'objectif est de faciliter les itérations sur les durées, vitesses et
/// enchaînements d'états afin de garantir une excellente synchronisation
/// entre gameplay et narration.
/// </summary>
public class CharacterAnimationTesterWindow : EditorWindow
{
    private const string WindowTitle = "Testeur d'animations";

    [MenuItem("Symphonie/Animations/Tester les transitions" )]
    public static void ShowWindow()
    {
        var window = GetWindow<CharacterAnimationTesterWindow>();
        window.titleContent = new GUIContent(WindowTitle);
        window.Show();
    }

    /// <summary>
    /// Référence d'un prefab ou d'un GameObject qui possède un
    /// <see cref="CharacterAnimationController"/> configuré. Ce champ permet
    /// de générer une copie dédiée à la prévisualisation dans une scène cachée.
    /// </summary>
    [SerializeField] private GameObject sourceObject;

    /// <summary>
    /// Copie instanciée dans la scène de prévisualisation. Toutes les actions
    /// d'évaluation sont effectuées sur cette instance afin de préserver l'objet
    /// original dans la hiérarchie.
    /// </summary>
    private GameObject previewInstance;

    /// <summary>
    /// Scene spécifique utilisée pour les prévisualisations. Elle est isolée du
    /// contenu principal du projet et se ferme automatiquement avec la fenêtre.
    /// </summary>
    // NOTE : nous utilisons désormais directement le struct Scene d'Unity.
    // Le type PreviewScene exposé par UnityEditor est interne dans certaines
    // versions d'Unity, ce qui provoquait l'erreur CS0122. En conservant un
    // Scene standard nous restons compatibles tout en profitant de la même
    // API (IsValid, MoveGameObjectToScene, etc.).
    private Scene previewScene;

    /// <summary>
    /// Utilitaire Unity permettant de rendre une prévisualisation 3D dans une
    /// fenêtre personnalisée.
    /// </summary>

    private PreviewRenderUtility previewUtility;

    /// <summary>
    /// Délégation interne utilisée pour ajouter un GameObject à la scène de prévisualisation.
    /// Elle s'adapte selon la version d'Unity (API "Managed" ou fallback via AddSingleGO).
    /// </summary>
    private Action<GameObject> addPreviewGameObject;

    /// <summary>
    /// Délégation utilisée pour retirer un GameObject précédemment ajouté. Peut être vide si la
    /// version d'Unity ne propose pas l'API correspondante.
    /// </summary>
    private Action<GameObject> removePreviewGameObject;

    /// <summary>
    /// Indique si l'API "Managed" de PreviewRenderUtility est disponible (Unity 2020+).
    /// Elle permet d'éviter de réinstancier le personnage à chaque frame.
    /// </summary>
    private bool managedPreviewApiDisponible;

    /// <summary>
    /// Composant Animator de l'instance de prévisualisation. Il est forcé en
    /// mode "AlwaysAnimate" pour éviter la mise en veille hors focus.
    /// </summary>
    private Animator previewAnimator;

    /// <summary>
    /// Contrôleur d'animations paramétrique (corps + visage) associé à
    /// l'instance de prévisualisation.
    /// </summary>
    private CharacterAnimationController previewController;

    /// <summary>
    /// Orientation de la caméra utilisée pour le rendu de prévisualisation.
    /// </summary>
    private Vector2 previewAngles = new Vector2(120f, -15f);

    /// <summary>
    /// Facteur de zoom appliqué lors du rendu. Plus la valeur est élevée et
    /// plus la caméra s'éloigne du personnage.
    /// </summary>
    private float previewZoom = 1.5f;

    /// <summary>
    /// Permet de jouer l'Animator en continu même lorsque l'application n'est
    /// pas en mode Play.
    /// </summary>
    private bool autoPlay = true;

    /// <summary>
    /// Durée utilisée lors de l'application d'un nouvel état Body.
    /// </summary>
    private float bodyTransitionDuration = 0.1f;

    /// <summary>
    /// Position de départ normalisée (0-1) pour le layer Body.
    /// </summary>
    private float bodyNormalizedStartTime = 0f;

    /// <summary>
    /// Vitesse normalisée transmise au blend-tree de locomotion.
    /// </summary>
    private float bodyNormalizedSpeed = 0f;

    /// <summary>
    /// Indique si l'on souhaite forcer une transition instantanée pour le corps.
    /// </summary>
    private bool forceInstantBodyTransition;

    /// <summary>
    /// Durée utilisée lors de l'application d'un nouvel état Face.
    /// </summary>
    private float faceTransitionDuration = 0.1f;

    /// <summary>
    /// Indique si l'on souhaite forcer une transition instantanée pour le visage.
    /// </summary>
    private bool forceInstantFaceTransition;

    /// <summary>
    /// Etat actuellement sélectionné pour le layer Body.
    /// </summary>
    private CharacterAnimationController.BodyAnimationState selectedBodyState = CharacterAnimationController.BodyAnimationState.IdleWorld;

    /// <summary>
    /// Etat actuellement sélectionné pour le layer Face.
    /// </summary>
    private CharacterAnimationController.FaceAnimationState selectedFaceState = CharacterAnimationController.FaceAnimationState.Neutral;

    /// <summary>
    /// Tranche temporelle appliquée lorsque l'on avance manuellement l'Animator.
    /// </summary>
    private float manualDeltaTime = 1f / 60f;

    /// <summary>
    /// Timestamp de la dernière mise à jour pour calculer un delta correct lors
    /// du mode de lecture automatique.
    /// </summary>
    private double lastUpdateTime;

    /// <summary>
    /// Contrôleur actuellement analysé afin d'éviter de reconstruire les caches
    /// inutilement lorsque l'utilisateur ne change pas de personnage.
    /// </summary>
    private RuntimeAnimatorController cachedRuntimeController;

    /// <summary>
    /// Dictionnaire regroupant les états disponibles par nom court. Chaque entrée
    /// liste les layers et chemins correspondants partageant ce même identifiant.
    /// </summary>
    private readonly Dictionary<string, List<AnimatorStateReference>> animationStatesByName = new();

    /// <summary>
    /// Liste triée alphabétiquement des noms d'états détectés. Elle alimente
    /// directement l'interface de sélection pour afficher des résultats stables.
    /// </summary>
    private readonly List<string> sortedAnimationNames = new();

    /// <summary>
    /// Mémorisation de l'état des volets (foldouts) utilisés pour détailler les
    /// layers associés à une animation donnée. Permet de conserver l'UI ouverte
    /// entre deux repaints.
    /// </summary>
    private readonly Dictionary<string, bool> animationFoldoutStates = new();

    /// <summary>
    /// Position du scroll des animations directes. Cela évite que la liste ne se
    /// réinitialise lors des rafraîchissements de la fenêtre.
    /// </summary>
    private Vector2 animationListScroll;

    /// <summary>
    /// Filtre textuel appliqué sur les noms d'animations afin de faciliter la
    /// recherche des clips souhaités.
    /// </summary>
    private string animationNameFilter = string.Empty;

    /// <summary>
    /// Durée de transition utilisée lorsque l'on joue manuellement un état dans
    /// tous les layers correspondants.
    /// </summary>
    private float directAnimationTransitionDuration = 0.1f;

    /// <summary>
    /// Position normalisée de départ (0-1) lors de l'exécution manuelle d'un état.
    /// </summary>
    private float directAnimationNormalizedStartTime = 0f;

    /// <summary>
    /// Représentation simplifiée d'un état Animator. Elle regroupe toutes les
    /// informations nécessaires pour jouer ou afficher les occurrences d'un nom.
    /// </summary>
    private struct AnimatorStateReference
    {
        /// <summary>
        /// Index du layer dans lequel l'état est déclaré.
        /// </summary>
        public int LayerIndex;

        /// <summary>
        /// Hash du nom court (sans chemin) généré par Unity. Utile pour vérifier
        /// rapidement l'existence d'un état via <see cref="Animator.HasState(int, int)"/>.
        /// </summary>
        public int ShortNameHash;

        /// <summary>
        /// Hash du chemin complet (layer + sous machines) calculé par Unity.
        /// Il garantit l'accès correct à l'état, même si plusieurs partagent le
        /// même nom court dans des sous-machines différentes.
        /// </summary>
        public int FullPathHash;

        /// <summary>
        /// Nom lisible de l'état. Il correspond à la clé du dictionnaire mais est
        /// stocké ici pour tracer facilement les opérations dans les logs.
        /// </summary>
        public string StateName;

        /// <summary>
        /// Chemin lisible dans la hiérarchie des state machines (Layer/SousState/Etat).
        /// Très utile pour le debug et la présentation dans l'interface.
        /// </summary>
        public string DisplayPath;
    }

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
        EnsurePreviewUtility();
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        CleanupPreview();
    }

    private void OnEditorUpdate()
    {
        if (!autoPlay || previewAnimator == null)
            return;

        double currentTime = EditorApplication.timeSinceStartup;
        float deltaTime = (float)(currentTime - lastUpdateTime);

        if (deltaTime <= 0f)
            return;

        previewAnimator.Update(deltaTime);
        lastUpdateTime = currentTime;
        Repaint();
    }

    private void OnGUI()
    {
        DrawSourceSelector();
        DrawPlaybackControls();
        DrawBodyControls();
        DrawFaceControls();
        DrawTriggerControls();
        DrawDirectAnimationControls();
        DrawPreviewArea();
    }

    /// <summary>
    /// Affiche les champs permettant de sélectionner l'objet source à copier
    /// dans la scène de prévisualisation.
    /// </summary>
    private void DrawSourceSelector()
    {
        EditorGUILayout.LabelField("Source à tester", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        sourceObject = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Prefab ou GameObject", "Le modèle doit contenir un Animator et un CharacterAnimationController."),
            sourceObject,
            typeof(GameObject),
            true);
        if (EditorGUI.EndChangeCheck())
        {
            ReloadPreviewInstance();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Recharger", "Réinstancie complètement l'objet de test.")))
            {
                ReloadPreviewInstance();
            }

            using (new EditorGUI.DisabledScope(previewAnimator == null))
            {
                if (GUILayout.Button(new GUIContent("Rebind Animator", "Force un Rebind pour repartir d'un état neutre.")))
                {
                    previewController?.ForceAnimatorRebind();
                    previewAnimator?.Update(0f);
                    lastUpdateTime = EditorApplication.timeSinceStartup;
                    RebuildAnimationLookup();
                }
            }
        }
    }

    /// <summary>
    /// Interface liée à la progression temporelle de l'Animator.
    /// </summary>
    private void DrawPlaybackControls()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Lecture", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            bool newAutoPlay = EditorGUILayout.ToggleLeft(new GUIContent("Lecture continue", "Met à jour automatiquement l'Animator à chaque frame d'éditeur."), autoPlay, GUILayout.Width(150f));
            if (newAutoPlay != autoPlay)
            {
                autoPlay = newAutoPlay;
                lastUpdateTime = EditorApplication.timeSinceStartup;
            }

            using (new EditorGUI.DisabledScope(previewAnimator == null))
            {
                if (GUILayout.Button(new GUIContent("Pas +", "Avance manuellement l'Animator selon le delta choisi.")))
                {
                    previewAnimator.Update(Mathf.Max(0f, manualDeltaTime));
                    Repaint();
                }
            }
        }

        using (new EditorGUI.DisabledScope(previewAnimator == null))
        {
            manualDeltaTime = EditorGUILayout.Slider(new GUIContent("Delta manuel", "Durée en secondes utilisée par le bouton Pas +."), manualDeltaTime, 0.01f, 0.5f);
        }
    }

    /// <summary>
    /// Regroupe les contrôles dédiés au layer "Body" du contrôleur.
    /// </summary>
    private void DrawBodyControls()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Layer corps", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(previewController == null))
        {
            selectedBodyState = (CharacterAnimationController.BodyAnimationState)EditorGUILayout.EnumPopup(new GUIContent("Etat", "Etat principal à évaluer dans le layer Body."), selectedBodyState);
            bodyTransitionDuration = EditorGUILayout.Slider(new GUIContent("Durée de transition", "Durée d'interpolation en secondes."), bodyTransitionDuration, 0f, 1.5f);
            bodyNormalizedStartTime = EditorGUILayout.Slider(new GUIContent("Départ normalisé", "Position dans le clip d'arrivée (0 = début, 1 = fin)."), bodyNormalizedStartTime, 0f, 1f);
            bodyNormalizedSpeed = EditorGUILayout.Slider(new GUIContent("Vitesse normalisée", "Pilote le blend-tree de locomotion."), bodyNormalizedSpeed, 0f, 1f);
            forceInstantBodyTransition = EditorGUILayout.Toggle(new GUIContent("Transition instantanée", "Déclenche le trigger dédié si configuré."), forceInstantBodyTransition);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Appliquer", "Envoie tous les paramètres configurés au layer Body.")))
                {
                    ApplyBodyState();
                }

                if (GUILayout.Button(new GUIContent("Idle monde", "Raccourci pour revenir à l'idle d'exploration.")))
                {
                    selectedBodyState = CharacterAnimationController.BodyAnimationState.IdleWorld;
                    ApplyBodyState();
                }
            }
        }
    }

    /// <summary>
    /// Regroupe les contrôles dédiés au layer "Face" du contrôleur.
    /// </summary>
    private void DrawFaceControls()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Layer visage", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(previewController == null))
        {
            selectedFaceState = (CharacterAnimationController.FaceAnimationState)EditorGUILayout.EnumPopup(new GUIContent("Expression", "Expression faciale à tester."), selectedFaceState);
            faceTransitionDuration = EditorGUILayout.Slider(new GUIContent("Durée de transition", "Durée d'interpolation des blend-shapes."), faceTransitionDuration, 0f, 1.5f);
            forceInstantFaceTransition = EditorGUILayout.Toggle(new GUIContent("Transition instantanée", "Active le trigger visage si disponible."), forceInstantFaceTransition);

            if (GUILayout.Button(new GUIContent("Appliquer", "Envoie les paramètres sur le layer visage.")))
            {
                previewController?.SetFaceState(selectedFaceState, faceTransitionDuration, 0f, forceInstantFaceTransition);
                previewAnimator?.Update(0f);
            }
        }
    }

    /// <summary>
    /// Affiche les triggers disponibles pour le layer corps.
    /// </summary>
    private void DrawTriggerControls()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Triggers", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(previewController == null))
        {
            foreach (CharacterAnimationController.BodyAnimationTrigger trigger in Enum.GetValues(typeof(CharacterAnimationController.BodyAnimationTrigger)))
            {
                if (trigger == CharacterAnimationController.BodyAnimationTrigger.None)
                    continue;

                if (GUILayout.Button(new GUIContent(trigger.ToString(), "Déclenche le trigger associé dans l'Animator.")))
                {
                    previewController.ActivateBodyTrigger(trigger);
                    previewAnimator?.Update(0f);
                }
            }
        }
    }

    /// <summary>
    /// Interface permettant de lancer rapidement une animation par son nom sur tous
    /// les layers possédant un état correspondant.
    /// </summary>
    private void DrawDirectAnimationControls()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Lecture par nom", EditorStyles.boldLabel);

        if (previewAnimator == null)
        {
            EditorGUILayout.HelpBox("Instanciez un personnage pour accéder à la lecture directe des animations.", MessageType.Info);
            return;
        }

        // Si le contrôleur change à chaud (édition du prefab), nous reconstruisons automatiquement
        // les caches pour éviter tout décalage entre la réalité et l'interface.
        if (previewAnimator.runtimeAnimatorController != cachedRuntimeController)
        {
            RebuildAnimationLookup();
        }

        if (sortedAnimationNames.Count == 0)
        {
            EditorGUILayout.HelpBox("Aucun état détecté sur l'Animator. Cliquez sur \"Rafraîchir\" après avoir configuré un contrôleur.", MessageType.Warning);
            if (GUILayout.Button(new GUIContent("Rafraîchir", "Analyse à nouveau le contrôleur pour détecter les états disponibles.")))
            {
                RebuildAnimationLookup();
            }
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            animationNameFilter = EditorGUILayout.TextField(new GUIContent("Filtre", "Filtre textuel appliqué sur les noms d'états."), animationNameFilter);

            if (GUILayout.Button(new GUIContent("Rafraîchir", "Reconstruit la liste d'états depuis le contrôleur courant."), GUILayout.Width(100f)))
            {
                RebuildAnimationLookup();
            }
        }

        directAnimationTransitionDuration = EditorGUILayout.Slider(new GUIContent("Transition", "Durée de cross-fade appliquée lors du lancement manuel."), directAnimationTransitionDuration, 0f, 1.5f);
        directAnimationNormalizedStartTime = EditorGUILayout.Slider(new GUIContent("Départ normalisé", "Position dans l'animation (0 = début, 1 = fin)."), directAnimationNormalizedStartTime, 0f, 1f);

        IEnumerable<string> filteredNames = sortedAnimationNames;
        if (!string.IsNullOrWhiteSpace(animationNameFilter))
        {
            filteredNames = filteredNames.Where(name => name.IndexOf(animationNameFilter, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        List<string> namesToDisplay = filteredNames.ToList();
        if (namesToDisplay.Count == 0)
        {
            EditorGUILayout.HelpBox("Aucune animation ne correspond au filtre actuel.", MessageType.Info);
            return;
        }

        using (var scroll = new EditorGUILayout.ScrollViewScope(animationListScroll, GUILayout.Height(250f)))
        {
            animationListScroll = scroll.scrollPosition;

            foreach (string animationName in namesToDisplay)
            {
                if (!animationStatesByName.TryGetValue(animationName, out List<AnimatorStateReference> states) || states.Count == 0)
                    continue;

                string header = $"{animationName} ({states.Count} occurrence(s))";
                animationFoldoutStates.TryGetValue(animationName, out bool foldout);

                foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, new GUIContent(header, "Afficher les layers possédant cet état."));
                if (foldout)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        if (GUILayout.Button(new GUIContent("Jouer sur tous les layers", "Lance simultanément l'état sur chaque layer correspondant.")))
                        {
                            PlayAnimationOnMatchingLayers(animationName);
                        }

                        foreach (AnimatorStateReference stateReference in states)
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                string layerLabel = previewAnimator.GetLayerName(stateReference.LayerIndex);
                                EditorGUILayout.LabelField(new GUIContent(layerLabel, "Nom du layer dans l'Animator."), GUILayout.Width(120f));
                                EditorGUILayout.LabelField(new GUIContent(stateReference.DisplayPath, "Chemin complet de l'état."));

                                if (GUILayout.Button(new GUIContent("Solo", "Joue uniquement cette occurrence."), GUILayout.Width(60f)))
                                {
                                    PlaySingleAnimationState(stateReference);
                                }
                            }
                        }
                    }
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
                animationFoldoutStates[animationName] = foldout;
            }
        }
    }

    /// <summary>
    /// Dessine la zone de rendu 3D qui affiche le personnage testé.
    /// </summary>
    private void DrawPreviewArea()
    {
        EditorGUILayout.Space();
        Rect previewRect = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        GUI.Box(previewRect, GUIContent.none, EditorStyles.helpBox);

        if (previewUtility == null || previewInstance == null)
        {
            EditorGUI.DropShadowLabel(previewRect, "Sélectionnez un personnage pour lancer la prévisualisation.");
            return;
        }

        HandlePreviewInput(previewRect);

        if (Event.current.type == EventType.Repaint)
        {
            previewUtility.BeginPreview(previewRect, GUIStyle.none);
            RenderPreview();
            Texture texture = previewUtility.EndPreview();
            GUI.DrawTexture(previewRect, texture, ScaleMode.StretchToFill, false);
        }
    }

    /// <summary>
    /// Gestion des interactions utilisateur (rotation, zoom) sur la zone de prévisualisation.
    /// </summary>
    private void HandlePreviewInput(Rect previewRect)
    {
        Event evt = Event.current;
        if (!previewRect.Contains(evt.mousePosition))
            return;

        if (evt.type == EventType.ScrollWheel)
        {
            previewZoom = Mathf.Clamp(previewZoom + evt.delta.y * 0.05f, 0.5f, 4f);
            evt.Use();
            Repaint();
        }
        else if (evt.type == EventType.MouseDrag && evt.button == 0)
        {
            previewAngles += evt.delta * 0.5f;
            evt.Use();
            Repaint();
        }
    }

    /// <summary>
    /// Positionne la caméra et déclenche le rendu de la scène de prévisualisation.
    /// </summary>
    private void RenderPreview()
    {
        if (previewInstance == null)
            return;

        Bounds bounds = CalculateBounds(previewInstance);
        float radius = Mathf.Max(0.5f, bounds.extents.magnitude);
        float distance = radius * (2.2f * previewZoom);
        Quaternion rotation = Quaternion.Euler(previewAngles.y, previewAngles.x, 0f);
        Vector3 direction = rotation * Vector3.forward;

        previewUtility.camera.transform.position = bounds.center - direction * distance;
        previewUtility.camera.transform.rotation = rotation;
        previewUtility.camera.nearClipPlane = 0.05f;
        previewUtility.camera.farClipPlane = distance * 4f;
        previewUtility.camera.clearFlags = CameraClearFlags.Color;
        previewUtility.camera.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 1f);

        previewUtility.lights[0].intensity = 1.2f;
        previewUtility.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
        previewUtility.lights[1].intensity = 0.8f;

        previewUtility.Render();
    }

    /// <summary>
    /// Calcule un bounding box global pour positionner correctement la caméra.
    /// </summary>
    private static Bounds CalculateBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    /// <summary>
    /// Lance une animation par son nom sur tous les layers possédant un état correspondant.
    /// </summary>
    /// <param name="stateName">Nom court de l'état à jouer.</param>
    private void PlayAnimationOnMatchingLayers(string stateName)
    {
        if (previewAnimator == null)
            return;

        if (!animationStatesByName.TryGetValue(stateName, out List<AnimatorStateReference> states) || states.Count == 0)
        {
            Debug.LogWarning($"[AnimationTester] Aucun état nommé '{stateName}' n'a été trouvé lors de la lecture groupée.");
            return;
        }

        int playedCount = 0;
        foreach (AnimatorStateReference stateReference in states)
        {
            if (!previewAnimator.HasState(stateReference.LayerIndex, stateReference.FullPathHash) &&
                !previewAnimator.HasState(stateReference.LayerIndex, stateReference.ShortNameHash))
            {
                continue;
            }

            previewAnimator.CrossFade(stateReference.FullPathHash, Mathf.Max(0f, directAnimationTransitionDuration), stateReference.LayerIndex, Mathf.Clamp01(directAnimationNormalizedStartTime));
            playedCount++;
        }

        if (playedCount == 0)
        {
            Debug.LogWarning($"[AnimationTester] Impossible de jouer '{stateName}' : aucun layer valide détecté.");
            return;
        }

        previewAnimator.Update(0f);
        lastUpdateTime = EditorApplication.timeSinceStartup;
        Debug.Log($"[AnimationTester] Lecture de '{stateName}' sur {playedCount} layer(s).");
        Repaint();
    }

    /// <summary>
    /// Joue une occurrence précise d'un état sur un layer donné.
    /// </summary>
    private void PlaySingleAnimationState(AnimatorStateReference stateReference)
    {
        if (previewAnimator == null)
            return;

        if (!previewAnimator.HasState(stateReference.LayerIndex, stateReference.FullPathHash) &&
            !previewAnimator.HasState(stateReference.LayerIndex, stateReference.ShortNameHash))
        {
            Debug.LogWarning($"[AnimationTester] L'état '{stateReference.DisplayPath}' n'existe plus dans l'Animator.");
            return;
        }

        previewAnimator.CrossFade(stateReference.FullPathHash, Mathf.Max(0f, directAnimationTransitionDuration), stateReference.LayerIndex, Mathf.Clamp01(directAnimationNormalizedStartTime));
        previewAnimator.Update(0f);
        lastUpdateTime = EditorApplication.timeSinceStartup;
        Debug.Log($"[AnimationTester] Lecture solo de '{stateReference.DisplayPath}'.");
        Repaint();
    }

    /// <summary>
    /// Applique tous les paramètres actuellement configurés pour le layer Body.
    /// </summary>
    private void ApplyBodyState()
    {
        if (previewController == null)
            return;

        previewController.SetBodySpeed(bodyNormalizedSpeed);
        previewController.SetBodyState(selectedBodyState, bodyTransitionDuration, bodyNormalizedStartTime, forceInstantBodyTransition);
        previewAnimator?.Update(0f);
    }

    /// <summary>
    /// Reconstruit les caches de lecture directe en analysant le contrôleur Animator courant.
    /// </summary>
    private void RebuildAnimationLookup()
    {
        animationStatesByName.Clear();
        sortedAnimationNames.Clear();
        animationFoldoutStates.Clear();

        if (previewAnimator == null)
        {
            cachedRuntimeController = null;
            return;
        }

        cachedRuntimeController = previewAnimator.runtimeAnimatorController;
        if (cachedRuntimeController is not AnimatorController controller)
            return;

        for (int layerIndex = 0; layerIndex < controller.layers.Length; layerIndex++)
        {
            AnimatorControllerLayer layer = controller.layers[layerIndex];
            if (layer?.stateMachine == null)
                continue;

            string initialPath = string.IsNullOrEmpty(layer.name) ? $"Layer {layerIndex}" : layer.name;
            CollectStatesRecursive(layer.stateMachine, layerIndex, initialPath);
        }

        sortedAnimationNames.AddRange(animationStatesByName.Keys);
        sortedAnimationNames.Sort(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parcourt récursivement une state machine afin d'enregistrer tous les états disponibles.
    /// </summary>
    private void CollectStatesRecursive(AnimatorStateMachine stateMachine, int layerIndex, string parentPath)
    {
        if (stateMachine == null)
            return;

        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            if (childState.state == null)
                continue;

            string displayPath = string.IsNullOrEmpty(parentPath) ? childState.state.name : $"{parentPath}/{childState.state.name}";

            var reference = new AnimatorStateReference
            {
                LayerIndex = layerIndex,
                ShortNameHash = childState.state.shortNameHash,
                FullPathHash = childState.state.fullPathHash,
                StateName = childState.state.name,
                DisplayPath = displayPath
            };

            if (!animationStatesByName.TryGetValue(reference.StateName, out List<AnimatorStateReference> list))
            {
                list = new List<AnimatorStateReference>();
                animationStatesByName.Add(reference.StateName, list);
            }

            list.Add(reference);
        }

        foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
        {
            if (childMachine.stateMachine == null)
                continue;

            string nextPath = string.IsNullOrEmpty(parentPath) ? childMachine.stateMachine.name : $"{parentPath}/{childMachine.stateMachine.name}";
            CollectStatesRecursive(childMachine.stateMachine, layerIndex, nextPath);
        }
    }

    /// <summary>
    /// Créé l'utilitaire de rendu si nécessaire.
    /// </summary>
    private void EnsurePreviewUtility()
    {
        if (previewUtility != null)
            return;

        previewUtility = new PreviewRenderUtility();
        previewUtility.cameraFieldOfView = 30f;

        var addMethod = typeof(PreviewRenderUtility).GetMethod("AddManagedGO", new[] { typeof(GameObject) });
        var removeMethod = typeof(PreviewRenderUtility).GetMethod("RemoveManagedGO", new[] { typeof(GameObject) });

        if (addMethod != null)
        {
            managedPreviewApiDisponible = true;
            addPreviewGameObject = go =>
            {
                if (go != null)
                    addMethod.Invoke(previewUtility, new object[] { go });
            };
            if (removeMethod != null)
            {
                removePreviewGameObject = go =>
                {
                    if (go != null)
                        removeMethod.Invoke(previewUtility, new object[] { go });
                };
            }
            else
            {
                removePreviewGameObject = _ => { };
            }
        }
        else
        {
            managedPreviewApiDisponible = false;
            addPreviewGameObject = go =>
            {
                if (go != null)
                    previewUtility.AddSingleGO(go);
            };
            removePreviewGameObject = _ => { };
        }
    }

    /// <summary>
    /// Réinstancie complètement l'objet de prévisualisation.
    /// </summary>
    private void ReloadPreviewInstance()
    {
        CleanupPreviewInstance();

        if (sourceObject == null)
        {
            previewController = null;
            previewAnimator = null;
            return;
        }

        EnsurePreviewUtility();

        if (!previewScene.IsValid())
        {
            // Création explicite d'une nouvelle scène de prévisualisation via
            // l'API publique d'EditorSceneManager afin d'éviter toute dépendance
            // à des types internes non accessibles.
            previewScene = EditorSceneManager.NewPreviewScene();
        }

        try
        {
            if (PrefabUtility.IsPartOfPrefabAsset(sourceObject))
            {
                previewInstance = (GameObject)PrefabUtility.InstantiatePrefab(sourceObject, previewScene);
            }
            else
            {
                previewInstance = Instantiate(sourceObject);
                // Déplacement dans la scène de prévisualisation nouvellement créée.
                SceneManager.MoveGameObjectToScene(previewInstance, previewScene);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Impossible de créer l'aperçu : {ex.Message}");
            CleanupPreviewInstance();
            return;
        }

        addPreviewGameObject?.Invoke(previewInstance);
        previewInstance.transform.position = Vector3.zero;

        previewAnimator = previewInstance.GetComponent<Animator>();
        if (previewAnimator != null)
        {
            previewAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            previewAnimator.updateMode = AnimatorUpdateMode.Normal;
        }

        previewController = previewInstance.GetComponent<CharacterAnimationController>();
        previewController?.RefreshCachedParameters();

        if (previewAnimator != null)
        {
            previewAnimator.Rebind();
            previewAnimator.Update(0f);
            RebuildAnimationLookup();
        }

        lastUpdateTime = EditorApplication.timeSinceStartup;
    }

    /// <summary>
    /// Détruit l'instance de prévisualisation actuelle sans toucher à l'objet source.
    /// </summary>
    private void CleanupPreviewInstance()
    {
        if (previewInstance != null)
        {
            removePreviewGameObject?.Invoke(previewInstance);
            DestroyImmediate(previewInstance);
            previewInstance = null;
        }

        previewController = null;
        previewAnimator = null;
        cachedRuntimeController = null;
        animationStatesByName.Clear();
        sortedAnimationNames.Clear();
        animationFoldoutStates.Clear();
    }

    /// <summary>
    /// Libère toutes les ressources utilisées par la fenêtre.
    /// </summary>
    private void CleanupPreview()
    {
        CleanupPreviewInstance();

        if (previewScene.IsValid())
        {
            // Fermeture propre de la scène de prévisualisation en passant par
            // l'API d'EditorSceneManager, évitant ainsi l'appel à une méthode
            // interne de PreviewScene.
            EditorSceneManager.ClosePreviewScene(previewScene);
            previewScene = default;
        }

        previewUtility?.Cleanup();
        previewUtility = null;
        addPreviewGameObject = null;
        removePreviewGameObject = null;
        managedPreviewApiDisponible = false;
    }
}
