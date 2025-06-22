using UnityEngine;

public class EventSignalReceiver : MonoBehaviour
{
    [Header("Player Move Settings")]
    public float moveSpeed = 1.8f;

    private Transform playerTransform;

    private Vector3 moveDirection = Vector3.zero;

    private void Start()
    {
        // Cache le joueur pour �viter le Find() en boucle
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    private void Update()
    {
        if (playerTransform != null && moveDirection != Vector3.zero)
        {
            // D�place dans l'espace local du joueur (avant, arri�re, c�t�)
            Vector3 localMove = playerTransform.TransformDirection(moveDirection);
            playerTransform.position += localMove * moveSpeed * Time.deltaTime;
        }
    }

    public void MoveForward()
    {
        moveDirection = Vector3.forward;
    }

    public void MoveBack()
    {
        moveDirection = Vector3.back;
    }

    public void MoveRightSide()
    {
        moveDirection = Vector3.right;
    }

    public void MoveLeftSide()
    {
        moveDirection = Vector3.left;
    }

    public void StopMove()
    {
        moveDirection = Vector3.zero;
    }

    public void StartCameraSequence(CameraPath path)
    {
        if (path == null)
        {
            Debug.LogWarning("[EventSignalReceiver] CameraPath non fourni !");
            return;
        }

        CameraController.Instance.StartPathFollow(path, path.cameraTag);
    }

    public void ExpandPlayerDetecttion()
    {
        if (playerTransform != null)
        {
            PlayerDetection playerDetection = playerTransform.GetComponentInChildren<PlayerDetection>();
            if (playerDetection != null)
            {
                playerDetection.currentDetectionRadius += 10f;
            }
            else
            {
                Debug.LogWarning("[EventSignalReceiver] PlayerDetection non trouv� sur le joueur !");
            }
        }
        else
        {
            Debug.LogWarning("[EventSignalReceiver] Joueur non trouv� !");
        }
    }
}
