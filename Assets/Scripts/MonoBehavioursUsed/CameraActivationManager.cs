using System.Collections;
using UnityEngine;
using Cinemachine; // Pour la gestion de la BattleCamera Cinemachine

/// <summary>
/// Gère l'activation des différentes caméras du jeu afin d'éviter qu'elles soient
/// toutes actives simultanément, ce qui provoquerait une chute de FPS.
/// </summary>
public class CameraActivationManager : MonoBehaviour
{
    public static CameraActivationManager Instance { get; private set; }

    private Camera worldCamera;   // Référence à la WorldCam_Cam
    private CinemachineCamera battleCamera;  // Référence à la BattleCam_Cam
    private Camera versusCamera;  // Référence à la VersusCam_Cam

    /// <summary>
    /// Création automatique du gestionnaire après le chargement de la scène.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateInstance()
    {
        if (Instance == null)
        {
            GameObject manager = new GameObject("CameraActivationManager");
            manager.AddComponent<CameraActivationManager>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Recherche des caméras par leurs tags respectifs
        worldCamera = GameObject.FindGameObjectWithTag("WorldCamera")?.GetComponent<Camera>();
        // Le tag "BattleCamera" pointe désormais vers un CinemachineCamera
        battleCamera = GameObject.FindGameObjectWithTag("BattleCamera")?.GetComponent<CinemachineCamera>();
        versusCamera = GameObject.FindGameObjectWithTag("VersusCamera")?.GetComponent<Camera>();

        ActivateWorldCamera(); // État par défaut : exploration
    }

    /// <summary>
    /// Active uniquement la WorldCamera pour l'exploration.
    /// </summary>
    public void ActivateWorldCamera()
    {
        SetCameraState(worldCamera, true);
        SetCameraState(battleCamera, false);
        SetCameraState(versusCamera, false);
    }

    /// <summary>
    /// Active la BattleCamera et la VersusCamera simultanément (début d'un combat).
    /// </summary>
    public void ActivateBattleAndVersusCameras()
    {
        SetCameraState(worldCamera, false);
        SetCameraState(battleCamera, true);
        SetCameraState(versusCamera, true);
    }

    /// <summary>
    /// Active uniquement la BattleCamera (Versus terminé).
    /// </summary>
    public void ActivateBattleCamera()
    {
        SetCameraState(worldCamera, false);
        SetCameraState(battleCamera, true);
        SetCameraState(versusCamera, false);
    }

    /// <summary>
    /// Désactive la VersusCamera après la fin d'une animation donnée.
    /// </summary>
    public IEnumerator DisableVersusAfterAnimation(Animator animator)
    {
        if (animator == null)
            yield break;

        // Attente d'une frame pour être sûr que l'animation a bien démarré
        yield return null;
        float duration = animator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(duration);
        ActivateBattleCamera();
    }

    /// <summary>
    /// Active ou désactive proprement une caméra si elle existe.
    /// Cette méthode s'assure également que la WorldCamera et la BattleCamera
    /// ne puissent jamais être actives en même temps afin d'éviter des rendus
    /// multiples coûteux.
    /// </summary>
    // Utilise Behaviour pour accepter aussi bien Camera que CinemachineCamera
    private void SetCameraState(Behaviour cam, bool state)
    {
        if (cam == null)
            return;

        // Active ou désactive directement le GameObject de la caméra ciblée
        cam.gameObject.SetActive(state);

        if (!state)
            return;

        // Si on active la WorldCamera, on force la BattleCamera à s'éteindre
        if (cam == worldCamera && battleCamera != null)
            battleCamera.gameObject.SetActive(false);

        // Si on active la BattleCamera, on force la WorldCamera à s'éteindre
        if (cam == battleCamera && worldCamera != null)
            worldCamera.gameObject.SetActive(false);
    }
}

