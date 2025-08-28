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

        // Les animations modifient la position et la rotation locales du modèle.
        // On stocke ces valeurs afin de les appliquer au parent dans l'espace monde.
        Vector3 localDeltaPosition = animatedModel.localPosition;
        Quaternion localDeltaRotation = animatedModel.localRotation;

        // Application du déplacement : on convertit le déplacement local en espace monde
        // à l'aide de la rotation actuelle du parent, puis on l'ajoute à sa position.
        transform.position += transform.rotation * localDeltaPosition;

        // Application de la rotation : multiplication directe par la rotation locale obtenue.
        transform.rotation *= localDeltaRotation;

        // Réinitialisation du modèle pour conserver un offset nul par rapport au parent
        // et éviter toute dérive au fil des frames.
        animatedModel.localPosition = Vector3.zero;
        animatedModel.localRotation = Quaternion.identity;
    }
}
