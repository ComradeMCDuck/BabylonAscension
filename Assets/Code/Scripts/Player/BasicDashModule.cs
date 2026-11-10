using UnityEngine;

namespace DYP
{
    [CreateAssetMenu(fileName = "BasicDashModule", menuName = "DYP/Dash Module/Basic")]
    public class BasicDashModule : BaseDashModule
    {
        [SerializeField] private float m_DashSpeed = 20f;
        [SerializeField] private AnimationCurve m_SpeedCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f);

        [SerializeField] private bool m_UseCollision = true;
        [SerializeField] private bool m_UseGravity = false;
        [SerializeField] private bool m_ChangeFacing = true;
        [SerializeField] private bool m_CanDashToSlidingWall = false;
        [SerializeField] private bool m_CanOnlyBeUsedOnGround = false;

        public override bool UseCollision => m_UseCollision;
        public override bool UseGravity => m_UseGravity;
        public override bool ChangeFacing => m_ChangeFacing;
        public override bool CanDashToSlidingWall => m_CanDashToSlidingWall;
        public override bool CanOnlyBeUsedOnGround => m_CanOnlyBeUsedOnGround;

        public override float GetDashSpeed(float prevTimer, float currTimer)
        {
            // Speed multiplier follows the curve over the dash's progress (0 to 1)
            return m_DashSpeed * m_SpeedCurve.Evaluate(GetDashProgress(currTimer));
        }
    }
}