using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.U2D.IK;

/// <summary>
/// Procedural upper-body aiming for the skeletal player: the spine leans toward the cursor,
/// the bow arm reaches along the shoulder-to-cursor line, and the draw hand sits on the
/// string, pulling back as <see cref="BowCombat"/> charges.
///
/// Frame order matters, so the work is split in two passes:
/// <list type="number">
/// <item>LateUpdate here (order -50): after the Animator has written this frame's pose and
/// before IKManager2D (order -10). Moves the hips, leans the spine on top of the animated
/// pose and places both LimbSolver2D targets.</item>
/// <item><see cref="UpperBodyAimRigPostIK"/> (order 0): after IK, before SpriteSkin deforms
/// (order 10). Turns the bow and draw-hand bones to the aim and snaps BowPivot onto the bow hand.</item>
/// </list>
/// Works with a mirrored parent (PlayerFacing flips Appearance with scale.x = -1); the art
/// is assumed to face right when unflipped.
/// </summary>
[DefaultExecutionOrder(ExecutionOrder)]
public class UpperBodyAimRig : MonoBehaviour
{
    // Must run after the Animator and before IKManager2D, which solves at order -10.
    private const int ExecutionOrder = -50;
    // Cursor closer than this to the shoulder gives no usable direction; keep the last one.
    private const float MinAimSqrDistance = 0.0001f;

    [Serializable]
    private struct SpineBone
    {
        [Tooltip("Bone to rotate, e.g. Torso, Neck or Head.")]
        public Transform bone;
        [Tooltip("Share of the aim pitch this bone adds. Children inherit their parents' lean, so the shares add up toward the head. Negative leans the other way.")]
        public float weight;
        [Tooltip("This bone's lean never goes past this many degrees either way.")]
        [Min(0f)] public float maxAngle;
    }

    [Header("References")]
    [SerializeField] private Camera aimCamera;
    [Tooltip("Supplies the draw amount. Found in children when empty.")]
    [SerializeField] private BowCombat bowCombat;
    [Tooltip("Limb solver of the arm holding the bow (Shoulder_Back → Forearm_back). Its first bone is also where aiming starts.")]
    [SerializeField] private LimbSolver2D bowArmSolver;
    [Tooltip("Limb solver of the arm pulling the string (Shoulder_Front → Forearm_Front).")]
    [SerializeField] private LimbSolver2D drawArmSolver;

    [Header("Spine")]
    [Tooltip("Bones that lean with the aim, parent first. Each adds on top of whatever the Animator plays.")]
    [SerializeField] private SpineBone[] spineBones = Array.Empty<SpineBone>();
    [Tooltip("Aim pitch range in degrees used for leaning. 0 = straight ahead, 90 = straight up.")]
    [SerializeField] private Vector2 pitchLimits = new Vector2(-80f, 80f);
    [Tooltip("Seconds the lean takes to catch up with the cursor. The bow itself always points straight at it. 0 = instant.")]
    [SerializeField, Min(0f)] private float leanSmoothTime = 0.06f;

    [Header("Hips")]
    [Tooltip("Optional. A transform the legs animation moves per frame (e.g. an empty 'Hip' under CharacterBottomPart_0). The spine root follows how far it moves from its rest position.")]
    [SerializeField] private Transform hipAnchor;
    [Tooltip("The bone that follows the hip anchor, normally Core.")]
    [SerializeField] private Transform hipFollower;

    [Header("Bow Arm")]
    [Tooltip("How far the bow hand reaches toward the cursor, as a fraction of the full arm length. Below 1 keeps a slight bend in the elbow.")]
    [SerializeField, Range(0.3f, 1f)] private float gripReach = 0.95f;
    [Tooltip("Optional skeleton bone that carries the bow sprite (the 'Bow' bone). Turned to face the aim after IK.")]
    [SerializeField] private Transform bowBone;
    [Tooltip("Degrees added to the bow bone's aim, measured as if facing right.")]
    [SerializeField] private float bowBoneAngleOffset;
    [Tooltip("Optional. BowCombat's BowPivot. Placed on the bow hand every frame, so arrows leave from the hand.")]
    [SerializeField] private Transform bowPivot;
    [Tooltip("Flips BowPivot vertically while facing left, so the bow sprite isn't upside down.")]
    [SerializeField] private bool flipBowPivotWhenFacingLeft = true;

