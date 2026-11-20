using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public GameObject MainMenuPanel;
    public GameObject LevelSelectionPanel;
    public GameObject LoadingPanel;
    public GameObject SettingPanel;

    public GameObject[] Levels;

    public string PrivacyLink;
    private int LevelNum;
    // Start is called before the first frame update
    void Start()
    {
        Time.timeScale = 1;
        print(PlayerPrefs.GetInt("TotalUnlocked"));
        CheckLevelsStatus();
        StartCoroutine(Delay());

    }

    IEnumerator Delay()
    {
        yield return new WaitForSeconds(1);
        if(AdsManager.instance)
            AdsManager.instance.Show_AdMob_Banner();
    }


    public void CheckLevelsStatus()
    {
        for (int i = 0; i < Levels.Length; i++)
        {
            if (i <= PlayerPrefs.GetInt("TotalUnlocked"))
            {
                Levels[i].GetComponent<Button>().interactable = true;   
            }
        }
    }
 

    public void BackButton()
    {
        if (LevelSelectionPanel.activeInHierarchy == true && MainMenuPanel.activeInHierarchy == false)
        {
            MainMenuPanel.SetActive(true);
            LevelSelectionPanel.SetActive(false);
        }
        else if (LevelSelectionPanel.activeInHierarchy == false && MainMenuPanel.activeInHierarchy == true)
        {
            Application.Quit();
        }
    }

    public void LevelSelect(int levelNo)
    {
        LoadingPanel.SetActive(true);
        LevelNum = levelNo;
        Invoke(nameof(NowLoadLevel),1.5f);
        
    }
    public void NowLoadLevel()
    {
        PlayerPrefs.SetInt("CurrentLevel", LevelNum);
        SceneManager.LoadScene("GamePlay");
    }

    public void PlayButton()
    {
        MainMenuPanel.SetActive(false);
        LevelSelectionPanel.SetActive(true);
        
        if(AdsManager.instance)
            AdsManager.instance.Show_AdMob_Interstitial();
    }

    public void RateUsButton()
    {
        Application.OpenURL("market://details?id=" + Application.identifier);
    }


    public void SettingButton()
    {
        SettingPanel.SetActive(true);
    }


    public void PolicyButton()
    {
        Application.OpenURL(PrivacyLink);
    }


    public void SettingsCheck()
    {
        if (PlayerPrefs.GetInt("SoundVol") == 1)
        {
            //sound on

        }
        else
        {
            //sound off

        }
        if (PlayerPrefs.GetInt("MusicVol") == 1)
        {
            //Music on

        }
        else
        {
            //Music off

        }
        if (PlayerPrefs.GetInt("VibrationVol") == 1)
        {
            //Vibration on

        }
        else
        {
            //Vibration off
            
        }
    }



}
