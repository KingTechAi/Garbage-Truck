using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SettingManager : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }
    //vibration
    public void VibrationON()
    {
        Handheld.Vibrate();
    }
    public void VibrationOFF()
    {
        
    }
    //sound
    public void SoundOn()
    {

    }

    public void SoundOff()
    {

    }

    //music
    public void MusicOn()
    {
        AudioManager.Instance.MusicOn();
    }

    public void MusicOff()
    {
        AudioManager.Instance.MusicOff();
    }



    // Update is called once per frame
    void Update()
    {
        
    }
}