    [Header("Draw Arm")]
    [Tooltip("Distance from the bow hand back to the draw hand when the bow is resting (hand on the nock).")]
    [SerializeField, Min(0f)] private float idleNockDistance = 0.35f;
    [Tooltip("Distance from the bow hand back to the draw hand at full charge.")]
    [SerializeField, Min(0f)] private float fullDrawDistance = 1.1f;
    [Tooltip("Moves the draw hand sideways off the aim line. Positive is toward the character's up.")]
    [SerializeField] private float nockSideOffset;
    [Tooltip("Optional draw-hand bone (Arm_Front). Turned along the aim after IK.")]
    [SerializeField] private Transform drawHandBone;
    [Tooltip("Degrees added to the draw hand's aim, measured as if facing right.")]
    [SerializeField] private float drawHandAngleOffset;
    [Tooltip("How fast the draw hand springs back after a shot, in full draws per second.")]
    [SerializeField, Min(0f)] private float releaseSpeed = 12f;

    // Remembers the pose the Animator wrote, so the lean is added on top of it instead of
    // piling up every frame on bones the Animator doesn't touch.
    private struct AdditiveRotation
    {
        public Quaternion basePose;
        public Quaternion written;
        public bool hasWritten;
    }

    private AdditiveRotation[] spineStates;
    private Vector3 hipAnchorRest;
    private Vector3 hipFollowerBase;
    private Vector3 hipFollowerWritten;
    private bool hipHasWritten;

    private IKChain2D bowChain;
    private IKChain2D drawChain;
    private UpperBodyAimRigPostIK postIK;

    private Vector3 aimPoint;
    private Vector2 aimDirection = Vector2.right;
    private float facing = 1f;
    private float pitch;
    private float pitchVelocity;
    private float drawAmount;
    private bool hasAim;

    /// <summary>World-space direction from the bow shoulder to the cursor.</summary>
    public Vector2 AimDirection => aimDirection;
    /// <summary>Cursor position on the player's XY plane.</summary>
    public Vector3 AimPoint => aimPoint;
    /// <summary>Smoothed aim pitch in degrees, as if facing right (positive = up).</summary>
    public float Pitch => pitch;
    /// <summary>The bone aiming starts from (the bow arm's shoulder). Give it to BowCombat as Aim Origin.</summary>
    public Transform AimOrigin => bowChain != null ? bowChain.transforms[0] : null;

    private void Awake()
    {
        if (aimCamera == null)
            aimCamera = Camera.main;

        if (bowCombat == null)
            bowCombat = GetComponentInChildren<BowCombat>();

        bowChain = GetValidChain(bowArmSolver, "bow arm");
        drawChain = GetValidChain(drawArmSolver, "draw arm");

        if (aimCamera == null)
            Debug.LogError("UpperBodyAimRig needs an aim camera.", this);

        if (bowChain == null || drawChain == null || aimCamera == null)
        {
            enabled = false;
            return;
        }

        if (hipAnchor != null && hipFollower == null)
            Debug.LogError("UpperBodyAimRig has a Hip Anchor but no Hip Follower bone; hips won't follow.", this);

        if (hipAnchor != null)
            hipAnchorRest = hipAnchor.localPosition;

        spineStates = new AdditiveRotation[spineBones.Length];

        postIK = gameObject.AddComponent<UpperBodyAimRigPostIK>();
        postIK.hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;
        postIK.Initialize(this);
    }

    private void OnEnable()
    {
        if (postIK != null) postIK.enabled = true;
    }

    private void OnDisable()
    {
        if (postIK != null) postIK.enabled = false;
    }

    private void OnDestroy()
    {
        if (postIK != null) Destroy(postIK);
    }

    private IKChain2D GetValidChain(LimbSolver2D solver, string label)
    {
        if (solver == null)
        {
            Debug.LogError($"UpperBodyAimRig needs the {label} LimbSolver2D.", this);
            return null;
        }

        IKChain2D chain = solver.GetChain(0);
        if (chain == null || chain.target == null || chain.effector == null ||
            chain.transforms == null || chain.transforms.Length < 3)
        {
            Debug.LogError($"The {label} LimbSolver2D needs an effector, a target and a 2-bone chain.", solver);
            return null;
        }

        return chain;
    }

