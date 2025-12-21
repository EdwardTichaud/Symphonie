using UnityEngine;

namespace Symphonie.Movement
{
    /// <summary>
    /// Applique le Root Motion de l'Animator sur le Transform ou le CharacterController
    /// tout en gérant la gravité. Utilisé lorsque l'autorité est confiée à l'animation
    /// (exploration ou cinématique).
    /// </summary>
    public class RootMotionDriver : MonoBehaviour
    {
        [SerializeField] private MovementAuthorityController mode;
        [SerializeField] private float gravity = -30f;
        [SerializeField] private bool lockYToGround = true; // option pour ignorer le Y du Root Motion

        private Vector3 verticalVelocity; // vitesse verticale cumulée pour la gravité

        private void OnAnimatorMove()
        {
            // On ne lit le Root Motion que si l'autorité est Animation ou NavMesh
            if (mode.Authority != MovementAuthority.Animation && mode.Authority != MovementAuthority.NavMesh)
                return;

            var animator = mode.Animator;
            var controller = mode.CC;

            // delta du Root Motion calculé par l'Animator cette frame
            Vector3 delta = animator.deltaPosition;
            Quaternion drot = animator.deltaRotation;

            // Applique toujours la rotation du Root Motion pour conserver la fluidité
            transform.rotation *= drot;

            if (controller)
            {
                // Gestion de la gravité : on maintient un léger contact avec le sol
                if (controller.isGrounded) verticalVelocity.y = -0.1f;
                else verticalVelocity.y += gravity * Time.deltaTime;

                // Si demandé, on ignore la composante verticale du Root Motion
                if (lockYToGround) delta.y = 0f;

                Vector3 total = delta + verticalVelocity * Time.deltaTime;
                controller.Move(total);
            }
            else
            {
                // Sans CharacterController (ex: cinématique), on applique directement le delta
                transform.position += delta;
            }
        }
    }
}
