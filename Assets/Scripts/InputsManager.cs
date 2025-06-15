using UnityEngine;
using UnityEngine.InputSystem;
using UnityEditor;

public class InputsManager : MonoBehaviour
{
    public static InputsManager Instance { get; private set; }
    public PlayerInputs playerInputs;

    private InputActionMap[] allMaps;

    void Awake()
    {
        playerInputs = new PlayerInputs();

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        allMaps = new[]
        {
            playerInputs.Player.Get(),
            playerInputs.Inventory.Get(),
            playerInputs.Battle.Get(),
            playerInputs.Munin.Get(),
            playerInputs.InfoBox.Get()
        };
    }

    void Start()
    {
        ActivateOnly(playerInputs.Player.Get());
    }

    public void ActivateOnly(params InputActionMap[] mapsToEnable)
    {
        // 1) on désactive tout
        foreach (var m in allMaps)
            m.Disable();

        // 2) on ré-active le sous-ensemble voulu
        foreach (var m in mapsToEnable)
            m.Enable();
    }
}

[CustomEditor(typeof(InputsManager))]
[CanEditMultipleObjects]
public class InputsManagerEditor : Editor
{
    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        // Rafraîchit l'Inspector en temps réel
        Repaint();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Affiche l'inspecteur par défaut
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.LabelField("🎮 Input Action Maps Status", EditorStyles.boldLabel);

            // Pour chaque instance sélectionnée
            foreach (var obj in targets)
            {
                var mgr = obj as InputsManager;
                if (mgr == null) continue;

                EditorGUILayout.LabelField($"-- {mgr.gameObject.name} --", EditorStyles.miniBoldLabel);
                DrawMapStatus("Player", mgr.playerInputs.Player.Get());
                DrawMapStatus("Inventory", mgr.playerInputs.Inventory.Get());
                DrawMapStatus("Battle", mgr.playerInputs.Battle.Get());
                DrawMapStatus("Munin", mgr.playerInputs.Munin.Get());
                DrawMapStatus("InfoBox", mgr.playerInputs.InfoBox.Get());
                EditorGUILayout.Space();
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Passez en Play Mode pour voir l'état des Input Action Maps.",
                MessageType.Info
            );
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawMapStatus(string label, InputActionMap map)
    {
        bool isEnabled = map.enabled;
        string statusText = isEnabled ? "Enabled" : "Disabled";

        var style = new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = isEnabled ? Color.green : Color.red }
        };

        EditorGUILayout.LabelField(label, statusText, style);
    }
}