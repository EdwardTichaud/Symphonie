using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetCameraToCanvas : MonoBehaviour
{
    [Header("Permet de rechercher et d'affecter la caméra qui sera en charge")]
    [Header("d'afficher le gameObject de manière à être")]
    [Header("indépendant des continuités de scènes")]
    public string cameraToSetup = "uiCamera2D";

    [Header("Options de Sorting Layer")]
    public string sortingLayerName = "UI";  // Nom du Sorting Layer
    public int sortingLayerIndex;           // Numéro d'ordre dans le Sorting Layer

    private Canvas canvas;

    void Start()
    {
        canvas = GetComponent<Canvas>();
        SetCameraForCanvas(cameraToSetup);
        SetSortingLayer();
    }

    void SetCameraForCanvas(string cameraName)
    {
        Camera foundCam = null;

        // Étape 1 : Essaye GameObject.Find (dans la hiérarchie active uniquement)
        GameObject camObj = GameObject.Find(cameraName);
        if (camObj != null)
        {
            foundCam = camObj.GetComponent<Camera>();
        }

        // Étape 2 : Si pas trouvé, cherche dans toutes les scènes, y compris objets désactivés
        if (foundCam == null)
        {
            var allCameras = Resources.FindObjectsOfTypeAll<Camera>();
            foreach (Camera cam in allCameras)
            {
                if (cam.name == cameraName)
                {
                    foundCam = cam;
                    break;
                }
            }
        }

        if (foundCam == null)
        {
            Debug.LogWarning($"[SetCameraToCanvas] Caméra '{cameraName}' non trouvée sur l'objet {name}");
            return;
        }

        canvas.worldCamera = foundCam;
    }

    void SetSortingLayer()
    {
        if (!string.IsNullOrEmpty(sortingLayerName))
            canvas.sortingLayerName = sortingLayerName;
        else
            canvas.sortingLayerName = "UI";

        canvas.sortingOrder = sortingLayerIndex;
    }
}
