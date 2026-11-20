using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Loading : MonoBehaviour
{
    public float LoadingTime;
    public bool isSplash;
    private void OnEnable()
    {
        Invoke(nameof(Work), LoadingTime);

    }


    void Work()
    {
        if (isSplash)
            SceneManager.LoadScene(1);
        else
        {
            gameObject.SetActive(false);
        }

    }

    public void LoadScene()
    {

    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
