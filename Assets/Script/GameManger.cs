using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManger : MonoBehaviour
{
    public GameObject[] Missions;
    public GameObject[] Trucks;
    public float[] MissionTimmer;
    public GameObject RccCanvus;
    public GameObject GameOverCanvus;


    public static GameManger Instance;


    private void Awake()
    {

        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        Time.timeScale = 1;
        Missions[PlayerPrefs.GetInt("CurrentLevel")].SetActive(true);
        Instantiate(Trucks[PlayerPrefs.GetInt("CurrentVehicle")], Missions[PlayerPrefs.GetInt("CurrentLevel")].transform.GetChild(0).transform.position, Quaternion.identity);
        if (PlayerPrefs.GetInt("TotalUnlocked") == 19)
        {
            GameOverCanvus.transform.GetChild(4).transform.gameObject.GetComponent<Button>().interactable = false;
        }
        
        if(AdsManager.instance)
            AdsManager.instance.Show_AdMob_Interstitial();

    }

    public void StartWork()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    public void ShowGameCompleteCanvus()
    {
        RccCanvus.SetActive(false);
        GameOverCanvus.SetActive(true);
        Debug.Log("complete");
        if(AdsManager.instance)
            AdsManager.instance.Show_AdMob_Interstitial();
        
    }
    

    // game Pause functions
    public void PauseButton()
    {
        Time.timeScale = 0;
        
        if(AdsManager.instance)
            AdsManager.instance.Show_AdMob_Interstitial();
    }

    public void PauseHome()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void PauseRestart()
    {
        SceneManager.LoadScene("GamePlay");
    }

    public void PauseResume()
    {
        Time.timeScale = 1;
    }



    // game over functions
    public void GameOverHome()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void GameOverRestart()
    {
        SceneManager.LoadScene("GamePlay");
    }

    public void GameOverNext()
    {
        // level up here
        if(PlayerPrefs.GetInt("TotalUnlocked") < 19)
        {

          //  if (PlayerPrefs.GetInt("CurrentLevel") ==  PlayerPrefs.GetInt("TotalUnlocked"))
         //   {
                PlayerPrefs.SetInt(("TotalUnlocked"), PlayerPrefs.GetInt("TotalUnlocked") + 1);
         //   }

            PlayerPrefs.SetInt("CurrentLevel" , PlayerPrefs.GetInt("CurrentLevel")+1);


            
        }
        
        SceneManager.LoadScene("GamePlay");
    }

}
