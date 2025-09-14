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

    [Tooltip("Duree par defaut du blend entre deux cameras (en secondes).")]
    [SerializeField] private float defaultBlendDuration = 1f;

    private readonly Dictionary<string, CinemachineCamera> _byName = new();
    private CinemachineCamera _current; // camera actuellement active

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

        // Active immediatement la camera d'indice 0 si elle existe afin
        // d'avoir une vue de combat par defaut pour les menus et le ciblage.
        ActivateDefaultCamera();
    }

    /// <summary>
    /// Active la <see cref="CinemachineCamera"/> nommee <paramref name="cameraName"/>.
    /// <para>Si <paramref name="cameraName"/> est <c>null</c>, la camera d'indice 0
    /// devient la camera par defaut.</para>
    /// <para>Si une chaine vide est passee, toutes les cameras sont desactivees
    /// pour revenir a la camera classique.</para>
    /// </summary>
    public void DisplayCamera(string cameraName)
    {
        // Redirige vers la surcharge avec duree explicite en utilisant
        // la duree de blend par defaut.
        DisplayCamera(cameraName, defaultBlendDuration);
    }

    /// <summary>
    /// Active la <see cref="CinemachineCamera"/> nommee <paramref name="cameraName"/>.
    /// </summary>
    /// <param name="cameraName">Nom de la camera a activer.</param>
    /// <param name="blendDuration">Duree du blend en secondes.</param>
    public void DisplayCamera(string cameraName, float blendDuration)
    {
        // Cas : retour a la camera par defaut (indice 0).
        if (cameraName == null)
        {
            if (brain)
                brain.DefaultBlend = new CinemachineBlendDefinition(blendStyle, Mathf.Max(0f, blendDuration));

            ActivateDefaultCamera();
            return;
        }

        // Cas : aucune camera Cinemachine souhaitee -> toutes inactives.
        if (string.IsNullOrEmpty(cameraName))
        {
            foreach (var c in cameras)
                c.Priority = inactivePriority;

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
    /// Active la camera placee a l'indice 0 de la liste <see cref="cameras"/>.
    /// Cette camera represente la vue de combat par defaut.
    /// </summary>
    private void ActivateDefaultCamera()
    {
        if (cameras == null || cameras.Count == 0)
            return; // aucune camera a activer

        foreach (var c in cameras)
            c.Priority = (c == cameras[0]) ? activePriority : inactivePriority;

        _current = cameras[0];
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
