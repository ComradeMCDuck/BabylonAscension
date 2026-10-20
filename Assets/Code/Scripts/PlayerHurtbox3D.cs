using UnityEngine;

/// <summary>
/// The player moves with 2D physics (BoxCollider2D), but enemy projectiles use
/// 3D physics, and the two never interact. This creates a 3D trigger collider
/// that mirrors the player's BoxCollider2D so 3D projectiles can hit the player.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerHurtbox3D : MonoBehaviour
{
    [Tooltip("Layer of the generated 3D hurtbox. Must be in the projectiles' Hit Layers.")]
    [SerializeField] private string hurtboxLayer = "Player";

    [Tooltip("Thickness of the hurtbox along Z.")]
    [SerializeField] private float depth = 1f;

    private BoxCollider2D source;
    private BoxCollider hurtbox;

    public BoxCollider Hurtbox => hurtbox;

    private void Awake()
    {
        source = GetComponent<BoxCollider2D>();

        var hurtboxObject = new GameObject("Hurtbox3D");
        hurtboxObject.transform.SetParent(transform, false);
        hurtboxObject.tag = gameObject.tag;

        int layer = LayerMask.NameToLayer(hurtboxLayer);
        if (layer >= 0)
            hurtboxObject.layer = layer;
        else
            Debug.LogWarning($"Layer \"{hurtboxLayer}\" does not exist.", this);

        hurtbox = hurtboxObject.AddComponent<BoxCollider>();
        hurtbox.isTrigger = true;

        SyncWithSource();
    }

    // The 2D collider can be resized at runtime (e.g. crouching), so keep it in sync.
    private void LateUpdate()
    {
        SyncWithSource();
    }

    private void SyncWithSource()
    {
        if (source == null || hurtbox == null)
            return;

        hurtbox.enabled = source.enabled;
        hurtbox.center = new Vector3(source.offset.x, source.offset.y, 0f);
        hurtbox.size = new Vector3(source.size.x, source.size.y, depth);
    }
}
