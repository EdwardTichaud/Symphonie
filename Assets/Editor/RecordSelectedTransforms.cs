using UnityEditor;
using UnityEngine;
using UnityEditor.Animations;

public class RecordSelectedTransforms : MonoBehaviour
{
    [MenuItem("Tools/Record Transforms to Animation")]
    static void RecordTransformsToAnimation()
    {
        if (Selection.transforms.Length == 0)
        {
            Debug.LogWarning("No objects selected.");
            return;
        }

        var clip = Selection.activeObject as AnimationClip;
        if (clip == null)
        {
            Debug.LogWarning("Select an AnimationClip in the Project window.");
            return;
        }

        Undo.RegisterCompleteObjectUndo(clip, "Record Transforms");

        foreach (var tr in Selection.transforms)
        {
            string path = AnimationUtility.CalculateTransformPath(tr, null);

            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.x"), AnimationCurve.Constant(0, 0, tr.localPosition.x));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.y"), AnimationCurve.Constant(0, 0, tr.localPosition.y));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.z"), AnimationCurve.Constant(0, 0, tr.localPosition.z));

            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.x"), AnimationCurve.Constant(0, 0, tr.localRotation.x));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.y"), AnimationCurve.Constant(0, 0, tr.localRotation.y));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.z"), AnimationCurve.Constant(0, 0, tr.localRotation.z));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.w"), AnimationCurve.Constant(0, 0, tr.localRotation.w));

            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalScale.x"), AnimationCurve.Constant(0, 0, tr.localScale.x));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalScale.y"), AnimationCurve.Constant(0, 0, tr.localScale.y));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalScale.z"), AnimationCurve.Constant(0, 0, tr.localScale.z));
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Recorded {Selection.transforms.Length} transforms to {clip.name}");
    }
}
