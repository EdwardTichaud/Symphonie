using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Composant utilitaire pour supprimer tous les colliders des enfants (et du parent, optionnel).
/// Fonctionne en mode Édition, compatible multi-sélection via l'inspector personnalisé.
/// </summary>
[ExecuteInEditMode]
public class RemoveChildColliders : MonoBehaviour
{
    [Tooltip("Inclure les colliders sur l'objet parent sélectionné.")]
    public bool includeParent = false;

    [Tooltip("Inclure les objets inactifs dans la recherche.")]
    public bool includeInactive = true;

#if UNITY_EDITOR
    /// <summary>
    /// Supprime les colliders sous 'root'. Utilise Undo en mode éditeur.
    /// </summary>
    public static int RemoveCollidersUnder(GameObject root, bool includeParent, bool includeInactive)
    {
        if (root == null) return 0;

        // Récupère tous les colliders (3D). Si tu veux gérer les 2D, duplique le bloc pour Collider2D.
        var all = root.GetComponentsInChildren<Collider>(includeInactive);
        if (!includeParent)
            all = all.Where(c => c.gameObject != root).ToArray();

        int count = 0;
        foreach (var col in all)
        {
            Undo.DestroyObjectImmediate(col); // Undo-safe en mode Éditeur
            count++;
        }
        if (count > 0)
            Debug.Log($"[{root.name}] {count} collider(s) supprimé(s).");
        return count;
    }
#endif
}

#if UNITY_EDITOR
// ----------- Inspector personnalisé : supporte la multi-sélection -----------
[CustomEditor(typeof(RemoveChildColliders)), CanEditMultipleObjects]
public class RemoveChildCollidersEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        // ----------- Actions disponibles dans l'inspector -----------
        // Le bouton ci-dessous reproduit le comportement du menu Tools historique
        // (détaillé dans la note plus bas) mais fonctionne directement depuis
        // l'inspector avec la multi-sélection. Cela évite à l'utilisateur
        // d'ouvrir un menu supplémentaire lorsque l'outil est déjà attaché sur
        // un GameObject dans la hiérarchie.
        if (GUILayout.Button("Supprimer les colliders des enfants"))
        {
            // Récupère les composants sélectionnés (multi-sélection supportée)
            // afin de traiter chaque GameObject individuellement avec ses
            // propres options includeParent/includeInactive.
            var components = targets
                .Cast<RemoveChildColliders>()
                .Distinct()
                .ToArray();

            if (components.Length == 0)
            {
                // Cas de figure improbable mais plus sûr : si aucun composant
                // n'est trouvé, on prévient l'utilisateur pour éviter un Undo
                // inutile.
                EditorUtility.DisplayDialog(
                    "Aucun objet",
                    "Sélectionnez au moins un objet possédant le composant RemoveChildColliders.",
                    "OK");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Supprimer les colliders des enfants");

            int total = 0;
            try
            {
                for (int i = 0; i < components.Length; i++)
                {
                    var component = components[i];
                    var go = component.gameObject;

                    // Affiche une barre de progression pour informer l'utilisateur
                    // du GameObject actuellement traité, particulièrement utile
                    // lorsque de nombreuses entrées sont sélectionnées.
                    float progress = (i + 1f) / components.Length;
                    EditorUtility.DisplayProgressBar("Suppression des colliders",
                        $"Traitement : {go.name}", progress);

                    total += RemoveChildColliders.RemoveCollidersUnder(
                        go,
                        component.includeParent,
                        component.includeInactive);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorUtility.DisplayDialog("Terminé",
                $"Colliders supprimés : {total}\nObjets traités : {components.Length}", "OK");
        }

        // ----------- Menu Tools (historique) -----------------
        // NB : Le menu Tools original vivait dans ce fichier mais entrait en
        // conflit avec la nouvelle implémentation d'outils d'éditeur dans
        // `Assets/Editor/RemoveChildColliderUtility.cs`. Pour éviter le message
        // "Cannot add validate method ... because a menu item with the same
        // name already has a validate method" signalé par Unity, le menu a été
        // entièrement déplacé dans le script d'éditeur. Nous conservons cette note
        // afin que les futurs contributeurs sachent que le menu existe toujours
        // (même commande, même fonctionnalité) mais se trouve désormais dans le
        // dossier Editor où Unity s'attend à le trouver.
    }

    // IMPORTANT : ne pas redéclarer ici de MenuItem pour "Tools/Colliders/Supprimer sur la sélection".
    // Le menu vit maintenant dans `Assets/Editor/RemoveChildColliderUtility.cs`. Cela évite
    // le message d'erreur Unity "Cannot add validate method ..." lié aux doublons de validate.
    // L'implémentation historique est laissée en commentaire à titre de documentation :
    // [MenuItem("Tools/Colliders/Supprimer sur la sélection (enfants)", true)]
    // public static bool ValidateRemoveOnSelection()
    //     => Selection.gameObjects != null && Selection.gameObjects.Length > 0;
}
#endif
