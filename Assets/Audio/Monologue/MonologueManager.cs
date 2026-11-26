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
        if (MonologueSave.playing == true)
        {
            currentLines = MonologueSave.currentLines;
            index = MonologueSave.currentIndex;
            StartMonologue(MonologueSave.currentLines, MonologueSave.playAtPosition);
        }
    }

    public void StartMonologue(MonologueLines newLines, float positionToPlayAt = 0)
    {
        MonologueSave.playing = true;
        currentLines = newLines;
        MonologueSave.currentLines = currentLines;
        monologuePlaying = true;
        textDisplayed.gameObject.SetActive(true);
        NextMonologueLine(positionToPlayAt);
    }

    void NextMonologueLine(float positionToPlayAt = 0)
    {
        textDisplayed.text = currentLines.textLines[index];
        audioSource.clip = currentLines.audioLines[index];
        if (positionToPlayAt != 0 && positionToPlayAt < audioSource.clip.length)
        {
            audioSource.time = positionToPlayAt;
            audioSource.Play();
        } else 
        {
            audioSource.Play();
        }
        

        StartCoroutine(WaitForVoiceToEnd());
    }

    IEnumerator WaitForVoiceToEnd()
    {
        
        while (audioSource.isPlaying == true)
        {
            MonologueSave.playAtPosition += Time.deltaTime;
            yield return null;
        }
       

        MonologueSave.playAtPosition = 0.0f;

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
        MonologueSave.playing = false;

        monologuePlaying = false;
        textDisplayed.text = "";
        index = 0;
        textDisplayed.gameObject.SetActive(false);
    }
}
