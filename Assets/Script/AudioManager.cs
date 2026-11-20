using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;
    private void Awake()
    {

        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        DontDestroyOnLoad(this.gameObject);
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    public void MusicOn()
    {
        gameObject.transform.GetChild(0).transform.gameObject.GetComponent<AudioSource>().Play();
    }


    public void MusicOff()
    {
        gameObject.transform.GetChild(0).transform.gameObject.GetComponent<AudioSource>().Pause();
    }
}
