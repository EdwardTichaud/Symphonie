using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Inspecteur personnalisé pour simplifier la configuration du CharacterAnimationController.
/// Il fournit des aides contextuelles et des actions rapides pour synchroniser les paramètres
/// attendus dans l'Animator depuis l'éditeur Unity.
/// </summary>
[CustomEditor(typeof(CharacterAnimationController))]
public class CharacterAnimationControllerEditor : Editor
{
    // Références sérialisées vers les champs privés du contrôleur. Nous conservons ces SerializedProperty
    // pour bénéficier de la gestion d'Undo/Redo standard de l'éditeur Unity.
    private SerializedProperty bodyStateParameter;
    private SerializedProperty bodyTransitionParameter;
    private SerializedProperty bodyNormalizedTimeParameter;
    private SerializedProperty bodyInstantParameter;
    private SerializedProperty bodySpeedParameter;
    private SerializedProperty faceStateParameter;
    private SerializedProperty faceTransitionParameter;
    private SerializedProperty faceInstantParameter;
    private SerializedProperty bodyTriggersProperty;

    // Indicateurs mis à jour après chaque scan afin d'afficher dans l'inspecteur
    // si les paramètres attendus sont présents et correctement typés.
    private bool parametersValid;
    private string lastScanReport;

    private void OnEnable()
    {
        // Mise en cache des propriétés sérialisées.
        bodyStateParameter = serializedObject.FindProperty("bodyStateParameter");
        bodyTransitionParameter = serializedObject.FindProperty("bodyTransitionDurationParameter");
        bodyNormalizedTimeParameter = serializedObject.FindProperty("bodyNormalizedTimeParameter");
        bodyInstantParameter = serializedObject.FindProperty("bodyInstantTransitionParameter");
        bodySpeedParameter = serializedObject.FindProperty("bodySpeedParameter");
        faceStateParameter = serializedObject.FindProperty("faceStateParameter");
        faceTransitionParameter = serializedObject.FindProperty("faceTransitionDurationParameter");
        faceInstantParameter = serializedObject.FindProperty("faceInstantTransitionParameter");
        bodyTriggersProperty = serializedObject.FindProperty("bodyTriggers");

        RefreshStatus();
    }

    public override void OnInspectorGUI()
    {
        // Synchronisation avec l'objet cible avant toute modification.
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Ce composant pilote toutes les animations via des paramètres. Assurez-vous que l'Animator contient bien les entrées listées ci-dessous pour bénéficier d'enchaînements fluides.",
            MessageType.Info);

        // Affichage classique des propriétés pour conserver la flexibilité de configuration.
        DrawDefaultInspector();

        EditorGUILayout.Space();
        DrawAnimatorAssistant();

