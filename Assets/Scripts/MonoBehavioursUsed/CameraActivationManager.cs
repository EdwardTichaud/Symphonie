using System.Collections;
using UnityEngine;

/// <summary>
/// Gère l'activation des différentes caméras du jeu afin d'éviter qu'elles soient
/// toutes actives simultanément, ce qui provoquerait une chute de FPS.
/// </summary>
public class CameraActivationManager : MonoBehaviour
{
    public static CameraActivationManager Instance { get; private set; }

    private Camera worldCamera;   // Référence à la WorldCam_Cam
    private Camera battleCamera;  // Référence à la BattleCam_Cam
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
        battleCamera = GameObject.FindGameObjectWithTag("BattleCamera")?.GetComponent<Camera>();
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
    /// </summary>
    private void SetCameraState(Camera cam, bool state)
    {
        if (cam != null)
            cam.gameObject.SetActive(state);
    }
}

