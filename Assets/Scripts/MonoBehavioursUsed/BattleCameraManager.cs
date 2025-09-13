using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

/// <summary>
/// Gère l'activation des différentes CinemachineVirtualCameras en combat.
/// Chaque move ou item peut spécifier le nom d'une caméra à activer.
/// </summary>
public class BattleCameraManager : MonoBehaviour
{
    public static BattleCameraManager Instance { get; private set; }

    [Tooltip("Nom de la caméra utilisée par défaut lorsque aucun nom n'est fourni.")]
    public string defaultCameraName = "BattleCam_Default";

    private readonly Dictionary<string, CinemachineVirtualCamera> cameras = new();
    private CinemachineVirtualCamera activeCamera;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Recherche toutes les caméras virtuelles enfants et les enregistre.
        foreach (var vcam in GetComponentsInChildren<CinemachineVirtualCamera>(true))
        {
            cameras[vcam.gameObject.name] = vcam;
            vcam.gameObject.SetActive(false); // Désactive toutes les caméras au départ
        }

        // Active la caméra par défaut si définie
        SwitchToCamera(defaultCameraName);
    }

    /// <summary>
    /// Active la caméra correspondant au nom fourni. Si aucun nom n'est trouvé,
    /// la caméra par défaut est réactivée.
    /// </summary>
    /// <param name="cameraName">Nom de la CinemachineVirtualCamera à afficher.</param>
    public void SwitchToCamera(string cameraName)
    {
        CinemachineVirtualCamera newCam = null;
        if (!string.IsNullOrEmpty(cameraName))
            cameras.TryGetValue(cameraName, out newCam);

        if (newCam == null && !string.IsNullOrEmpty(defaultCameraName))
            cameras.TryGetValue(defaultCameraName, out newCam);

        if (newCam == activeCamera)
            return;

        if (activeCamera != null)
            activeCamera.gameObject.SetActive(false);

        if (newCam != null)
        {
            newCam.gameObject.SetActive(true);
            activeCamera = newCam;
        }
    }
}
