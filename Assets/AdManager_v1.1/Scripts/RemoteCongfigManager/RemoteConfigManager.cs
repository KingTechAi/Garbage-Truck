using System;
using System.Collections;
using System.Collections.Generic;
using Unity.RemoteConfig;
using UnityEngine;


public struct UserAttributes
{
        
}
    
public struct AppAttributes
{
    
}


public class RemoteConfigManager : MonoBehaviour
{
    public static RemoteConfigManager instance;
    public GameObject adsManager;

    [HideInInspector] public bool isAdsEnable;
    private void Awake()
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
        
        ConfigManager.FetchCompleted += AdsStatus;
        ConfigManager.FetchConfigs<UserAttributes,AppAttributes>(new UserAttributes(), new AppAttributes());
    }
    
    void AdsStatus(ConfigResponse response)
    {
        bool AdsEnable = ConfigManager.appConfig.GetBool("EnableAds");
        isAdsEnable = AdsEnable;
        if (!AdsEnable)
        {
            
                Destroy(adsManager.gameObject);

        }
        else
        {
            adsManager.SetActive(true);            
        }
    }
}
