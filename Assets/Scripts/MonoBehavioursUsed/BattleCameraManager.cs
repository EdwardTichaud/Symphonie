using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Gere l'activation des CinemachineCamera durant les combats.
/// Les transitions s'effectuent via <see cref="CinemachineBlendSwitcher"/>.
/// </summary>
public class BattleCameraManager : MonoBehaviour
{
    /// <summary>Acces global au gestionnaire de camera de combat.</summary>
    public static BattleCameraManager Instance { get; private set; }

    [Tooltip("Duree du blend entre deux cameras.")]
    [SerializeField] private float blendDuration = 0.5f;

    // Composant responsable du changement de camera via les priorites.
    private CinemachineBlendSwitcher blendSwitcher;

    // Ensemble des cameras Cinemachine disponibles pour les moves.
    private readonly List<CinemachineCamera> availableCameras = new();

    void Awake()
    {
        // Mise en place du singleton classique.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Recherche du CinemachineBlendSwitcher present dans la scene.
        blendSwitcher = FindFirstObjectByType<CinemachineBlendSwitcher>();
        if (!blendSwitcher)
            Debug.LogWarning("[BattleCameraManager] Aucun CinemachineBlendSwitcher trouve dans la scene.");

        // Recense toutes les CinemachineCamera presentes (angles speciaux).
        foreach (var cam in FindObjectsOfType<CinemachineCamera>())
        {
            if (cam != null)
                availableCameras.Add(cam);
        }

        // Au demarrage du combat on revient sur la camera principale taggee "BattleCamera".
        if (blendSwitcher)
            blendSwitcher.DisplayCamera(null, 0f);
    }

    /// <summary>
    /// Active la camera correspondant au nom fourni.
    /// - <c>null</c>  : retour a la camera de combat par defaut (tag "BattleCamera").
    /// - chaine vide : selection d'une camera aleatoire.
    /// </summary>
    /// <param name="cameraName">Nom de la camera souhaitee.</param>
    public void SwitchToCamera(string cameraName)
    {
        if (!blendSwitcher)
            return; // Impossible de switcher sans blendSwitcher

        // Cas 1 : aucun move/item en cours -> on revient sur la camera par defaut.
        if (cameraName == null)
        {
            blendSwitcher.DisplayCamera(null, blendDuration);
            return;
        }

        // Cas 2 : nom vide -> choix d'une camera aleatoire.
        if (string.IsNullOrWhiteSpace(cameraName))
        {
            if (availableCameras.Count > 0)
            {
                var randomCam = availableCameras[Random.Range(0, availableCameras.Count)];
                cameraName = randomCam.gameObject.name;
            }
            else
            {
                // Aucune camera disponible, on retombe sur la camera principale.
                blendSwitcher.DisplayCamera(null, blendDuration);
                return;
            }
        }

        // Affiche la camera demandee (transition assuree par le blend switcher).
        blendSwitcher.DisplayCamera(cameraName, blendDuration);
    }
}
