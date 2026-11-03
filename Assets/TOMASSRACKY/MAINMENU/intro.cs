
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class EnableButtonsAfterDelay : MonoBehaviour
{
    [SerializeField] private Button button1;
    [SerializeField] private Button button2;

    private IEnumerator Start()
    {
        button1.gameObject.SetActive(false);
        button2.gameObject.SetActive(false);

        yield return new WaitForSeconds(2.5f);

        button1.gameObject.SetActive(true);
        button2.gameObject.SetActive(true);
    }
}