        // Enregistrement des potentielles modifications utilisateur.
        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Dessine la section d'aide dédiée à la configuration de l'Animator.
    /// </summary>
    private void DrawAnimatorAssistant()
    {
        var controller = (CharacterAnimationController)target;
        var animator = controller.Animator != null ? controller.Animator : controller.GetComponent<Animator>();

        EditorGUILayout.LabelField("Assistant de configuration Animator", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(animator == null))
        {
            if (animator == null)
            {
                EditorGUILayout.HelpBox(
                    "Aucun Animator n'est associé à ce GameObject. Ajoutez un composant Animator pour accéder aux outils de configuration.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(lastScanReport, parametersValid ? MessageType.Info : MessageType.Warning);

                if (GUILayout.Button("Analyser l'Animator"))
                {
                    RefreshStatus();
                }

                if (GUILayout.Button("Configurer automatiquement les paramètres"))
                {
                    ConfigureAnimator(animator);
                    RefreshStatus();
                }
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Astuce : utilisez également le menu contextuel du composant (clic droit > Animator/Configurer automatiquement les paramètres) pour lancer cette opération directement depuis la hiérarchie.",
            MessageType.None);
    }

    /// <summary>
    /// Analyse l'Animator pour identifier les paramètres manquants et produire un compte-rendu.
    /// </summary>
    private void RefreshStatus()
    {
        var controller = (CharacterAnimationController)target;
        var animator = controller.Animator != null ? controller.Animator : controller.GetComponent<Animator>();
        if (animator == null)
        {
            parametersValid = false;
            lastScanReport = "Impossible d'analyser l'Animator : composant absent.";
            return;
        }

        var runtimeController = animator.runtimeAnimatorController;
        if (runtimeController == null)
        {
            parametersValid = false;
            lastScanReport = "Aucun RuntimeAnimatorController assigné. Assignez un contrôleur pour continuer.";
            return;
        }

        var animatorController = ResolveAnimatorController(runtimeController);
        if (animatorController == null)
        {
            parametersValid = false;
            lastScanReport = "Le contrôleur associé n'est pas un AnimatorController éditable. Remplacez-le ou éditez le contrôleur source.";
            return;
        }

        parametersValid = true;
        var report = new System.Text.StringBuilder();
        report.AppendLine("Vérification des paramètres :");

        CheckParameter(animatorController, bodyStateParameter.stringValue, AnimatorControllerParameterType.Int, "Body State", report);
        CheckParameter(animatorController, bodyTransitionParameter.stringValue, AnimatorControllerParameterType.Float, "Body Transition", report);
        CheckParameter(animatorController, bodyNormalizedTimeParameter.stringValue, AnimatorControllerParameterType.Float, "Body Normalized Time", report);
        CheckParameter(animatorController, bodyInstantParameter.stringValue, AnimatorControllerParameterType.Trigger, "Body Instant", report);
        CheckParameter(animatorController, bodySpeedParameter.stringValue, AnimatorControllerParameterType.Float, "Body Speed", report);
        CheckParameter(animatorController, faceStateParameter.stringValue, AnimatorControllerParameterType.Int, "Face State", report);
        CheckParameter(animatorController, faceTransitionParameter.stringValue, AnimatorControllerParameterType.Float, "Face Transition", report);
        CheckParameter(animatorController, faceInstantParameter.stringValue, AnimatorControllerParameterType.Trigger, "Face Instant", report);

        if (bodyTriggersProperty != null && bodyTriggersProperty.isArray)
        {
            for (int i = 0; i < bodyTriggersProperty.arraySize; i++)
            {
                var element = bodyTriggersProperty.GetArrayElementAtIndex(i);
                var parameterName = element.FindPropertyRelative("parameterName").stringValue;
                CheckParameter(animatorController, parameterName, AnimatorControllerParameterType.Trigger, $"Body Trigger {i + 1}", report);
            }
        }

        lastScanReport = report.ToString();
    }

    /// <summary>
    /// Configure l'Animator en ajoutant les paramètres manquants.
    /// </summary>
    private void ConfigureAnimator(Animator animator)
    {
        if (animator == null)
            return;

        var runtimeController = animator.runtimeAnimatorController;
        if (runtimeController == null)
        {
            EditorUtility.DisplayDialog("Configuration impossible", "Aucun RuntimeAnimatorController n'est assigné à cet Animator.", "Fermer");
            return;
        }

        var animatorController = ResolveAnimatorController(runtimeController);
        if (animatorController == null)
        {
            EditorUtility.DisplayDialog(
                "Contrôleur non pris en charge",
                "Le contrôleur assigné n'est pas un AnimatorController éditable. Utilisez un AnimatorController classique ou ouvrez le contrôleur source.",
                "Compris");
            return;
        }

        Undo.RecordObject(animatorController, "Configuration des paramètres Animator");

        EnsureParameter(animatorController, bodyStateParameter.stringValue, AnimatorControllerParameterType.Int);
        EnsureParameter(animatorController, bodyTransitionParameter.stringValue, AnimatorControllerParameterType.Float);
        EnsureParameter(animatorController, bodyNormalizedTimeParameter.stringValue, AnimatorControllerParameterType.Float);
        EnsureParameter(animatorController, bodyInstantParameter.stringValue, AnimatorControllerParameterType.Trigger);
        EnsureParameter(animatorController, bodySpeedParameter.stringValue, AnimatorControllerParameterType.Float);
        EnsureParameter(animatorController, faceStateParameter.stringValue, AnimatorControllerParameterType.Int);
        EnsureParameter(animatorController, faceTransitionParameter.stringValue, AnimatorControllerParameterType.Float);
        EnsureParameter(animatorController, faceInstantParameter.stringValue, AnimatorControllerParameterType.Trigger);

        if (bodyTriggersProperty != null && bodyTriggersProperty.isArray)
        {
            for (int i = 0; i < bodyTriggersProperty.arraySize; i++)
            {
                var element = bodyTriggersProperty.GetArrayElementAtIndex(i);
                var parameterName = element.FindPropertyRelative("parameterName").stringValue;
                EnsureParameter(animatorController, parameterName, AnimatorControllerParameterType.Trigger);
            }
        }

        EditorUtility.SetDirty(animatorController);
    }

    /// <summary>
    /// Vérifie qu'un paramètre donné est bien présent dans l'Animator et conforme au type souhaité.
    /// </summary>
    private void CheckParameter(AnimatorController controller, string parameterName, AnimatorControllerParameterType type, string label, System.Text.StringBuilder report)
    {
        if (string.IsNullOrEmpty(parameterName))
        {
            parametersValid = false;
            report.AppendLine($"- {label} : nom de paramètre vide.");
            return;
        }

        foreach (var parameter in controller.parameters)
        {
            if (parameter.name == parameterName)
            {
                if (parameter.type != type)
                {
                    parametersValid = false;
                    report.AppendLine($"- {label} : type attendu {type}, trouvé {parameter.type}. Remplacement recommandé.");
                }
                else
                {
                    report.AppendLine($"- {label} : OK");
                }

                return;
            }
        }

        parametersValid = false;
        report.AppendLine($"- {label} : paramètre introuvable.");
    }

    /// <summary>
    /// Ajoute un paramètre si nécessaire ou corrige son type.
    /// </summary>
    private void EnsureParameter(AnimatorController controller, string parameterName, AnimatorControllerParameterType type)
    {
        if (controller == null || string.IsNullOrEmpty(parameterName))
            return;

        foreach (var parameter in controller.parameters)
        {
            if (parameter.name == parameterName)
            {
                if (parameter.type != type)
                {
                    controller.RemoveParameter(parameter);
                    controller.AddParameter(parameterName, type);
                }

                return;
            }
        }

        controller.AddParameter(parameterName, type);
    }

    /// <summary>
    /// Récupère un AnimatorController éditable à partir du RuntimeAnimatorController assigné.
    /// </summary>
    private AnimatorController ResolveAnimatorController(RuntimeAnimatorController runtimeController)
    {
        if (runtimeController is AnimatorController controller)
            return controller;

        if (runtimeController is AnimatorOverrideController overrideController)
            return overrideController.runtimeAnimatorController as AnimatorController;

        return null;
    }
}
