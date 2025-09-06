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
        if (GUILayout.Button("Supprimer les colliders sous l'objet (multi-sélection OK)"))
        {
            var components = targets.Cast<RemoveChildColliders>().ToArray();

            // Enregistre un Undo global lisible
            Undo.SetCurrentGroupName("Remove Child Colliders");
            int undoGroup = Undo.GetCurrentGroup();

            int total = 0;
            foreach (var comp in components)
            {
                if (comp == null || comp.gameObject == null) continue;
                total += RemoveChildColliders.RemoveCollidersUnder(
                    comp.gameObject, comp.includeParent, comp.includeInactive);
            }

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"Total colliders supprimés: {total} (sélection: {components.Length} objet(s)).");
        }
    }
}

// ----------- Menu Tools (pas besoin d'ajouter le composant) -----------------
public static class RemoveChildCollidersMenu
{
    [MenuItem("Tools/Colliders/Supprimer sur la sélection (enfants)")]
    public static void RemoveOnSelection()
    {
        var selection = Selection.gameObjects;
        if (selection == null || selection.Length == 0)
        {
            EditorUtility.DisplayDialog("Remove Colliders", "Aucun GameObject sélectionné.", "OK");
            return;
        }

        bool includeParent = EditorUtility.DisplayDialog(
            "Inclure le parent ?",
            "Souhaites-tu aussi supprimer les colliders directement sur les objets sélectionnés (pas seulement leurs enfants) ?",
            "Oui, inclure le parent", "Non, seulement les enfants");

        bool includeInactive = EditorUtility.DisplayDialog(
            "Inclure objets inactifs ?",
            "Faut-il aussi traiter les objets inactifs ?",
            "Oui", "Non");

        Undo.SetCurrentGroupName("Remove Child Colliders (Selection)");
        int undoGroup = Undo.GetCurrentGroup();

        int total = 0;
        try
        {
            EditorUtility.DisplayProgressBar("Suppression des colliders", "Analyse de la sélection...", 0f);

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
