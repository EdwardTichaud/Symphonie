using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Gère l'activation des différentes <see cref="CinemachineCamera"/> en combat.
/// Utilise <see cref="CinemachineBlendSwitcher"/> pour assurer des transitions fluides
/// entre les angles de caméra.
/// Chaque <c>MusicalMove</c> ou <c>Item</c> peut spécifier le nom d'une caméra à activer.
/// </summary>
public class BattleCameraManager : MonoBehaviour
{
    /// <summary>Accès global au gestionnaire de caméra de combat.</summary>
    public static BattleCameraManager Instance { get; private set; }

    [Tooltip("Durée du blend entre deux caméras.")]
    [SerializeField] private float blendDuration = 0.5f;

    // Composant responsable des priorités et du blend entre caméras
    private CinemachineBlendSwitcher blendSwitcher;

    // Caméra principale de combat (taguée "BattleCamera")
    private CinemachineCamera battleCamera;

    // Collection de toutes les autres caméras disponibles pour le choix aléatoire
    private readonly List<CinemachineCamera> otherCameras = new();

    void Awake()
    {
        // Mise en place du singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Recherche du CinemachineBlendSwitcher présent dans la scène
        blendSwitcher = FindObjectOfType<CinemachineBlendSwitcher>();
        if (!blendSwitcher)
            Debug.LogWarning("[BattleCameraManager] Aucun CinemachineBlendSwitcher trouvé dans la scène.");

        // Récupération de la caméra principale via son tag
        battleCamera = GameObject.FindGameObjectWithTag("BattleCamera")?.GetComponent<CinemachineCamera>();
        if (!battleCamera)
            Debug.LogWarning("[BattleCameraManager] Caméra principale introuvable (tag BattleCamera).");

        // Constitution de la liste des autres caméras pour les sélections aléatoires
        foreach (var cam in FindObjectsOfType<CinemachineCamera>())
        {
            if (cam != null && cam != battleCamera)
                otherCameras.Add(cam);
        }

        // S'assure que la caméra principale est affichée au démarrage
        if (blendSwitcher && battleCamera)
            blendSwitcher.DisplayCamera(battleCamera.gameObject.name, 0f);
    }

    /// <summary>
    /// Active la caméra correspondant au nom fourni.
    /// - <c>null</c>  : retour à la caméra de combat principale.
    /// - chaîne vide : sélection d'une caméra aléatoire.
    /// </summary>
    /// <param name="cameraName">Nom de la caméra souhaitée.</param>
    public void SwitchToCamera(string cameraName)
    {
        if (!blendSwitcher)
            return; // Impossible de switcher sans blendSwitcher

        // Cas 1 : aucun move/item en cours ➜ on revient à la BattleCamera
        if (cameraName == null)
        {
            if (battleCamera)
                blendSwitcher.DisplayCamera(battleCamera.gameObject.name, blendDuration);
            return;
        }

        // Cas 2 : nom vide ➜ choix d'une caméra aléatoire
        if (string.IsNullOrWhiteSpace(cameraName))
        {
            if (otherCameras.Count > 0)
            {
                var randomCam = otherCameras[Random.Range(0, otherCameras.Count)];
                cameraName = randomCam.gameObject.name;
            }
            else if (battleCamera)
            {
                // Aucun autre angle disponible, on retombe sur la caméra principale
                cameraName = battleCamera.gameObject.name;
            }
        }

        // Affiche la caméra demandée (ou la BattleCamera si tout a échoué)
        blendSwitcher.DisplayCamera(cameraName, blendDuration);
    }
}
