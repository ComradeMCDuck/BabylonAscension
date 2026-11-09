using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class NoteOpener : MonoBehaviour
{
    
    public string textToDisplay;

    private bool active = false;

    public void NoteEntered()
    {
        active = true;
        StartCoroutine(NoteActive());
    }

    IEnumerator NoteActive()
    {
        while (active == true)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                NoteManager.inst.OpenNote(textToDisplay);
            }
            yield return new WaitForEndOfFrame();
        }
    }

    public void NoteExited()
    {
        active = false;
    }
}
