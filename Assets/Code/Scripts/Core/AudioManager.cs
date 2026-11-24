using UnityEngine;
using FMODUnity;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance { get; private set; }
    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("Multiple instances of AudioManager detected.");
        }
        instance = this;
    }
    public void PlayOneShot(EventReference sound, Vector3 position)
    {
        RuntimeManager.PlayOneShot(sound, position);
    }
}
