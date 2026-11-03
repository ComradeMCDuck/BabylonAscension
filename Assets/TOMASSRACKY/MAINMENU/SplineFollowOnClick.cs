using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
using System.Collections;

/// <summary>
/// Put this script on any GameObject (often the same as the UI Button).
/// It does the following on button click:
///   1. Enables camera tracking (Follow & LookAt).
///   2. Starts the Dolly‑Track Position animation.
/// </summary>
public class SplineFollowOnClick : MonoBehaviour
{
    [Header("UI")]
    public Button triggerButton;          // The “Play” button

    [Header("Cinemachine")]
    public CinemachineCamera virtCam;   // virtual camera that has a TrackedDolly
    public Transform trackingTarget; // The Transform to follow and look at. If left empty we will try to locate it by name/tag on button press.
    public string targetName; // Optional: name of the GameObject to lock onto at runtime.
    public string targetTag;   // Optional: tag of the GameObject to lock onto at runtime.
    public CinemachineSplineDolly dolly;   // the TrackedDolly component

    [Header("Rotation smoothing")]
    public bool smoothStartRotation = true;
    public float rotationSmoothTime = 0.5f;

    [Header("Animation")]
    public Animator anim;                    // Animator that plays the DollyMove clip
    public string dollyClipState = "DollyMove"; // name of the state that contains the clip

    private void Awake()
    {
        // Validate references – if anything missing we don’t want to run.
        if (triggerButton == null || virtCam == null || dolly == null || anim == null || (trackingTarget == null && string.IsNullOrEmpty(targetName) && string.IsNullOrEmpty(targetTag)))
        {
            Debug.LogError("[SplineFollowOnClick] Some required components are missing. If you intend to let the script find the target at runtime, set either Target Name or Tag.");
            enabled = false;
            return;
        }

        // Wire up the button
        triggerButton.onClick.AddListener(OnButtonClicked);
    }

    public void CallOnButtonClicked()
    {
        OnButtonClicked();   // calls the private logic
    }
    private void OnButtonClicked()
    {
        // Resolve a target at runtime if it hasn’t been assigned yet
        if (trackingTarget == null)
        {
            if (!string.IsNullOrEmpty(targetName))
                trackingTarget = GameObject.Find(targetName)?.transform;
            else if (!string.IsNullOrEmpty(targetTag))
                trackingTarget = GameObject.FindWithTag(targetTag)?.transform;
        }

        if (trackingTarget == null)
        {
            Debug.LogError("[SplineFollowOnClick] No tracking target could be found. Provide a target via Inspector or set Target Name/Tag.");
            return;
        }

        // Activate tracking and play animation, optionally smoothing rotation
        StartCoroutine(StartTrackingWithRotation());
    }
    private IEnumerator StartTrackingWithRotation()
    {
        if (trackingTarget == null)
            yield break;

        if (smoothStartRotation)
        {
            Quaternion startRot = virtCam.transform.rotation;
            Vector3 dir = trackingTarget.position - virtCam.transform.position;
            if (dir.sqrMagnitude < Mathf.Epsilon)
                dir = Vector3.forward;
            Quaternion targetRot = Quaternion.LookRotation(dir);

            float elapsed = 0f;
            while (elapsed < rotationSmoothTime)
            {
                virtCam.transform.rotation = Quaternion.Slerp(startRot, targetRot, elapsed / rotationSmoothTime);
                elapsed += Time.deltaTime;
                yield return null;
            }
            virtCam.transform.rotation = targetRot;
        }

        virtCam.Follow = trackingTarget;
        virtCam.LookAt = trackingTarget;

        if (!string.IsNullOrEmpty(dollyClipState))
            anim.Play(dollyClipState);

        gameObject.SetActive(false);
    }

}