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

        [SerializeField]
        private InputActionReference firstArrowActionReference;

        [SerializeField]
        private InputActionReference secondArrowActionReference;

        private InputAction m_MoveAction;
        private InputAction m_JumpAction;
        private InputAction m_DashAction;
        private InputAction m_FirstArrowAction;
        private InputAction m_SecondArrowAction;

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
            // "Previous" is bound to key 1 and "Next" to key 2 in InputSystem_Actions.
            m_FirstArrowAction = firstArrowActionReference != null ? firstArrowActionReference.action : GetAction("Previous");
            m_SecondArrowAction = secondArrowActionReference != null ? secondArrowActionReference.action : GetAction("Next");
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
            if (m_FirstArrowAction != null) m_FirstArrowAction.Enable();
            if (m_SecondArrowAction != null) m_SecondArrowAction.Enable();
        }

        private void DisableActions()
        {
            if (m_MoveAction != null) m_MoveAction.Disable();
            if (m_JumpAction != null) m_JumpAction.Disable();
            if (m_DashAction != null) m_DashAction.Disable();
            if (m_FirstArrowAction != null) m_FirstArrowAction.Disable();
            if (m_SecondArrowAction != null) m_SecondArrowAction.Disable();
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

            if (m_FirstArrowAction != null && m_FirstArrowAction.triggered)
            {
                m_Controller.SelectArrow(0);
            }
            else if (m_SecondArrowAction != null && m_SecondArrowAction.triggered)
            {
                m_Controller.SelectArrow(1);
            }
        }
    }
}