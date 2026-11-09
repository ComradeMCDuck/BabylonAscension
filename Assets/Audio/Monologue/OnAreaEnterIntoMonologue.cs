using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OnAreaEnterIntoMonologue : MonoBehaviour
{
    public MonologueLines monologueLines;
    public bool entered = false;
    public bool startOnStart = false;

    void Start()
    {
        if (startOnStart == true)
        {
            MonologueManager.inst.StartMonologue(monologueLines);
        }
    }

    public void StartMonologue()
    {
        if (entered == true)
        {
            return;
        }

        entered = true;
        MonologueManager.inst.StartMonologue(monologueLines);
    }


}
