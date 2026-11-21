using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MonologueManager : MonoBehaviour
{
    public static MonologueManager inst;

    public MonologueLines currentLines;
    public AudioSource audioSource;
    public TextMeshProUGUI textDisplayed;

    private int index = 0;
    public bool monologuePlaying = false;

    void Awake()
	{
		inst = this;
	}

    void Start()
    {
        if (MonologueSave.currentIndex > 0)
        {
            currentLines = MonologueSave.currentLines;
            index = MonologueSave.currentIndex;
            StartMonologue(MonologueSave.currentLines);
        }
    }

    public void StartMonologue(MonologueLines newLines)
    {
        currentLines = newLines;
        MonologueSave.currentLines = currentLines;
        monologuePlaying = true;
        //textDisplayed.SetActive(true);
        NextMonologueLine();
    }

    void NextMonologueLine()
    {
        textDisplayed.text = currentLines.textLines[index];
        audioSource.clip = currentLines.audioLines[index];
        audioSource.Play();

        StartCoroutine(WaitForVoiceToEnd());
    }

    IEnumerator WaitForVoiceToEnd()
    {
        while (audioSource.isPlaying == true)
        {
            yield return null;
        }

        if (currentLines.textLines.Length > index + 1)
        {
            index++;
            MonologueSave.currentIndex = index;
            NextMonologueLine();
        } else 
        {
            EndMonologue();
        }
        
    }

    void EndMonologue()
    {
        MonologueSave.currentLines = null;
        MonologueSave.currentIndex = 0;
        monologuePlaying = false;
        textDisplayed.text = "";
        index = 0;
        //textDisplayed.SetActive(false);
    }
}
