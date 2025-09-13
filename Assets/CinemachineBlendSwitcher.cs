using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Cinemachine;

[DisallowMultipleComponent]
public class CinemachineBlendSwitcher : MonoBehaviour
{
    [Header("Brain (auto si vide)")]
    [SerializeField] private CinemachineBrain brain;

    [Header("Caméras (auto-scan si vide)")]
    [SerializeField] private List<CinemachineCamera> cameras = new();

    [Header("Priorités")]
    [SerializeField] private int activePriority = 100;
    [SerializeField] private int inactivePriority = 10;

    [Header("Style de blend par défaut")]
    [SerializeField]
    private CinemachineBlendDefinition.Styles blendStyle =
        CinemachineBlendDefinition.Styles.EaseInOut;

    private readonly Dictionary<string, CinemachineCamera> _byName = new();
    private CinemachineCamera _current;

    void Awake()
    {
        // Brain
        if (!brain && Camera.main) brain = Camera.main.GetComponent<CinemachineBrain>();
        if (!brain) Debug.LogWarning("[BlendSwitcher] Aucun CinemachineBrain trouvé sur la caméra de rendu.");

        // Collecte des CmCameras
        if (cameras == null || cameras.Count == 0)
            cameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None).ToList();

        cameras = cameras.Where(c => c != null).Distinct().ToList();

        _byName.Clear();
        foreach (var c in cameras)
        {
            var key = c.gameObject.name;
            if (!_byName.ContainsKey(key)) _byName.Add(key, c);
        }

        foreach (var c in cameras) c.Priority = inactivePriority;
    }

    /// <summary>
    /// Active la CinemachineCamera nommée "cameraName" avec une durée de blend "blendDuration".
    /// </summary>
    public void DisplayCamera(string cameraName, float blendDuration)
    {
        if (string.IsNullOrEmpty(cameraName))
        {
            Debug.LogWarning("[BlendSwitcher] cameraName vide.");
            return;
        }

        if (!_byName.TryGetValue(cameraName, out var next))
        {
            RebuildMap();
            if (!_byName.TryGetValue(cameraName, out next))
            {
                Debug.LogWarning($"[BlendSwitcher] Aucune CinemachineCamera trouvée: {cameraName}");
                return;
            }
        }

        if (_current == next) return;

        // Définir le blend par défaut juste avant le switch
        if (brain)
            brain.DefaultBlend = new CinemachineBlendDefinition(blendStyle, Mathf.Max(0f, blendDuration));

        // Priorités : la plus haute sera prise par le Brain et blended depuis l’actuelle
        foreach (var c in cameras)
            c.Priority = (c == next) ? activePriority : inactivePriority;

        _current = next;
    }

    public void RebuildMap()
    {
        cameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None)
            .Where(c => c != null).Distinct().ToList();
        _byName.Clear();
        foreach (var c in cameras) _byName[c.gameObject.name] = c;
    }
}
