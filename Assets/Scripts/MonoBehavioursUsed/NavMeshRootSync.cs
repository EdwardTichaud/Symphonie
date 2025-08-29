using UnityEngine;
using UnityEngine.AI;

namespace Symphonie.Movement
{
    /// <summary>
    /// Synchronise le Root Motion de l'Animator avec la trajectoire d'un NavMeshAgent.
    /// Permet de conserver des animations naturelles tout en suivant un chemin calculé par le NavMesh.
    /// </summary>
    public class NavMeshRootSync : MonoBehaviour
    {
        [SerializeField] private MovementAuthorityController mode;
        [SerializeField] private float maxSpeed = 4.5f; // vitesse max autorisée
        [SerializeField] private float cornerSnap = 0.4f; // seuil de correction aux changements de direction

        private void Update()
        {
            if (mode.Authority != MovementAuthority.NavMesh || mode.Agent == null) return;

            // Orientation progressive vers la vélocité désirée
            Vector3 vel = mode.Agent.desiredVelocity;
            vel.y = 0f;
            if (vel.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(vel.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 12f * Time.deltaTime);
            }

            // Envoi de la vitesse pour un éventuel Blend Tree
            mode.Animator.SetFloat("Speed", vel.magnitude);
        }

        private void OnAnimatorMove()
        {
            if (mode.Authority != MovementAuthority.NavMesh || mode.Agent == null) return;

            // Delta issu du Root Motion cette frame
            Vector3 rootDelta = mode.Animator.deltaPosition;
            float rootSpeed = rootDelta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            float desiredSpeed = Mathf.Clamp(mode.Agent.desiredVelocity.magnitude, 0f, maxSpeed);

            // Échelle du Root Motion pour coller à la vitesse désirée
            float scale = (rootSpeed > 0.01f) ? desiredSpeed / rootSpeed : 0f;
            Vector3 move = rootDelta * scale;
            move.y = 0f; // la gestion verticale se fait ailleurs si nécessaire

            transform.position += move;
            transform.rotation *= mode.Animator.deltaRotation;

            // Synchronise la position interne de l'Agent sans qu'il déplace l'objet
            mode.Agent.nextPosition = transform.position;

            // Petites corrections aux coins pour éviter une dérive progressive
            if (!mode.Agent.pathPending && mode.Agent.remainingDistance < cornerSnap && !mode.Agent.hasPath)
            {
                mode.Agent.Warp(transform.position);
            }
        }
    }
}
