using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class LucianHybridMotion : MonoBehaviour
{
    [Header("Déplacement In-Place")]
    public float speed = 4f;
    public float rotationSharpness = 15f;
    public float gravity = -9.81f;

    [Header("Animator")]
    public string rootMotionTag = "RootMotion"; // Tag des états qui utilisent la RM

    CharacterController controller;
    Animator animator;
    Transform model; // l'objet qui porte l'Animator (enfant)
    float verticalVel;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    int tagRootMotionHash;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        model = animator ? animator.transform : null;
        if (animator) animator.applyRootMotion = false;
        tagRootMotionHash = Animator.StringToHash(rootMotionTag);
    }

    void Update()
    {
        Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
        bool hasInput = input.sqrMagnitude > 0.0001f;

        // Détecte si l'état courant doit utiliser la root motion (via tag)
        bool wantsRootMotion = false;
        if (animator)
        {
            var st = animator.GetCurrentAnimatorStateInfo(0);
            wantsRootMotion = st.tagHash == tagRootMotionHash;
            animator.applyRootMotion = wantsRootMotion;
        }

        if (!wantsRootMotion)
        {
            // --- Mode In-Place (comme ton script, mais avec rotation & gravité) ---
            Vector3 worldDir = transform.TransformDirection(input).normalized; // (ou basé caméra si tu préfères)
            if (model && hasInput)
            {
                var targetRot = Quaternion.LookRotation(worldDir, Vector3.up);
                model.rotation = Quaternion.Slerp(model.rotation, targetRot, 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
            }

            if (controller.isGrounded && verticalVel < 0f) verticalVel = -2f; // stick to ground
            verticalVel += gravity * Time.deltaTime;

            Vector3 move = new Vector3(worldDir.x * speed, verticalVel, worldDir.z * speed) * Time.deltaTime;
            controller.Move(move);

            if (animator) animator.SetFloat(SpeedHash, input.magnitude);
        }
        else
        {
            // En root motion, le déplacement sera appliqué dans OnAnimatorMove
            if (animator) animator.SetFloat(SpeedHash, 0f); // évite les blends de locomotion
        }
    }

    void OnAnimatorMove()
    {
        if (!animator || !animator.applyRootMotion) return;

        // Récupère le delta de la clip (fourni par l'Animator sur l'enfant)
        Vector3 delta = animator.deltaPosition;

        // Gère la gravité via le CharacterController
        if (controller.isGrounded && verticalVel < 0f) verticalVel = -2f;
        verticalVel += gravity * Time.deltaTime;
        delta.y = verticalVel * Time.deltaTime;

        controller.Move(delta);

        // Applique la rotation issue de la root motion au parent (objet gameplay)
        var dRot = animator.deltaRotation;
        if (dRot != Quaternion.identity)
            transform.rotation *= dRot;
    }
}
