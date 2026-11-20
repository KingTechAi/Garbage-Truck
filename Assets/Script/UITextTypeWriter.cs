using UnityEngine;
using System.Collections;
using UnityEngine.UI;

// attach to UI Text component (with the full text already there)

public class UITextTypeWriter : MonoBehaviour 
{
    public string[] MissionsDescription;
	Text txt;
    string story;
    public float TextTypingSpeed;
    public GameObject OkButton;
    public GameObject GameStart;

void Awake()
{
    txt = GetComponent<Text>();
        //story = txt.text;
    story = MissionsDescription[PlayerPrefs.GetInt("CurrentLevel")];
    txt.text = "";

    // TODO: add optional delay when to start
    StartCoroutine(nameof(PlayText));
}

IEnumerator PlayText()
{
    foreach (char c in story)
    {
        txt.text += c;
        yield return new WaitForSeconds(TextTypingSpeed);
    }
        OkButton.SetActive(true);
}




    public void OkButtonPressed()
    {
        StopCoroutine(nameof(PlayText));
        GameManger.Instance.StartWork();
        Time.timeScale = 1;
    }
}