    // Runs after MotionInterpolator (-1000) has moved the visuals and before BowCombat (0)
    // fires, so a shot this frame leaves from where the bow hand is drawn.
    private void Update()
    {
        if (Time.timeScale == 0f) return;
        SnapBowPivotToHand(rotate: false);
    }

    private void LateUpdate()
    {
        // Paused or respawning: the Animator is frozen too, so leave the last pose alone.
        if (Time.timeScale == 0f) return;

        ApplyHipFollow();
        RestoreSpineBase();

        if (TryGetCursor(out Vector3 cursor))
        {
            aimPoint = cursor;
            hasAim = true;
        }

        if (!hasAim) return;

        Transform shoulder = bowChain.transforms[0];
        facing = MirrorSign(shoulder.parent);

        UpdatePitch(shoulder.position);
        ApplySpineLean();

        // The shoulder has moved with the lean, so aim from where it is now.
        Vector2 toCursor = aimPoint - shoulder.position;
        if (toCursor.sqrMagnitude > MinAimSqrDistance)
            aimDirection = toCursor.normalized;

        UpdateDrawAmount();
        PlaceTargets(shoulder.position);
    }

    /// <summary>Second pass, called by <see cref="UpperBodyAimRigPostIK"/> once IK has solved.</summary>
    internal void ApplyPostIK()
    {
        if (Time.timeScale == 0f || !hasAim) return;

        if (bowBone != null)
            PointBoneAlong(bowBone, Rotate(aimDirection, bowBoneAngleOffset * facing));

        if (drawHandBone != null)
            PointBoneAlong(drawHandBone, Rotate(aimDirection, drawHandAngleOffset * facing));

        SnapBowPivotToHand(rotate: true);
    }

