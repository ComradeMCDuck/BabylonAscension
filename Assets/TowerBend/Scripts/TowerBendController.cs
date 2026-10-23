using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Drives the global tower-bend and tessellation parameters for TowerBend shaders.
/// Put one in the scene. The level is built flat on the XY plane, camera looking +Z.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class TowerBendController : MonoBehaviour
{
    public static TowerBendController Active { get; private set; }

    [Header("Tower")]
    [Tooltip("The bend origin follows this camera's X, so the player always stands at the front of the tower.")]
    public Camera gameCamera;
    [Min(0.01f)] public float radius = 20f;
    [Tooltip("Flat-world Z of the gameplay plane. Lengths are preserved exactly on this plane.")]
    public float surfaceZ = 0f;
    public bool bendInSceneView = true;

    [Header("Tessellation")]
    [Tooltip("Target edge length in world units, used up to the reference distance.")]
    [Min(0.01f)] public float targetEdgeLength = 0.5f;
    [Tooltip("Beyond this distance the target edge length grows linearly (constant density on screen).")]
    [Min(0.01f)] public float referenceDistance = 15f;
    [Range(1f, 64f)] public float maxTessFactor = 32f;
    [Tooltip("Tessellation fades out to none at this distance. 0 = unlimited.")]
    [Min(0f)] public float maxTessDistance = 200f;
    [Tooltip("0 = only the horizontal (flat X) extent of an edge is subdivided, which is the only direction that bends. 1 = full edge length.")]
    [Range(0f, 1f)] public float isotropy = 0f;

    [Header("Culling")]
    [Tooltip("Patches further around the tower than this angle are discarded on the GPU. 0 = off.")]
    [Range(0f, 180f)] public float cullAngle = 110f;
    [Tooltip("Widens the CPU frustum horizontally. Bending pulls geometry towards the screen centre, so objects whose FLAT bounds are off-screen can be visible.")]
    [Min(1f)] public float cullingWidthScale = 1.6f;

    static readonly int BendId = Shader.PropertyToID("_TowerBend");
    static readonly int BendPrevId = Shader.PropertyToID("_TowerBendPrev");
    static readonly int TessId = Shader.PropertyToID("_TowerTess");
    static readonly int TessCameraId = Shader.PropertyToID("_TowerTessCamera");
    static readonly int CullId = Shader.PropertyToID("_TowerCull");

    Vector4 m_PrevGameBend;
    bool m_HasPrevGameBend;
    readonly List<Camera> m_CamerasWithCullingOverride = new List<Camera>();

    /// <summary>Bend parameters as seen by the game camera this frame.</summary>
    public TowerBendParams Params => new TowerBendParams
    {
        originX = gameCamera ? gameCamera.transform.position.x : 0f,
        radius = radius,
        surfaceZ = surfaceZ,
        enabled = isActiveAndEnabled,
    };

    void OnEnable()
    {
        Active = this;
        m_HasPrevGameBend = false;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        if (Active == this) Active = null;

        Shader.SetGlobalVector(BendId, Vector4.zero);
        Shader.SetGlobalVector(BendPrevId, Vector4.zero);
        foreach (var cam in m_CamerasWithCullingOverride)
            if (cam) cam.ResetCullingMatrix();
        m_CamerasWithCullingOverride.Clear();
    }

    bool ShouldBend(Camera cam)
    {
        switch (cam.cameraType)
        {
            case CameraType.Game: return true;
            case CameraType.SceneView: return bendInSceneView;
            default: return false; // material previews, reflection probes, ...
        }
    }

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        bool bend = ShouldBend(cam);
        var p = Params;
        p.enabled = bend;
        Vector4 bendVec = p.ToShaderVector();

        Vector4 prev = bendVec;
        if (cam == gameCamera && m_HasPrevGameBend)
            prev = m_PrevGameBend;

        Shader.SetGlobalVector(BendId, bendVec);
        Shader.SetGlobalVector(BendPrevId, prev);
        Shader.SetGlobalVector(TessId, new Vector4(targetEdgeLength, referenceDistance, maxTessFactor, maxTessDistance));
        Shader.SetGlobalVector(TessCameraId, new Vector4(cam.transform.position.x, cam.transform.position.y, cam.transform.position.z, isotropy));
        Shader.SetGlobalVector(CullId, new Vector4(cullAngle * Mathf.Deg2Rad, 0f, 0f, 0f));

        if (bend)
        {
            Matrix4x4 proj = cam.projectionMatrix;
            proj.m00 /= cullingWidthScale;
            cam.cullingMatrix = proj * cam.worldToCameraMatrix;
            if (!m_CamerasWithCullingOverride.Contains(cam))
                m_CamerasWithCullingOverride.Add(cam);

            TowerBendTransform.BendAll(p);
        }
        else
        {
            cam.ResetCullingMatrix();
        }
    }

    void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        TowerBendTransform.RestoreAll();

        if (cam == gameCamera)
        {
            m_PrevGameBend = Params.ToShaderVector();
            m_HasPrevGameBend = true;
        }
    }
}
