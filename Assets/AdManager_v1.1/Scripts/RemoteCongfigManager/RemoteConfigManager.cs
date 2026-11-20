using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.RemoteConfig;
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

        FetchAdsStatus();
    }

    private async void FetchAdsStatus()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            RuntimeConfig config = await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());
            AdsStatus(config.GetBool("EnableAds", true));
        }
        catch (Exception exception)
        {
            // Without a bound Unity project and a deployed config there is nothing to fetch,
            // so leave ads exactly as they are authored in the scene.
            Debug.LogWarning("Remote Config fetch failed, keeping the scene's ad settings. " + exception.Message);
        }
    }

    void AdsStatus(bool AdsEnable)
    {
        if (adsManager == null)
        {
            return;
        }

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
