using UnityEngine;

public class CameraTargetProxy : MonoBehaviour
{
    public Transform player;
    public float fixedZ;

    void Start()
    {
        fixedZ = player.position.z; // or set a value manually
    }

    void LateUpdate()
    {
        Vector3 p = player.position;
        transform.position = new Vector3(p.x, p.y, fixedZ);
    }
}
