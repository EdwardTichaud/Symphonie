using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ApplyMaterialToChildren))]
public class ApplyMaterialToChildrenEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(6);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Apply Now", GUILayout.Height(28)))
            {
                var comp = (ApplyMaterialToChildren)target;
                var renderers = comp.GetComponentsInChildren<Renderer>(comp.includeInactive);

                Undo.RecordObjects(renderers, "Apply Material To Children");
                comp.ApplyNow();

                foreach (var r in renderers)
                    if (r) EditorUtility.SetDirty(r);
            }

            if (GUILayout.Button("Select Child Renderers", GUILayout.Height(28)))
            {
                var comp = (ApplyMaterialToChildren)target;
                var list = new List<Object>(comp.GetComponentsInChildren<Renderer>(comp.includeInactive));
                Selection.objects = list.ToArray();
            }
        }
    }

    [MenuItem("Tools/Materials/Apply To Selection")]
    private static void ApplyToSelection()
    {
        foreach (var obj in Selection.gameObjects)
        {
            var comp = obj.GetComponent<ApplyMaterialToChildren>();
            if (!comp) continue;

            var renderers = comp.GetComponentsInChildren<Renderer>(comp.includeInactive);
            Undo.RecordObjects(renderers, "Apply Material To Children");
            comp.ApplyNow();

            foreach (var r in renderers)
                if (r) EditorUtility.SetDirty(r);
        }
    }
}
