using UnityEngine;
using UnityEngine.InputSystem;

namespace DYP
{
    /// <summary>
    /// The player input driver for controller BasicMovementController2D
    /// </summary>
    public class PlayerBasicMovementInput : MonoBehaviour
    {
        [SerializeField]
        private BasicMovementController2D m_Controller;

        [SerializeField]
        private InputActionReference moveActionReference;

        [SerializeField]
        private InputActionReference jumpActionReference;

        [SerializeField]
        private InputActionReference dashActionReference;

        private InputAction m_MoveAction;
        private InputAction m_JumpAction;
        private InputAction m_DashAction;

        private void OnEnable()
        {
            ResolveActions();
            EnableActions();
        }

        private void OnDisable()
        {
            DisableActions();
        }

        private void ResolveActions()
        {
            m_MoveAction = moveActionReference != null ? moveActionReference.action : GetAction("Move");
            m_JumpAction = jumpActionReference != null ? jumpActionReference.action : GetAction("Jump");
            m_DashAction = dashActionReference != null ? dashActionReference.action : GetAction("Sprint");
        }

        private InputAction GetAction(string actionName)
        {
            if (InputSystem.actions == null)
            {
                return null;
            }

            var playerMap = InputSystem.actions.FindActionMap("Player", false);
            return playerMap != null ? playerMap.FindAction(actionName, false) : null;
        }

        private void EnableActions()
        {
            if (m_MoveAction != null) m_MoveAction.Enable();
            if (m_JumpAction != null) m_JumpAction.Enable();
            if (m_DashAction != null) m_DashAction.Enable();
        }

        private void DisableActions()
        {
            if (m_MoveAction != null) m_MoveAction.Disable();
            if (m_JumpAction != null) m_JumpAction.Disable();
            if (m_DashAction != null) m_DashAction.Disable();
        }

        // Update is called once per frame
        void Update()
        {
            if (m_Controller == null)
            {
                return;
            }

            var move = m_MoveAction != null ? m_MoveAction.ReadValue<Vector2>() : Vector2.zero;
            var horizontal = move.x;
            var vertical = move.y;

            var jump = m_JumpAction != null && m_JumpAction.triggered;
            var dash = m_DashAction != null && m_DashAction.triggered;

            var holdingJump = m_JumpAction != null && m_JumpAction.IsPressed();
            var holdingDash = m_DashAction != null && m_DashAction.IsPressed();

            var releaseJump = m_JumpAction != null && m_JumpAction.WasReleasedThisFrame();

            m_Controller.InputMovement(new Vector2(horizontal, vertical));

            m_Controller.PressJump(jump);

            m_Controller.PressDash(dash);

            m_Controller.HoldJump(holdingJump);

            m_Controller.HoldDash(holdingDash);

            m_Controller.ReleaseJump(releaseJump);
        }
    }
}