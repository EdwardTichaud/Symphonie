using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BattleEventTrigger))]
public class BattleEventTriggerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "Surveille les unites configurees et declenche un motif camera, une animation, un audio clip "
            + "ou une timeline lorsqu'un seuil est atteint. Chaque entree de \"Unites et seuils\" peut cibler "
            + "une unite (HP Threshold, LastStandUnit) ou un comportement global "
            + "(LastStandEnemy, LastStandAllUnits). LastStandAllUnits ignore UnitData et declenche pour chaque "
            + "unite seule de son camp; la timeline est ignoree pour ce mode. Si audioClip est vide sur un "
            + "LastStand, le CharacterData.lastStandAudioClip de l'unite est utilise.",
            MessageType.Info);

        DrawDefaultInspector();
    }
}
