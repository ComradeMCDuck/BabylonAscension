using UnityEngine;

public class LedgeDetected : MonoBehaviour
{
    private PlayerVariables playerVariables;
    private void Start()
    {
        playerVariables = FindAnyObjectByType<PlayerVariables>();
        if (playerVariables == null)
        {
            Debug.LogWarning("Can't find PlayerVariables in parent of " + gameObject);
        }
    }
    private void Update()
    {
        if (playerVariables.FlipX)
        {
            transform.localPosition = new Vector3(-Mathf.Abs(transform.localPosition.x), transform.localPosition.y, transform.localPosition.z);
        }
        else
        {
            transform.localPosition = new Vector3(Mathf.Abs(transform.localPosition.x), transform.localPosition.y, transform.localPosition.z);
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Environment"))
        {
            if (CompareTag("UpperLedgeCollider"))
            {
                playerVariables.FoundSpaceAboveWall = false;
            }
            else if (CompareTag("WallCollider"))
            {
                playerVariables.FacingWall = true;
            }
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (CompareTag("UpperLedgeCollider"))
        {
            playerVariables.FoundSpaceAboveWall = true;
        }
        else if (CompareTag("WallCollider"))
        {
            playerVariables.FacingWall = false;
        }
    }
}
