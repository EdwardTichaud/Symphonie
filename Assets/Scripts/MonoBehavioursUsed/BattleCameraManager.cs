using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Gère l'activation des différentes <see cref="CinemachineCamera"/> en combat.
/// Chaque move ou item peut spécifier le nom d'une caméra à activer.
/// </summary>
public class BattleCameraManager : MonoBehaviour
{
    public static BattleCameraManager Instance { get; private set; }

    [Tooltip("Nom de la caméra utilisée par défaut lorsque aucun nom n'est fourni.")]
    public string defaultCameraName = "CinemachineCamera_0_BattleCamera";

    [Tooltip("Priorité utilisée par la caméra par défaut.")]
    public int defaultPriority = 10;

    [Tooltip("Priorité appliquée lorsqu'une caméra secondaire est active (MusicalMove/Item)." )]
    public int overridePriority = 20;

    // Priorité assignée aux caméras inactives pour permettre le blend via le CinemachineBrain
    private const int inactivePriority = 0;

    // Liste des caméras Cinemachine disponibles, indexées par leur nom de GameObject
    private readonly Dictionary<string, CinemachineCamera> cameras = new();
    // Caméra de combat par défaut, toujours présente dans la scène
    private CinemachineCamera defaultCamera;
    // Caméra actuellement prioritaire dans la scène de combat
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
        // Elles restent actives pour permettre un fondu entre elles.
        foreach (var vcam in GetComponentsInChildren<CinemachineCamera>(true))
        {
            cameras[vcam.gameObject.name] = vcam;
            vcam.gameObject.SetActive(true);        // garder actives pour le blend
            vcam.Priority = inactivePriority;        // priorité minimale par défaut
        }

        // Récupère la caméra par défaut et lui assigne sa priorité initiale
        if (cameras.TryGetValue(defaultCameraName, out defaultCamera))
        {
            defaultCamera.Priority = defaultPriority;
            activeCamera = defaultCamera;
        }
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

        // Si aucune caméra spécifique n'est trouvée, on retombe sur la caméra par défaut
        if (newCam == null)
            newCam = defaultCamera;

        // Rien à faire si la caméra demandée est déjà active
        if (newCam == activeCamera)
            return;

        // Rétablit la priorité de l'ancienne caméra (si ce n'est pas la caméra par défaut)
        if (activeCamera != null && activeCamera != defaultCamera)
            activeCamera.Priority = inactivePriority;

        // Affecte la priorité appropriée à la nouvelle caméra
        if (newCam != null)
        {
            newCam.Priority = (newCam == defaultCamera) ? defaultPriority : overridePriority;
            activeCamera = newCam;                    // devient la caméra actuellement prioritaire
        }
    }
}