    private bool TryGetCursor(out Vector3 cursor)
    {
        cursor = default;
        if (Mouse.current == null || aimCamera == null) return false;

        // Intersect the camera ray with the player's XY movement plane.
        Plane aimPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, transform.position.z));
        Ray ray = aimCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (!aimPlane.Raycast(ray, out float distance)) return false;

        cursor = ray.GetPoint(distance);
        return true;
    }

    private void ApplyHipFollow()
    {
        if (hipAnchor == null || hipFollower == null) return;

        // If the Animator wrote the follower this frame, that's the new base; otherwise reuse the old one.
        if (!hipHasWritten || hipFollower.localPosition != hipFollowerWritten)
            hipFollowerBase = hipFollower.localPosition;

        Vector3 worldDelta = hipAnchor.parent != null
            ? hipAnchor.parent.TransformVector(hipAnchor.localPosition - hipAnchorRest)
            : hipAnchor.localPosition - hipAnchorRest;
        Vector3 localDelta = hipFollower.parent != null
            ? hipFollower.parent.InverseTransformVector(worldDelta)
            : worldDelta;
        localDelta.z = 0f;

        hipFollowerWritten = hipFollowerBase + localDelta;
        hipFollower.localPosition = hipFollowerWritten;
        hipHasWritten = true;
    }

    private void RestoreSpineBase()
    {
        for (int i = 0; i < spineBones.Length; i++)
        {
            Transform bone = spineBones[i].bone;
            if (bone == null) continue;

            ref AdditiveRotation state = ref spineStates[i];
            if (!state.hasWritten || bone.localRotation != state.written)
                state.basePose = bone.localRotation;

            bone.localRotation = state.basePose;
        }
    }

    private void UpdatePitch(Vector3 pivot)
    {
        Vector2 toCursor = aimPoint - pivot;
        // Measure as if facing right, so the same lean works on both sides.
        float target = Mathf.Atan2(toCursor.y, toCursor.x * facing) * Mathf.Rad2Deg;
        target = Mathf.Clamp(target, pitchLimits.x, pitchLimits.y);

        pitch = leanSmoothTime > 0f
            ? Mathf.SmoothDampAngle(pitch, target, ref pitchVelocity, leanSmoothTime)
            : target;
    }

    private void ApplySpineLean()
    {
        for (int i = 0; i < spineBones.Length; i++)
        {
            SpineBone spine = spineBones[i];
            if (spine.bone == null) continue;

            // In facing space, so no mirror correction: a positive local angle is "up" on both sides.
            float angle = Mathf.Clamp(pitch * spine.weight, -spine.maxAngle, spine.maxAngle);

            ref AdditiveRotation state = ref spineStates[i];
            state.written = state.basePose * Quaternion.AngleAxis(angle, Vector3.forward);
            state.hasWritten = true;
            spine.bone.localRotation = state.written;
        }
    }

    private void UpdateDrawAmount()
    {
        float target = bowCombat != null && bowCombat.IsCharging ? bowCombat.Charge01 : 0f;

        // Follow the charge up exactly; spring back quickly on release or cancel.
        drawAmount = target >= drawAmount
            ? target
            : Mathf.MoveTowards(drawAmount, target, releaseSpeed * Time.deltaTime);
    }

    private void PlaceTargets(Vector3 shoulderPosition)
    {
        Transform[] bones = bowChain.transforms;
        float armLength = Vector2.Distance(bones[0].position, bones[1].position) +
                          Vector2.Distance(bones[1].position, bowChain.effector.position);

        Vector2 grip = (Vector2)shoulderPosition + aimDirection * (armLength * gripReach);
        Quaternion aimRotation = Quaternion.AngleAxis(AimAngle(), Vector3.forward);

        SetTarget(bowChain.target, grip, aimRotation);

        // Perpendicular to the aim, pointing toward the character's up on either side.
        Vector2 side = new Vector2(-aimDirection.y, aimDirection.x) * facing;
        float pull = Mathf.Lerp(idleNockDistance, fullDrawDistance, drawAmount);
        Vector2 nock = grip - aimDirection * pull + side * nockSideOffset;

        SetTarget(drawChain.target, nock, aimRotation);
    }

    private void SnapBowPivotToHand(bool rotate)
    {
        if (bowPivot == null || bowChain == null) return;

        Transform hand = bowBone != null ? bowBone : bowChain.effector;
        Vector3 position = hand.position;
        position.z = bowPivot.position.z;
        bowPivot.position = position;

        if (!rotate || !hasAim) return;

        bowPivot.rotation = Quaternion.AngleAxis(AimAngle(), Vector3.forward);

        if (flipBowPivotWhenFacingLeft)
        {
            Vector3 scale = bowPivot.localScale;
            scale.y = Mathf.Abs(scale.y) * facing;
            bowPivot.localScale = scale;
        }
    }

    private float AimAngle() => Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

    private static void SetTarget(Transform target, Vector2 position, Quaternion rotation)
    {
        target.position = new Vector3(position.x, position.y, target.position.z);
        target.rotation = rotation;
    }

    /// <summary>
    /// Turns a bone so its drawn +X axis points along a world direction. The visible direction
    /// comes from TransformVector, which includes mirroring, and the turn is flipped under a
    /// mirrored parent, because there a positive local rotation shows up clockwise.
    /// </summary>
    private static void PointBoneAlong(Transform bone, Vector2 worldDirection)
    {
        Vector2 current = bone.TransformVector(Vector3.right);
        if (current.sqrMagnitude < MinAimSqrDistance) return;

        float worldDelta = Vector2.SignedAngle(current, worldDirection);
        float localDelta = worldDelta * MirrorSign(bone.parent);
        bone.localRotation = Quaternion.AngleAxis(localDelta, Vector3.forward) * bone.localRotation;
    }

    /// <summary>-1 when the transform's XY axes are mirrored in world space, otherwise 1.</summary>
    private static float MirrorSign(Transform t)
    {
        if (t == null) return 1f;

        Vector3 x = t.TransformVector(Vector3.right);
        Vector3 y = t.TransformVector(Vector3.up);
        return x.x * y.y - x.y * y.x < 0f ? -1f : 1f;
    }

    private static Vector2 Rotate(Vector2 v, float degrees) =>
        Quaternion.AngleAxis(degrees, Vector3.forward) * v;

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !hasAim || bowChain == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(bowChain.transforms[0].position, aimPoint);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(bowChain.target.position, 0.08f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(drawChain.target.position, 0.08f);
    }
#endif
}
