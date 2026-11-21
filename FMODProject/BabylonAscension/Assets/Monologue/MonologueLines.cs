using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "NewMonologueLines", menuName = "Scriptable Objects/Monologue Lines")]
public class MonologueLines : ScriptableObject
{
    public string[] textLines;
    public AudioClip[] audioLines;
}
