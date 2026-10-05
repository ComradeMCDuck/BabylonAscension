using System.Collections;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // Assuming the player has a PlayerHealth component to manage health
            var playerHealth = collision.gameObject.GetComponent<IDamageable>();
            if (playerHealth != null)
            {
                Debug.Log("Player hit! Dealing damage.");
                //playerHealth.TakeDamage(10); // Deal damage to the player
            }
        }
        Destroy(gameObject); // Destroy the object after hitting the player
    }

    public IEnumerator Shoot(Vector3 targetPosition, float attackSpeed)
    {
        // Move toward the saved position until we reach it
        Vector3 targetMiddlePosition = targetPosition + new Vector3(0f, 1f, 0f);
        while (this != null && Vector3.Distance(transform.position, targetMiddlePosition) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetMiddlePosition, attackSpeed * Time.deltaTime);
            yield return null; // wait one frame, then continue the loop
        }
        if (this != null) Destroy(gameObject); // Destroy the object after reaching the target position and if it still exists
    }
}
