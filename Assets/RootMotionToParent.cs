using UnityEngine;

public class RootMotionToParent : MonoBehaviour
{
    public CharacterController parentController; // réf. vers le CC du parent
    public Transform parentTransform;            // réf. du parent (rotation)
    public float gravity = -9.81f;

    Animator anim;
    float verticalVel;

    void Awake()
    {
        anim = GetComponent<Animator>();
        if (!parentTransform && parentController) parentTransform = parentController.transform;
    }

    void OnAnimatorMove()
    {
        // IMPORTANT : ce callback est sur l’OBJET QUI PORTE L’ANIMATOR
        if (!anim || !anim.applyRootMotion || parentController == null) return;

        // Delta issu de la clip
        Vector3 delta = anim.deltaPosition;

        // Gravité (optionnelle si tu la gères ailleurs)
        if (parentController.isGrounded && verticalVel < 0f) verticalVel = -2f;
        verticalVel += gravity * Time.deltaTime;
        delta.y = verticalVel * Time.deltaTime;

        // Appliquer le déplacement au PARENT (objet gameplay)
        parentController.Move(delta);

        // Appliquer la rotation issue de la root motion au parent
        parentTransform.rotation *= anim.deltaRotation;

        // Remarque : comme OnAnimatorMove est présent ici, Unity n’applique PAS
        // automatiquement la root motion à l’enfant — pas de “double déplacement”.
    }
}
