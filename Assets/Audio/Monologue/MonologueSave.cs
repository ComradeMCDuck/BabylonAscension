using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class MonologueSave
{
    static public bool playing = false;
    static public MonologueLines currentLines = null;
    static public int currentIndex = 0;
    static public float playAtPosition = 0.0f;

    static public List<int> playedVoiceLines = new List<int>();
}
