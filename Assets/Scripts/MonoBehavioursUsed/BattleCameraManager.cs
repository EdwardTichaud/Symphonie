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
        // On force une transition immediate (duree 0) pour eviter un fondu au lancement.
        if (blendSwitcher)
            blendSwitcher.DisplayCamera(null, 0f);
    }

    /// <summary>
    /// Active la camera correspondant au nom fourni.
    /// - <c>null</c>  : retour a la camera de combat par defaut (tag "BattleCamera").
    /// - chaine vide : selection d'une camera aleatoire.
    /// </summary>
    /// <param name="cameraName">Nom de la camera souhaitee.</param>
    /// <param name="blendTime">
    /// Duree du fondu en secondes. Utiliser une valeur negative pour conserver
    /// la duree definie dans le <see cref="CinemachineBlendSwitcher"/>.
    /// </param>
    public void SwitchToCamera(string cameraName, float blendTime = -1f)
    {
        if (!blendSwitcher)
            return; // Impossible de switcher sans blendSwitcher

        // Cas 1 : aucun move/item en cours -> on revient sur la camera par defaut.
        if (cameraName == null)
        {
            if (blendTime >= 0f)
                blendSwitcher.DisplayCamera(null, blendTime); // Transition forcee
            else
                blendSwitcher.DisplayCamera(null); // Duree par defaut
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
                // Si aucune camera speciale n'est disponible, on retourne sur la camera
                // principale avec la duree de blend souhaitee ou celle par defaut.
                if (blendTime >= 0f)
                    blendSwitcher.DisplayCamera(null, blendTime);
                else
                    blendSwitcher.DisplayCamera(null);
                return;
            }
        }

        // Affiche la camera demandee avec la duree de blend appropriee.
        if (blendTime >= 0f)
            blendSwitcher.DisplayCamera(cameraName, blendTime);
        else
            blendSwitcher.DisplayCamera(cameraName);
    }
}
