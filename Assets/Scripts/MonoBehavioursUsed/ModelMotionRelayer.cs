using UnityEngine;

/// <summary>
/// Transfère le déplacement et la rotation appliqués par l'animation du modèle
/// enfant vers le GameObject parent. Permet de garder la continuité du
/// déplacement lorsque l'animation contient du mouvement.
/// </summary>
public class ModelMotionRelayer : MonoBehaviour
{
    [Tooltip("Modèle animé dont le mouvement doit être reporté sur le parent.")]
    public Transform animatedModel; // Référence vers le modèle enfant animé.

    void LateUpdate()
    {
        if (animatedModel == null) return; // Sécurité si la référence n'est pas renseignée.

        // Calcul de la différence de position et de rotation générée par l'animation.
        Vector3 deltaPosition = animatedModel.position - transform.position;
        Quaternion deltaRotation = animatedModel.rotation * Quaternion.Inverse(transform.rotation);

        // Application du déplacement et de la rotation au parent.
        transform.position += deltaPosition;
        transform.rotation = deltaRotation * transform.rotation;

        // Réinitialisation du modèle pour conserver un offset nul par rapport au parent.
        animatedModel.localPosition = Vector3.zero;
        animatedModel.localRotation = Quaternion.identity;
    }
}
