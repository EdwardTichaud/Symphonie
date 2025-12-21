// HybridMotionPack - TrajectoryWarper.cs
// Optional helper to subtly warp the last portion of a root-motion state toward a goal position (XZ only).

using UnityEngine;

namespace HybridMotionPack
{
    [DisallowMultipleComponent]
    public class TrajectoryWarper : MonoBehaviour
    {
        public Transform target;                // Optional: dynamic goal (e.g., interaction point)
        public Vector3 staticGoal;              // Used if target is null
        [Range(0f, 1f)] public float warpStartNormalizedTime = 0.7f;
        [Range(0f, 1f)] public float warpStrength = 1f; // 0..1 (1 = full alignment at the end)
        public bool debugDraw;

        Transform _transform;
        Animator _anim;

        void Awake()
        {
            _transform = transform;
            _anim = GetComponentInChildren<Animator>();
        }

        /// <summary>
        /// Call from an animation event near the end of the state or poll in Update.
        /// </summary>
        public void ApplyWarpIfNeeded()
        {
            if (_anim == null) return;

            AnimatorStateInfo st = _anim.GetCurrentAnimatorStateInfo(0);
            float t = st.normalizedTime % 1f;
            if (t < warpStartNormalizedTime) return;

            Vector3 goal = target ? target.position : staticGoal;
            goal.y = _transform.position.y;

            Vector3 toGoal = goal - _transform.position;
            float phase = Mathf.InverseLerp(warpStartNormalizedTime, 1f, t);
            float strength = warpStrength * phase;

            Vector3 adjust = toGoal * strength * Time.deltaTime * 5f; // small, frame-rate independent bias
            _transform.position += new Vector3(adjust.x, 0f, adjust.z);

            if (debugDraw)
            {
                Debug.DrawLine(_transform.position, goal, Color.magenta, 0.1f);
            }
        }
    }
}