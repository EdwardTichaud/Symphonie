using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class LevelName : MonoBehaviour
{
    public TextMeshProUGUI sceneNameText;
    public TextMeshProUGUI sceneDescriptionText;

    // Créez un dictionnaire pour stocker les descriptions des scènes
    private Dictionary<string, string> sceneDescriptions = new Dictionary<string, string>();

    void Start()
    {
        SetLevelName();
        DisplayLevelName();
    }

    public void SetLevelName()
    {
        // Ajoutez des descriptions pour chaque scène (ajoutez autant d'entrées que nécessaire)
        sceneDescriptions.Add("Yggdrassil", "L'arbre Monde");
        sceneDescriptions.Add("Port de la tour", "Le hameau paisible");
        sceneDescriptions.Add("Mer d'Iluvilirae", "Territoire du serpent de mer légendaire");
        sceneDescriptions.Add("Château de l'Eternel", "Demeure ancestrale du créateur");
        sceneDescriptions.Add("Arène de Lilith", "Chambre de l'effroi");
        sceneDescriptions.Add("Enfer", "La place des damnés");

        // Récupère le nom de la scène actuelle
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // Vérifie si la scène actuelle a une description associée dans le dictionnaire
        if (sceneDescriptions.ContainsKey(currentSceneName))
        {
            sceneNameText.text = currentSceneName;
            sceneDescriptionText.text = sceneDescriptions[currentSceneName];
        }
        else
        {
            sceneNameText.text = currentSceneName;
        }
    }

    public void DisplayLevelName()
    {
        GetComponent<Animator>().SetTrigger("On");
    }
}
