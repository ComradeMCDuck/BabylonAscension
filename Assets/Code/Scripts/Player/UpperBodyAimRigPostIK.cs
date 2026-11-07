using UnityEngine;

/// <summary>
/// Second pass of <see cref="UpperBodyAimRig"/>. It needs its own LateUpdate slot: after
/// IKManager2D has solved the arms (order -10) and before SpriteSkin deforms the meshes
/// (order 10). The rig adds this component itself at runtime, so don't add it by hand.
/// </summary>
[DefaultExecutionOrder(ExecutionOrder)]
public class UpperBodyAimRigPostIK : MonoBehaviour
{
    // Between IKManager2D (-10) and SpriteSkin (10).
    private const int ExecutionOrder = 0;

    private UpperBodyAimRig rig;

    internal void Initialize(UpperBodyAimRig owner)
    {
        rig = owner;
    }

    private void LateUpdate()
    {
        if (rig != null) rig.ApplyPostIK();
    }
}
