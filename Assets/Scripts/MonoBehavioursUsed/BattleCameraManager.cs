using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

/// <summary>
/// Gère l'activation des différentes <see cref="CinemachineCamera"/> en combat.
/// Chaque move ou item peut spécifier le nom d'une caméra à activer.
/// </summary>
public class BattleCameraManager : MonoBehaviour
{
    public static BattleCameraManager Instance { get; private set; }

    [Tooltip("Nom de la caméra utilisée par défaut lorsque aucun nom n'est fourni.")]
    public string defaultCameraName = "BattleCam_Default";

    // Liste des caméras Cinemachine disponibles, indexées par leur nom de GameObject
    private readonly Dictionary<string, CinemachineCamera> cameras = new();
    // Caméra actuellement active dans la scène de combat
    private CinemachineCamera activeCamera;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Recherche toutes les caméras Cinemachine enfants et les enregistre.
        foreach (var vcam in GetComponentsInChildren<CinemachineCamera>(true))
        {
            cameras[vcam.gameObject.name] = vcam;
            // Désactive toutes les caméras au départ afin de garder la scène propre
            vcam.gameObject.SetActive(false);
        }

        // Active la caméra par défaut si définie
        SwitchToCamera(defaultCameraName);
    }

    /// <summary>
    /// Active la caméra correspondant au nom fourni. Si aucun nom n'est trouvé,
    /// la caméra par défaut est réactivée.
    /// </summary>
    /// <param name="cameraName">Nom de la <see cref="CinemachineCamera"/> à afficher.</param>
    public void SwitchToCamera(string cameraName)
    {
        // Récupère la caméra correspondant au nom demandé
        CinemachineCamera newCam = null;
        if (!string.IsNullOrEmpty(cameraName))
            cameras.TryGetValue(cameraName, out newCam);

        if (newCam == null && !string.IsNullOrEmpty(defaultCameraName))
            cameras.TryGetValue(defaultCameraName, out newCam);

        if (newCam == activeCamera)
            return;

        // Désactivation de l'ancienne caméra si elle existe
        if (activeCamera != null)
            activeCamera.gameObject.SetActive(false);

        // Activation de la nouvelle caméra
        if (newCam != null)
        {
            newCam.gameObject.SetActive(true);
            activeCamera = newCam;
        }
    }
}
