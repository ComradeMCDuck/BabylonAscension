using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Cinemachine extension that shifts the camera from the player toward the mouse cursor, so the player
/// can peek in the direction they aim. The shift is a fraction of the player-to-cursor distance and is
/// capped by <see cref="maxDistance"/>, so the player always stays in frame. Add it to the
/// CinemachineCamera next to the Position Composer; the composer keeps following CameraFollow and this
/// adds the cursor offset on top, with its own damping.
/// </summary>
[AddComponentMenu("Cinemachine/Procedural/Extensions/Cinemachine Cursor Offset")]
[SaveDuringPlay]
public class CinemachineCursorOffset : CinemachineExtension
{
    [Tooltip("Camera used to project the mouse onto the player's XY plane. Leave empty to use the camera that this CinemachineCamera outputs to.")]
    [SerializeField] private Camera aimCamera;

    [Tooltip("How much of the player-to-cursor distance the camera moves, per axis. 1 centres the camera on the cursor (until Max Distance stops it), 0 disables that axis.")]
    [SerializeField] private Vector2 influence = new Vector2(0.5f, 0.35f);

    [Tooltip("The camera stays put while the cursor is within this many world units of the player. Past it the shift grows from zero, so there is no jump at the edge.")]
    [SerializeField, Min(0f)] private float deadZoneRadius = 3f;

    [Tooltip("Furthest the camera may move away from the player, in world units. Keeps the player on screen.")]
    [SerializeField, Min(0f)] private float maxDistance = 4f;

    [Tooltip("Seconds the offset takes to settle on the cursor. 0 reacts instantly.")]
    [SerializeField, Min(0f)] private float damping = 0.4f;

    // Offset applied last frame. Also used to undo the camera shift when reading the cursor,
    // otherwise moving the camera would move the cursor's world position and chase itself.
    private Vector3 currentOffset;

    /// <summary>World-space offset currently applied to the camera.</summary>
    public Vector3 CurrentOffset => currentOffset;

    private void OnValidate()
    {
        influence.x = Mathf.Clamp01(influence.x);
        influence.y = Mathf.Clamp01(influence.y);
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Body) return;

        // Keep the offset frozen while paused, so the pause menu can't pan the camera.
        if (Application.isPlaying && Time.timeScale != 0f)
        {
            Vector3 desired = ComputeDesiredOffset(vcam);

            // deltaTime < 0 means the camera was just activated: snap instead of easing in.
            if (deltaTime < 0f || damping <= 0f) currentOffset = desired;
            else currentOffset += Damper.Damp(desired - currentOffset, damping, deltaTime);
        }
        else if (!Application.isPlaying)
        {
            currentOffset = Vector3.zero;
        }

        state.PositionCorrection += currentOffset;
    }

    private Vector3 ComputeDesiredOffset(CinemachineVirtualCameraBase vcam)
    {
        Transform follow = vcam.Follow;
        if (follow == null || Mouse.current == null) return Vector3.zero;

        Camera cam = aimCamera;
        if (cam == null)
        {
            CinemachineBrain brain = CinemachineCore.FindPotentialTargetBrain(vcam);
            cam = brain != null ? brain.OutputCamera : Camera.main;
            if (cam == null) return Vector3.zero;
        }

        // Intersect the camera ray with the player's XY plane, like BowCombat does for aiming.
        Vector3 followPosition = follow.position;
        Plane aimPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, followPosition.z));
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!aimPlane.Raycast(ray, out float distance)) return Vector3.zero;

        // The real camera already includes last frame's offset, so remove it to get the cursor
        // position as seen from the unshifted camera. This keeps the result stable.
        Vector3 cursor = ray.GetPoint(distance) - currentOffset;

        Vector3 offset = cursor - followPosition;
        offset.z = 0f;

        // Ignore the cursor near the player and measure the rest from the dead zone's edge,
        // so the offset starts at zero exactly where the effect kicks in.
        float cursorDistance = offset.magnitude;
        if (cursorDistance <= deadZoneRadius) return Vector3.zero;
        offset *= (cursorDistance - deadZoneRadius) / cursorDistance;

        offset.x *= influence.x;
        offset.y *= influence.y;

        return Vector3.ClampMagnitude(offset, maxDistance);
    }
}
