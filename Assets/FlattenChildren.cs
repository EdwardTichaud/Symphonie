using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[AddComponentMenu("Symphonie/Tools/Flatten Direct Children (keep grandchildren)")]
public class FlattenChildren : MonoBehaviour
{
#if UNITY_EDITOR
    [ContextMenu("Shunter enfants directs (garder leurs enfants)")]
    public void FlattenNow_ContextMenu()
    {
        FlattenNow();
    }
#endif

    public void FlattenNow()
    {
#if UNITY_EDITOR
        Flatten(transform);
#else
        Debug.LogWarning("Cette fonction s'utilise dans l'éditeur Unity.");
#endif
    }

#if UNITY_EDITOR
    public static void Flatten(Transform parent)
    {
        if (parent == null) return;

        // Snapshot des enfants directs (car on va modifier la hiérarchie)
        var directChildren = new List<Transform>(parent.childCount);
        for (int i = 0; i < parent.childCount; i++)
            directChildren.Add(parent.GetChild(i));

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName($"Flatten Direct Children: {parent.name}");
        int group = Undo.GetCurrentGroup();

        foreach (var child in directChildren)
        {
            if (child == null) continue;

            // Snapshot des "petits-enfants" (enfants du child)
            var grandChildren = new List<Transform>(child.childCount);
            for (int j = 0; j < child.childCount; j++)
                grandChildren.Add(child.GetChild(j));

            // On insère les petits-enfants à la place du child pour garder un ordre lisible
            int insertIndex = child.GetSiblingIndex();

            foreach (var gc in grandChildren)
            {
                if (gc == null) continue;
                // Reparent en conservant la pose monde (Undo gère l'opération)
                Undo.SetTransformParent(gc, parent, "Reparent grandchild");
                gc.SetSiblingIndex(insertIndex++);
            }

            // Supprime l'enfant direct devenu vide (ou non) — c’est le "shunt"
            Undo.DestroyObjectImmediate(child.gameObject);
        }

        Undo.CollapseUndoOperations(group);
    }
#endif
}
