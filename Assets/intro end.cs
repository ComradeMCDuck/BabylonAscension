
using UnityEngine;

public class DisableGameObjectOnClick : MonoBehaviour
{
    public GameObject targetObject1;
    public GameObject targetObject2;
    public GameObject targetObject3;
    public GameObject targetObject4;
    public GameObject targetObject5;

    public void DisableObjects()
    {
        targetObject1.SetActive(false);
        targetObject2.SetActive(false);
        targetObject3.SetActive(false);
        targetObject4.SetActive(false);
        targetObject5.SetActive(true);
    }
}
