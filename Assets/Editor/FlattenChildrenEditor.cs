#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FlattenChildren))]
[CanEditMultipleObjects]
public class FlattenChildrenEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Shunte les enfants directs : reparent leurs enfants au GameObject courant, puis supprime ces enfants directs.",
            MessageType.Info);

        if (GUILayout.Button("Shunter enfants directs (garder leurs enfants)"))
        {
            foreach (Object t in targets)
            {
                var comp = t as FlattenChildren;
                if (comp != null)
                    FlattenChildren.Flatten(comp.transform);
            }
        }
    }

    [MenuItem("GameObject/Symphonie/Flatten Direct Children (keep grandchildren)", false, 0)]
    private static void FlattenFromMenu()
    {
        var selection = Selection.transforms;
        if (selection == null || selection.Length == 0) return;

        foreach (var tr in selection)
        {
            FlattenChildren.Flatten(tr);
        }
    }
}
#endif
