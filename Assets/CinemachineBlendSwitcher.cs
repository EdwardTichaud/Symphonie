using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Permet de changer de CinemachineCamera en ajustant les priorites.
/// </summary>
[DisallowMultipleComponent]
public class CinemachineBlendSwitcher : MonoBehaviour
{
    [Header("Brain (auto si vide)")]
    [SerializeField] private CinemachineBrain brain;

    [Header("Cameras (auto-scan si vide)")]
    [SerializeField] private List<CinemachineCamera> cameras = new();

    [Header("Priorites")]
    [SerializeField] private int activePriority = 100;
    [SerializeField] private int inactivePriority = 10;

    [Header("Style de blend par defaut")]
    [SerializeField]
    private CinemachineBlendDefinition.Styles blendStyle =
        CinemachineBlendDefinition.Styles.EaseInOut;

    private readonly Dictionary<string, CinemachineCamera> _byName = new();
    private CinemachineCamera _current;

    void Awake()
    {
        // Recuperation du CinemachineBrain sur la camera de rendu.
        if (!brain && Camera.main) brain = Camera.main.GetComponent<CinemachineBrain>();
        if (!brain) Debug.LogWarning("[BlendSwitcher] Aucun CinemachineBrain trouve sur la camera de rendu.");

        // Collecte automatique des CinemachineCamera si la liste est vide.
        if (cameras == null || cameras.Count == 0)
            cameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None).ToList();

        cameras = cameras.Where(c => c != null).Distinct().ToList();

        // Construction du dictionnaire d'acces rapide par nom.
        _byName.Clear();
        foreach (var c in cameras)
        {
            var key = c.gameObject.name;
            if (!_byName.ContainsKey(key)) _byName.Add(key, c);
        }

        // Toutes les cameras commencent avec une priorite inactive.
        foreach (var c in cameras) c.Priority = inactivePriority;
    }

    /// <summary>
    /// Active la CinemachineCamera nommee <paramref name="cameraName"/>.
    /// Si <paramref name="cameraName"/> est null ou vide,
    /// toutes les CinemachineCamera sont desactivees pour revenir
    /// a la camera classique taggee "BattleCamera".
    /// </summary>
    public void DisplayCamera(string cameraName, float blendDuration)
    {
        // Cas : aucune camera Cinemachine active souhaitee.
        if (string.IsNullOrEmpty(cameraName))
        {
            foreach (var c in cameras)
                c.Priority = inactivePriority; // toutes perdent la main

            _current = null;
            return;
        }

        // Recherche de la camera a activer.
        if (!_byName.TryGetValue(cameraName, out var next))
        {
            RebuildMap();
            if (!_byName.TryGetValue(cameraName, out next))
            {
                Debug.LogWarning($"[BlendSwitcher] Aucune CinemachineCamera trouvee: {cameraName}");
                return;
            }
        }

        if (_current == next) return;

        // Definition du blend par defaut juste avant le switch.
        if (brain)
            brain.DefaultBlend = new CinemachineBlendDefinition(blendStyle, Mathf.Max(0f, blendDuration));

        // Gestion des priorites : seule la camera cible obtient la priorite active.
        foreach (var c in cameras)
            c.Priority = (c == next) ? activePriority : inactivePriority;

        _current = next;
    }

    /// <summary>
    /// Reconstruit la liste et le dictionnaire des cameras disponibles.
    /// </summary>
    public void RebuildMap()
    {
        cameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None)
            .Where(c => c != null).Distinct().ToList();
        _byName.Clear();
        foreach (var c in cameras) _byName[c.gameObject.name] = c;
    }
}
