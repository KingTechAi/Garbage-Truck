using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoInternet : MonoBehaviour
{
    public static NoInternet instance;
    public GameObject Internet_Panel,panel;
    //public GameObject ref_Loading_Screen;

    bool isRetryPressed;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != null)
        {
            Destroy(gameObject);
        }
        DontDestroyOnLoad(this.gameObject);
    }
    void Start()
    {

        StartCoroutine(InternetChecking_Function());
    }

    IEnumerator InternetChecking_Function()
    {
        while (true)
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Internet_Panel.SetActive(true);
                Time.timeScale = 0f;
                Debug.Log("Error. Check internet connection!");
            }
            else
            {
                // open loading
                //ref_Loading_Screen.GetComponent<loadingScene>().ref_selectSceneType = loadingScene.SceneType.splash;
                //ref_Loading_Screen.SetActive(true);

                Internet_Panel.SetActive(false);
                Time.timeScale = 1f;
            }
            yield return null;
        }


    }
    private void Update()
    {

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Internet_Panel.SetActive(true);

            Time.timeScale = 0;

        }


        if (isRetryPressed)
        {
            panel.transform.localScale = Vector3.Lerp(panel.transform.localScale, Vector3.one, Time.unscaledDeltaTime * 2);
            var diff = Vector3.one - panel.transform.localScale;
            if (Mathf.Approximately(diff.magnitude, panel.transform.localScale.magnitude))
            {
                isRetryPressed = false;
            }
        }
    }
    public void Retry_Function()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            panel.transform.localScale = Vector3.zero;
            isRetryPressed = true;
        }
        else
        {
            Time.timeScale = 1;
            Internet_Panel.SetActive(false);
        }
    }

}
