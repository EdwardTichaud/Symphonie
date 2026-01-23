using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public static class TimelineTools
{
    private const string TemplatePrefabPath = "Assets/Timelines/Timeline_RunToTheDream.prefab";
    private const string DefaultPrefabPath = "Assets/Timelines/Timeline_New.prefab";

    [MenuItem("Tools/Symphonie/Timeline/Create Prefab From Template")]
    private static void CreatePrefabFromTemplate()
    {
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefabPath);
        if (template == null)
        {
            Debug.LogError($"[TimelineTools] Template prefab not found at {TemplatePrefabPath}.");
            return;
        }

        string targetPath = AssetDatabase.GenerateUniqueAssetPath(DefaultPrefabPath);
        if (!AssetDatabase.CopyAsset(TemplatePrefabPath, targetPath))
        {
            Debug.LogError($"[TimelineTools] Failed to copy template to {targetPath}.");
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        UnityEngine.Object created = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
        Selection.activeObject = created;
        EditorGUIUtility.PingObject(created);
        Debug.Log($"[TimelineTools] Created timeline prefab: {targetPath}");
    }

    [MenuItem("Tools/Symphonie/Timeline/Create Scene Instance From Template")]
    private static void CreateSceneInstanceFromTemplate()
    {
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefabPath);
        if (template == null)
        {
            Debug.LogError($"[TimelineTools] Template prefab not found at {TemplatePrefabPath}.");
            return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(template) as GameObject;
        if (instance == null)
        {
            Debug.LogError("[TimelineTools] Failed to instantiate timeline template.");
            return;
        }

        Undo.RegisterCreatedObjectUndo(instance, "Create Timeline Instance");
        Selection.activeGameObject = instance;
        Debug.Log("[TimelineTools] Timeline instance created in the active scene.");
    }

    [MenuItem("Tools/Symphonie/Timeline/Validate Scene Timelines")]
    private static void ValidateSceneTimelines()
    {
        PlayableDirector[] directors = UnityEngine.Object.FindObjectsByType<PlayableDirector>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int missingReceiver = 0;
        int missingBindings = 0;

        foreach (PlayableDirector director in directors)
        {
            if (director == null)
                continue;

            GameObject go = director.gameObject;
            bool hasReceiver = go.GetComponent<SignalReceiver>() != null;
            bool hasBindings = go.GetComponent<SetTimelineBidings>() != null;

            if (!hasReceiver || !hasBindings)
            {
                Debug.LogWarning($"[TimelineTools] {go.name} in scene '{go.scene.name}' " +
                                 $"missing: {(hasReceiver ? "" : "SignalReceiver ")}" +
                                 $"{(hasBindings ? "" : "SetTimelineBidings")}", go);
            }

            if (!hasReceiver)
                missingReceiver++;
            if (!hasBindings)
                missingBindings++;
        }

        Debug.Log($"[TimelineTools] Scene scan complete. Directors: {directors.Length}, " +
                  $"missing SignalReceiver: {missingReceiver}, missing SetTimelineBidings: {missingBindings}.");
    }

    [MenuItem("Tools/Symphonie/Timeline/Validate Prefabs In Assets/Timelines")]
    private static void ValidateTimelinePrefabs()
    {
        string[] folders = { "Assets/Timelines" };
        string[] guids = AssetDatabase.FindAssets("t:Prefab", folders);

        int prefabCount = 0;
        int directorCount = 0;
        int missingReceiver = 0;
        int missingBindings = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;

            prefabCount++;
            PlayableDirector[] directors = prefab.GetComponentsInChildren<PlayableDirector>(true);
            if (directors.Length == 0)
                continue;

            directorCount += directors.Length;

            foreach (PlayableDirector director in directors)
            {
                GameObject go = director.gameObject;
                bool hasReceiver = go.GetComponent<SignalReceiver>() != null;
                bool hasBindings = go.GetComponent<SetTimelineBidings>() != null;

                if (!hasReceiver || !hasBindings)
                {
                    Debug.LogWarning($"[TimelineTools] Prefab '{path}' object '{go.name}' " +
                                     $"missing: {(hasReceiver ? "" : "SignalReceiver ")}" +
                                     $"{(hasBindings ? "" : "SetTimelineBidings")}", prefab);
                }

                if (!hasReceiver)
                    missingReceiver++;
                if (!hasBindings)
                    missingBindings++;
            }
        }

        Debug.Log($"[TimelineTools] Prefab scan complete in Assets/Timelines. " +
                  $"Prefabs: {prefabCount}, directors: {directorCount}, " +
                  $"missing SignalReceiver: {missingReceiver}, missing SetTimelineBidings: {missingBindings}.");
    }
}
