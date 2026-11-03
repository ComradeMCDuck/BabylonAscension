using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneSwitcher : MonoBehaviour
{
    [Tooltip("The scene to load after delay")]
    public string nextSceneName = "GameScene";

    [Tooltip("Delay in seconds before switching")]
    public float delay = 4f;

    // Call this from the button's OnClick event
    public void OnButtonClick()
    {
        StartCoroutine(SwitchSceneAfterDelay());
    }

    private IEnumerator SwitchSceneAfterDelay()
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(nextSceneName);
    }
}
