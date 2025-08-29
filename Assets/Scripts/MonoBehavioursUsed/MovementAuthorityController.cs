using UnityEngine;
using UnityEngine.AI;

namespace Symphonie.Movement
{
    /// <summary>
    /// Enum référençant les différents systèmes pouvant piloter le déplacement d'un personnage.
    /// Animation : le Root Motion de l'Animator est maître.
    /// Code : le gameplay contrôle entièrement la position.
    /// NavMesh : l'Agent fournit la direction, mais le Root Motion est utilisé et mis à l'échelle.
    /// None : aucun système ne modifie la position.
    /// </summary>
    public enum MovementAuthority { Animation, Code, NavMesh, None }

    /// <summary>
    /// Orchestrateur d'autorité de mouvement. Il active ou désactive le Root Motion et le NavMeshAgent
    /// en fonction du contexte (exploration, cinématique, combat...).
    /// 
    /// Attachez ce composant au personnage principal et appelez <see cref="SetAuthority"/> pour changer
    /// de mode selon les besoins du gameplay ou des cinématiques.
    /// </summary>
    public class MovementAuthorityController : MonoBehaviour
    {
        /// <summary>Autorité de mouvement actuellement active.</summary>
        public MovementAuthority Authority { get; private set; } = MovementAuthority.Code;

        [Header("Références de composants")]
        [Tooltip("Animator utilisé pour lire le Root Motion.")]
        public Animator Animator;

        [Tooltip("CharacterController gérant les collisions au sol.")]
        public CharacterController CC; // ou Rigidbody si besoin

        [Tooltip("NavMeshAgent optionnel pour l'exploration.")]
        public NavMeshAgent Agent;

        /// <summary>
        /// Change l'autorité de mouvement et ajuste les composants Unity correspondants.
        /// </summary>
        /// <param name="a">Nouvelle autorité à appliquer.</param>
        public void SetAuthority(MovementAuthority a)
        {
            Authority = a;
            switch (a)
            {
                case MovementAuthority.Animation:
                    if (Agent) Agent.updatePosition = false; // on laisse l'Animator déplacer l'objet
                    Animator.applyRootMotion = true;
                    break;
                case MovementAuthority.Code:
                    Animator.applyRootMotion = false;
                    if (Agent) Agent.updatePosition = false;
                    break;
                case MovementAuthority.NavMesh:
                    Animator.applyRootMotion = true; // le Root Motion sera mis à l'échelle dans NavMeshRootSync
                    if (Agent) Agent.updatePosition = false;
                    break;
                case MovementAuthority.None:
                    Animator.applyRootMotion = false;
                    if (Agent) Agent.updatePosition = false;
                    break;
            }
        }
    }
}
