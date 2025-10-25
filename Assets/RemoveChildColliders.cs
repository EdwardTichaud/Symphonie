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
// ----------- Menu Tools (historique) -----------------
// NB : Le menu Tools original vivait dans ce fichier mais entrait en
// conflit avec la nouvelle implmentation d'outils d'diteur dans
// `Assets/Editor/RemoveChildColliderUtility.cs`. Pour viter le message
// "Cannot add validate method ... because a menu item with the same
// name already has a validate method" signal par Unity, le menu a t
// entirement dplac dans le script d'diteur. Nous conservons cette note
// afin que les futurs contributeurs sachent que le menu existe toujours
// (mme commande, mme fonctionnalit) mais se trouve dsormais dans le
// dossier Editor o Unity s'attend  le trouver.
            for (int i = 0; i < selection.Length; i++)
            {
                float p = (i + 1f) / selection.Length;
                EditorUtility.DisplayProgressBar("Suppression des colliders",
                    $"Traitement: {selection[i].name}", p);

                total += RemoveChildColliders.RemoveCollidersUnder(selection[i], includeParent, includeInactive);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.DisplayDialog("Terminé",
            $"Colliders supprimés: {total}\nObjets traités: {selection.Length}", "OK");
    }

    [MenuItem("Tools/Colliders/Supprimer sur la sélection (enfants)", true)]
    public static bool ValidateRemoveOnSelection()
        => Selection.gameObjects != null && Selection.gameObjects.Length > 0;
}
#endif
