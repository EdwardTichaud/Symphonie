#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Text.RegularExpressions;

[CustomEditor(typeof(RenameChildrenByFirst), true)]
[CanEditMultipleObjects]
public class RenameChildrenByFirstEditor : Editor
{
    SerializedProperty includeFirstProp;
    SerializedProperty preserveWidthProp;

    void OnEnable()
    {
        includeFirstProp = serializedObject.FindProperty("includeFirst");
        preserveWidthProp = serializedObject.FindProperty("preserveNumberWidth");
    }

    public override void OnInspectorGUI()
    {
        // Champs sérialisés => OK pour multi-objets (supprime le bandeau)
        serializedObject.Update();
        EditorGUILayout.PropertyField(includeFirstProp);
        EditorGUILayout.PropertyField(preserveWidthProp);
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Renommer enfants selon le premier", GUILayout.Height(26)))
            {
                // Applique à toutes les cibles sélectionnées ayant le composant
                foreach (var o in targets)
                {
                    var comp = o as RenameChildrenByFirst;
                    if (comp) RenameByFirst(comp.transform, comp.includeFirst, comp.preserveNumberWidth);
                }
            }
        }

        EditorGUILayout.HelpBox(
            "Renomme les enfants directs dans l'ordre en partant du nom du premier enfant (préfixe + numéro).",
            MessageType.Info);
    }

    // --- Logique outillée (même que précédemment, inlined ici pour être autonome) ---
    private static void RenameByFirst(Transform parent, bool includeFirst, bool preserveWidth)
    {
        if (!parent || parent.childCount == 0) return;

        Transform first = parent.GetChild(0);
        (string prefix, int number, int width) = ParseName(first.name);

        if (number < 0)
        {
            number = 1;
            width = preserveWidth ? 1 : 0;
            if (!prefix.EndsWith("_")) prefix += "_";
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName($"Rename Children By First: {parent.name}");
        int group = Undo.GetCurrentGroup();

        int index = number;
        int childCount = parent.childCount;

        for (int i = 0; i < childCount; i++)
        {
            var child = parent.GetChild(i);

            if (i == 0 && !includeFirst) { index++; continue; }

            string numStr = (preserveWidth && width > 0)
                ? index.ToString(new string('0', width))
                : index.ToString();

            string newName = prefix + numStr;

            if (child.name != newName)
            {
                Undo.RecordObject(child.gameObject, "Rename Child");
                child.name = newName;
                EditorUtility.SetDirty(child.gameObject);
            }

            index++;
        }

        Undo.CollapseUndoOperations(group);
    }

    private static (string prefix, int number, int width) ParseName(string name)
    {
        var m = Regex.Match(name, @"^(.*?)(\d+)$");
        if (m.Success) return (m.Groups[1].Value, int.Parse(m.Groups[2].Value), m.Groups[2].Value.Length);

        m = Regex.Match(name, @"^(.*?)(?:_|-|\s)(\d+)$");
        if (m.Success) return (m.Groups[1].Value + "_", int.Parse(m.Groups[2].Value), m.Groups[2].Value.Length);

        return (name, -1, 0);
    }
}
#endif
