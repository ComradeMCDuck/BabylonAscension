using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Attach to any GameObject (e.g. a UI button) and wire the button’s
/// OnClick event to the public Toggle() method.  When called it
/// enables or disables the Cinemachine camera’s Follow / LookAt targets.
/// </summary>
public class ToggleCinemachineTrack : MonoBehaviour
{
    [Header("Cinemachine")]
    public CinemachineCamera virtualCamera;   // The Virtual Camera you want to toggle
    public Transform target;                         // The GameObject to follow/ lookAt

    [Header("Debug / state")]
    public bool isTracking = false;                  // Current state – shown in the Inspector

    /// <summary>
    /// Toggle the tracking on or off.
    /// </summary>
    public void Toggle()
    {
        isTracking = !isTracking;

        if (isTracking)
        {
            // Enable tracking
            virtualCamera.Follow = target;
            virtualCamera.LookAt = target;
        }
        else
        {
            // Disable tracking
            virtualCamera.Follow = null;
            virtualCamera.LookAt = null;
        }
    }
}