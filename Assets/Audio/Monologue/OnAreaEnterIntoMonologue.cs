using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OnAreaEnterIntoMonologue : MonoBehaviour
{
    public MonologueLines monologueLines;
    public bool entered = false;
    public bool startOnStart = false;
    public int playedIndex = 0;

    void Start()
    {
        if (startOnStart == true)
        {
            MonologueManager.inst.StartMonologue(monologueLines);
        }

        if (MonologueSave.playedVoiceLines.Contains(playedIndex))
        {
            entered = true;
        }
    }

    public void StartMonologue()
    {
        if (entered == true)
        {
            return;
        }
        MonologueSave.playedVoiceLines.Add(playedIndex);

        entered = true;
        MonologueManager.inst.StartMonologue(monologueLines);
    }


}
