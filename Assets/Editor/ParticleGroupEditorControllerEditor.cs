using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ParticleGroupEditorController))]
public class ParticleGroupEditorControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ParticleGroupEditorController controller = (ParticleGroupEditorController)target;

        GUILayout.Space(10);

        if (!Application.isPlaying)
        {
            if (GUILayout.Button("▶️ Simuler"))
            {
                controller.StartEditorSimulation();
            }

            if (GUILayout.Button("⏹ Réinitialiser"))
            {
                controller.ResetParticles();
            }
        }
    }
}
