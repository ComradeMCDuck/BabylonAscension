using System.Collections;
using UnityEngine;

/// <summary>
/// Blinks every sprite under this object white, e.g. while the player is invincible after Second Chance.
/// A SpriteRenderer's color can only darken a sprite, so this sets the TowerBend/Sprite-Lit shader's _FlashAmount
/// (0 = normal, 1 = solid white) through a MaterialPropertyBlock on each renderer; the shared material is untouched.
/// Sprites using another shader don't flash. Added at runtime with <see cref="For"/>.
/// </summary>
public class SpriteFlash : MonoBehaviour
{
    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

    private SpriteRenderer[] renderers;
    private MaterialPropertyBlock block;
    private Coroutine blinking;

    /// <summary>The SpriteFlash on this object, added if it has none yet.</summary>
    public static SpriteFlash For(GameObject target)
    {
        return target.TryGetComponent(out SpriteFlash flash) ? flash : target.AddComponent<SpriteFlash>();
    }

    /// <summary>Switches between white and normal every interval for duration seconds, then ends normal.</summary>
    public void Blink(float duration, float interval)
    {
        Stop();
        // Looked up each time, so sprites added since (e.g. the bow glow) blink too.
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        blinking = StartCoroutine(BlinkRoutine(duration, Mathf.Max(0.02f, interval)));
    }

    public void Stop()
    {
        if (blinking != null)
        {
            StopCoroutine(blinking);
            blinking = null;
        }
        SetFlash(0f);
    }

    private IEnumerator BlinkRoutine(float duration, float interval)
    {
        float endTime = Time.time + duration;
        bool white = true;
        while (Time.time < endTime)
        {
            SetFlash(white ? 1f : 0f);
            white = !white;
            yield return new WaitForSeconds(interval);
        }

        SetFlash(0f);
        blinking = null;
    }

    private void SetFlash(float amount)
    {
        if (renderers == null) return;
        if (block == null) block = new MaterialPropertyBlock();

        foreach (SpriteRenderer sprite in renderers)
        {
            if (sprite == null) continue;
            // Keep what the renderer already has in its block (the sprite texture), only change the flash.
            sprite.GetPropertyBlock(block);
            block.SetFloat(FlashAmountId, amount);
            sprite.SetPropertyBlock(block);
        }
    }

    private void OnDisable() => Stop();
}
