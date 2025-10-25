using System.Linq;
using UnityEngine;
using UnityEditor;

public static class RemoveChildCollidersUtility
{
    // Ce menu réplique fidèlement l'ancien outil présent dans `Assets/RemoveChildColliders.cs`
    // mais vit désormais dans le dossier Editor pour éviter tout doublon d'attributs MenuItem.
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
            "Supprimer aussi les colliders directement sur les objets sélectionnés (en plus de leurs enfants) ?",
            "Oui, inclure le parent", "Non, seulement les enfants");

        bool includeInactive = EditorUtility.DisplayDialog(
            "Inclure objets inactifs ?",
            "Traiter aussi les GameObjects inactifs ?",
            "Oui", "Non");

        Undo.SetCurrentGroupName("Remove Child Colliders (Selection)");
        int undoGroup = Undo.GetCurrentGroup();

        int total = 0;
        try
        {
            EditorUtility.DisplayProgressBar("Suppression des colliders", "Analyse de la sélection...", 0f);

            for (int i = 0; i < selection.Length; i++)
            {
                var root = selection[i];
                float p = (i + 1f) / selection.Length;
                EditorUtility.DisplayProgressBar("Suppression des colliders", $"Traitement: {root.name}", p);

                total += RemoveUnder(root, includeParent, includeInactive);
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

    // --- Noyau : suppression avec Undo, multi-objets OK ---
    private static int RemoveUnder(GameObject root, bool includeParent, bool includeInactive)
    {
        if (root == null) return 0;

        // 3D
        var cols3D = root.GetComponentsInChildren<Collider>(includeInactive);
        if (!includeParent) cols3D = cols3D.Where(c => c.gameObject != root).ToArray();

        // 2D (décommente si tu veux aussi les 2D)
        var cols2D = root.GetComponentsInChildren<Collider2D>(includeInactive);
        if (!includeParent) cols2D = cols2D.Where(c => c.gameObject != root).ToArray();

        int count = 0;
        foreach (var c in cols3D) { Undo.DestroyObjectImmediate(c); count++; }
        foreach (var c in cols2D) { Undo.DestroyObjectImmediate(c); count++; }

        if (count > 0)
            Debug.Log($"[{root.name}] {count} collider(s) supprimé(s).");
        return count;
    }
}
