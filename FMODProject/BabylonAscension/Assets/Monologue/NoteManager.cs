using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;


public class NoteManager : MonoBehaviour
{
    public static NoteManager inst;

    public TextMeshProUGUI textDisplayed;
    public GameObject note;

    void Awake()
    {
        inst = this;
    }


    public void OpenNote(string noteText)
    {
        textDisplayed.text = noteText;
        note.SetActive(true);
    }

    public void CloseNote()
    {
        note.SetActive(false);
    }
}
