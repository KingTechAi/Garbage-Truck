using UnityEngine.Events;
using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Collections;

public class GoogleAdMobManager : MonoBehaviour
{
    #region UNITY Variables

    //========== Ad Calling =============
    private BannerView bannerView,rectBannerView,leftBannerView;
    private InterstitialAd interstitialAd;
    public RewardedAd rewardedAd;
    private RewardedInterstitialAd rewardedInterstitialAd;

    public UnityEvent OnAdLoadedEvent;
    public UnityEvent OnAdFailedToLoadEvent;
    public UnityEvent OnAdOpeningEvent;
    public UnityEvent OnAdFailedToShowEvent;
    public UnityEvent OnUserEarnedRewardEvent;
    public UnityEvent OnAdClosedEvent;


    private int bannerAdIndex = 0;
    private int squareBannerAdIndex = 0;
    private int interstitialAdIndex = 0;
    private int rewardedAdIndex = 0;
    
    public Text statusText;
    public Text RewardText;

    #endregion

    #region UNITY MONOBEHAVIOR METHODS

    public void Start()
    {
        MobileAds.SetiOSAppPauseOnBackground(true);

        List<String> deviceIds = new List<String>() { AdRequest.TestDeviceSimulator };

        // Add some test device IDs (replace with your own device IDs).
#if UNITY_IPHONE
        deviceIds.Add("96e23e80653bb28980d3f40beb58915c");
#elif UNITY_ANDROID
        deviceIds.Add("75EF8D155528C04DACBBA6F36F433035");
#endif

        // Configure TagForChildDirectedTreatment and test device IDs.
        RequestConfiguration requestConfiguration =
            new RequestConfiguration.Builder()
            .SetTagForChildDirectedTreatment(TagForChildDirectedTreatment.Unspecified)
            .SetTestDeviceIds(deviceIds).build();
        MobileAds.SetRequestConfiguration(requestConfiguration);

        // Initialize the Google Mobile Ads SDK.
        MobileAds.Initialize(HandleInitCompleteAction);
      
    }

    private void HandleInitCompleteAction(InitializationStatus initstatus)
    {
        Debug.Log("Initialization complete.");

        // Callbacks from GoogleMobileAds are not guaranteed to be called on
        // the main thread.
        // In this example we use MobileAdsEventExecutor to schedule these calls on
        // the next Update() loop.
        MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
//            statusText.text = "Initialization complete.";          
        });
        RequestBannerAd();
       // RequestRectangleAd();
       // RequestLeftBannerAd();
        RequestAndLoadInterstitialAd();
        //RequestAndLoadRewardedInterstitialAd();
        RequestAndLoadRewardedAd();

    }

  
    #endregion

    #region HELPER METHODS

    private AdRequest CreateAdRequest()
    {
        return new AdRequest.Builder()
            .AddKeyword("unity-admob-sample")
            .Build();
    }

    #endregion

    #region BANNER ADS

    public void RequestBannerAd()
    {

        PrintStatus("Requesting Banner ad.");

        // These ad units are configured to always serve test ads.
#if UNITY_EDITOR
        string adUnitId = "unused";
#elif UNITY_ANDROID
        string adUnitId = AdsManager.instance.AdmobBannerId[bannerAdIndex];
#elif UNITY_IPHONE
        string adUnitId = AdsManager.instance.AdmobBannerId[bannerAdIndex];
#else
        string adUnitId = "unexpected_platform";
#endif

        // Clean up banner before reusing
        if (bannerView != null)
        {
            bannerView.Destroy();
        }

        // Create a 320x50 banner at top of the screen
        if (AdsManager.instance.Admob_Banner_Simple && !AdsManager.instance.Admob_Banner_Smart)
        {
            bannerView = new BannerView(adUnitId, AdSize.Banner, AdsManager.instance.Admob_Banner_Position);
        }
        else if (AdsManager.instance.Admob_Banner_Smart && !AdsManager.instance.Admob_Banner_Simple)
        {
            bannerView = new BannerView(adUnitId, AdSize.SmartBanner, AdsManager.instance.Admob_Banner_Position);
        }
        else if (!AdsManager.instance.Admob_Banner_Simple && !AdsManager.instance.Admob_Banner_Smart)
        {
            bannerView = new BannerView(adUnitId, AdSize.Banner, AdsManager.instance.Admob_Banner_Position);
        }
        else if (AdsManager.instance.Admob_Banner_Simple && AdsManager.instance.Admob_Banner_Smart)
        {
            bannerView = new BannerView(adUnitId, AdSize.Banner, AdsManager.instance.Admob_Banner_Position);
        }

        // Add Event Handlers
        bannerView.OnAdLoaded += (sender, args) =>
        {
            bannerAdIndex = 0;
            PrintStatus("Banner ad loaded.");
            OnAdLoadedEvent.Invoke();
        };
        bannerView.OnAdFailedToLoad += (sender, args) =>
        {
            PrintStatus("Banner ad failed to load with error: "+args.LoadAdError.GetMessage());
            OnAdFailedToLoadEvent.Invoke();

            if (bannerAdIndex < 2 && AdsManager.instance.isThreeId)
            {
                StartCoroutine(BannerRequestDelay());
            }
        };
        bannerView.OnAdOpening += (sender, args) =>
        {
            PrintStatus("Banner ad opening.");
            OnAdOpeningEvent.Invoke();
        };
        bannerView.OnAdClosed += (sender, args) =>
        {
            PrintStatus("Banner ad closed.");
            OnAdClosedEvent.Invoke();
        };
        bannerView.OnPaidEvent += (sender, args) =>
        {
            string msg = string.Format("{0} (currency: {1}, value: {2}",
                                        "Banner ad received a paid event.",
                                        args.AdValue.CurrencyCode,
                                        args.AdValue.Value);
            PrintStatus(msg);
        };

        // Load a banner ad
        bannerView.LoadAd(CreateAdRequest());
        if (bannerView != null)
        {
            bannerView.Hide();
        }
    }

   private IEnumerator BannerRequestDelay()
   {
        yield return new WaitForSeconds(0.1f);
        bannerAdIndex++; 
        RequestBannerAd();
   }
    
    public void RequestRectangleAd()
    {
        
        // These ad units are configured to always serve test ads.
#if UNITY_EDITOR
        string adUnitId = "unused";
#elif UNITY_ANDROID
        string adUnitId = AdsManager.instance.AdmobSquareBannerId[squareBannerAdIndex];
#elif UNITY_IPHONE
        string adUnitId = AdsManager.instance.AdmobSquareBannerId[squareBannerAdIndex];
#else
        string adUnitId = "unexpected_platform";
#endif

        // Clean up banner before reusing
        if (rectBannerView != null)
        {
            rectBannerView.Destroy();
        }
        
        rectBannerView = new BannerView(adUnitId, AdSize.MediumRectangle, AdPosition.TopLeft);
        rectBannerView.LoadAd(CreateAdRequest()); 
        
        // Add Event Handlers
        rectBannerView.OnAdLoaded += (sender, args) =>
        {
            squareBannerAdIndex = 0;
            PrintStatus("Banner ad loaded.");
            OnAdLoadedEvent.Invoke();
        };
        rectBannerView.OnAdFailedToLoad += (sender, args) =>
        {
            PrintStatus("Banner ad failed to load with error: "+args.LoadAdError.GetMessage());
            OnAdFailedToLoadEvent.Invoke();

            if (squareBannerAdIndex < 2 && AdsManager.instance.isThreeId)
            {
                StartCoroutine(SquareBannerRequestDelay());
            }
          
        };
        rectBannerView.OnAdOpening += (sender, args) =>
        {
            PrintStatus("Banner ad opening.");
            OnAdOpeningEvent.Invoke();
        };
        rectBannerView.OnAdClosed += (sender, args) =>
        {
            PrintStatus("Banner ad closed.");
            OnAdClosedEvent.Invoke();
        };
        rectBannerView.OnPaidEvent += (sender, args) =>
        {
            string msg = string.Format("{0} (currency: {1}, value: {2}",
                "Banner ad received a paid event.",
                args.AdValue.CurrencyCode,
                args.AdValue.Value);
            PrintStatus(msg);
        };
        
        if (rectBannerView != null)
        {
            rectBannerView.Hide();
        }
    }
  private IEnumerator SquareBannerRequestDelay()
   {
        yield return new WaitForSeconds(0.1f);
        squareBannerAdIndex++; 
        RequestRectangleAd();
   }
    
    public void RequestLeftBannerAd()
    {
        
        // These ad units are configured to always serve test ads.
#if UNITY_EDITOR
        string adUnitId = "unused";
#elif UNITY_ANDROID
        string adUnitId = AdsManager.instance.AdmobBannerId[bannerAdIndex];
#elif UNITY_IPHONE
        string adUnitId = AdsManager.instance.AdmobBannerId[bannerAdIndex];
#else
        string adUnitId = "unexpected_platform";
#endif

        // Clean up banner before reusing
        if (leftBannerView != null)
        {
            leftBannerView.Destroy();
        }
        
        leftBannerView = new BannerView(adUnitId, AdSize.Banner, AdPosition.TopRight);
        leftBannerView.LoadAd(CreateAdRequest()); 
        
        // Add Event Handlers
        leftBannerView.OnAdLoaded += (sender, args) =>
        {
            bannerAdIndex = 0;
            PrintStatus("Banner ad loaded.");
            OnAdLoadedEvent.Invoke();
        };
        leftBannerView.OnAdFailedToLoad += (sender, args) =>
        {
            PrintStatus("Banner ad failed to load with error: "+args.LoadAdError.GetMessage());
            OnAdFailedToLoadEvent.Invoke();

            if (bannerAdIndex < 2 && AdsManager.instance.isThreeId)
            {
               StartCoroutine(LeftBannerRequestDelay());
            }
           
        };
        leftBannerView.OnAdOpening += (sender, args) =>
        {
            PrintStatus("Banner ad opening.");
            OnAdOpeningEvent.Invoke();
        };
        leftBannerView.OnAdClosed += (sender, args) =>
        {
            PrintStatus("Banner ad closed.");
            OnAdClosedEvent.Invoke();
        };
        leftBannerView.OnPaidEvent += (sender, args) =>
        {
            string msg = string.Format("{0} (currency: {1}, value: {2}",
                "Banner ad received a paid event.",
                args.AdValue.CurrencyCode,
                args.AdValue.Value);
            PrintStatus(msg);
        };
        
        if (leftBannerView != null)
        {
            leftBannerView.Hide();
        }
    }

   private IEnumerator LeftBannerRequestDelay()
   {
        yield return new WaitForSeconds(0.1f);
         bannerAdIndex++; 
         RequestLeftBannerAd();
   }

    public void ShowBannerAdmob()
    {
        if (bannerView != null)
        {
            bannerView.Show();
        }
    }
    
    public void ShowRectangleBannerAdmob()
    {
        HideBannerAdmob();
        if (rectBannerView != null)
        {
            rectBannerView.Show();
        }

        if (leftBannerView != null)
        {
            leftBannerView.Show();
        }
    }
    public void HideBannerAdmob()
    {
        if (bannerView != null)
        {
            bannerView.Hide();
        }
    }
    
    public void HideRectangleAdmob()
    {
        ShowBannerAdmob();
        if (rectBannerView != null)
        {
            rectBannerView.Hide();
        }
        if (leftBannerView != null)
        {
            leftBannerView.Hide();
        }
    }

    public void DestroyBannerAdAdmob()
    {
        if (bannerView != null)
        {
            bannerView.Destroy();
        }
    }

    #endregion

    #region INTERSTITIAL ADS

    public void RequestAndLoadInterstitialAd()
    {
        PrintStatus("Requesting Interstitial ad.");

#if UNITY_EDITOR
        string adUnitId = "unused";
#elif UNITY_ANDROID
        string adUnitId = AdsManager.instance.AdmobInterstitialId[interstitialAdIndex];
#elif UNITY_IPHONE
        string adUnitId = AdsManager.instance.AdmobInterstitialId[interstitialAdIndex];
#else
        string adUnitId = "unexpected_platform";
#endif
        
        // Clean up interstitial before using it
        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
        }

        interstitialAd = new InterstitialAd(adUnitId);

        // Add Event Handlers
        interstitialAd.OnAdLoaded += (sender, args) =>
        {
            interstitialAdIndex = 0;
            PrintStatus("Interstitial ad loaded.");
            OnAdLoadedEvent.Invoke();
        };
        interstitialAd.OnAdFailedToLoad += (sender, args) =>
        {
            PrintStatus("Interstitial ad failed to load with error: "+args.LoadAdError.GetMessage());
            OnAdFailedToLoadEvent.Invoke();

            if (interstitialAdIndex < 2 && AdsManager.instance.isThreeId)
            {
               
                StartCoroutine(InterstitalRequestDelay());
            }
            
        };
        interstitialAd.OnAdOpening += (sender, args) =>
        {
            PrintStatus("Interstitial ad opening.");
            OnAdOpeningEvent.Invoke();
            AdsManager.instance.OnInterstialOpening();
        };
        interstitialAd.OnAdClosed += (sender, args) =>
        {
            PrintStatus("Interstitial ad closed.");
            OnAdClosedEvent.Invoke();
            RequestAndLoadInterstitialAd(); 
            AdsManager.instance.OnInterstialClosed();
        };
        interstitialAd.OnAdDidRecordImpression += (sender, args) =>
        {
            PrintStatus("Interstitial ad recorded an impression.");
        };
        interstitialAd.OnAdFailedToShow += (sender, args) =>
        {
            PrintStatus("Interstitial ad failed to show.");
        };
        interstitialAd.OnPaidEvent += (sender, args) =>
        {
            string msg = string.Format("{0} (currency: {1}, value: {2}",
                                        "Interstitial ad received a paid event.",
                                        args.AdValue.CurrencyCode,
                                        args.AdValue.Value);
            PrintStatus(msg);
        };

        // Load an interstitial ad
        interstitialAd.LoadAd(CreateAdRequest());
    }

   private IEnumerator InterstitalRequestDelay()
   {
        yield return new WaitForSeconds(0.1f);
        interstitialAdIndex++;
        RequestAndLoadInterstitialAd();
   }

    public void ShowInterstitialAd()
    {
        if (interstitialAd != null && interstitialAd.IsLoaded())
        {
            interstitialAd.Show();
            Debug.Log("Interstitial ad is ready and show.");
        }
        else
        {
            AdsManager.instance.Show_Unity_Interstitial();
            RequestAndLoadInterstitialAd();
            PrintStatus("Interstitial ad is not ready yet.");
        }
    }

    public void DestroyInterstitialAd()
    {
        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
        }
    }

    #endregion

    #region REWARDED ADS

    public void RequestAndLoadRewardedAd()
    {
        PrintStatus("Requesting Rewarded ad.");
#if UNITY_EDITOR
        string adUnitId = "unused";
#elif UNITY_ANDROID
        string adUnitId = AdsManager.instance.AdmobRewardedVideoId[rewardedAdIndex];
#elif UNITY_IPHONE
        string adUnitId = AdsManager.instance.AdmobRewardedVideoId[rewardedAdIndex];
#else
        string adUnitId = "unexpected_platform";
#endif

        // create new rewarded ad instance
        rewardedAd = new RewardedAd(adUnitId);

        // Add Event Handlers
        rewardedAd.OnAdLoaded += (sender, args) =>
        {
            rewardedAdIndex = 0;
            PrintStatus("Reward ad loaded.");
            OnAdLoadedEvent.Invoke();
        };
        rewardedAd.OnAdFailedToLoad += (sender, args) =>
        {
            PrintStatus("Reward ad failed to load.");
            OnAdFailedToLoadEvent.Invoke();
            
            if (rewardedAdIndex < 2 && AdsManager.instance.isThreeId)
            {
                StartCoroutine(RewardedRequestDelay());
            }
        };
        rewardedAd.OnAdOpening += (sender, args) =>
        {
            PrintStatus("Reward ad opening.");
            OnAdOpeningEvent.Invoke();
            AdsManager.instance.DisableListners();
        };
        rewardedAd.OnAdFailedToShow += (sender, args) =>
        {
            PrintStatus("Reward ad failed to show with error: "+args.AdError.GetMessage());
            OnAdFailedToShowEvent.Invoke();
        };
        rewardedAd.OnAdClosed += (sender, args) =>
        {
            PrintStatus("Reward ad closed.");
            OnAdClosedEvent.Invoke();
            AdsManager.instance.EnableListners();
            RequestAndLoadRewardedAd();
        };
        rewardedAd.OnUserEarnedReward += (sender, args) =>
        {
            PrintStatus("User earned Reward ad reward: "+args.Amount);
            RewardHandler.instance.GiveReward();
            OnUserEarnedRewardEvent.Invoke();
        };
        rewardedAd.OnAdDidRecordImpression += (sender, args) =>
        {
            PrintStatus("Reward ad recorded an impression.");
        };
        rewardedAd.OnPaidEvent += (sender, args) =>
        {
            string msg = string.Format("{0} (currency: {1}, value: {2}",
                                        "Rewarded ad received a paid event.",
                                        args.AdValue.CurrencyCode,
                                        args.AdValue.Value);
            PrintStatus(msg);
        };

        // Create empty ad request
        rewardedAd.LoadAd(CreateAdRequest());
    }

private IEnumerator RewardedRequestDelay()
   {
        yield return new WaitForSeconds(0.1f);
        rewardedAdIndex++;
        RequestAndLoadRewardedAd();
   }
   

    public void ShowRewardedAd()
    {
        if (rewardedAd != null)
        {
            rewardedAd.Show();
            RequestAndLoadRewardedAd();
        }
        else
        {
            RequestAndLoadRewardedAd();
            PrintStatus("Rewarded ad is not ready yet.");
        }
    }

    public void RequestAndLoadRewardedInterstitialAd()
    {
        PrintStatus("Requesting Rewarded Interstitial ad.");

        // These ad units are configured to always serve test ads.
#if UNITY_EDITOR
        string adUnitId = "unused";
#elif UNITY_ANDROID
            string adUnitId = AdsManager.instance.AdmobInterstitialRewardedId;
#elif UNITY_IPHONE
            string adUnitId = AdsManager.instance.AdmobInterstitialRewardedId;
#else
            string adUnitId = "unexpected_platform";
#endif

        // Create an interstitial.
        RewardedInterstitialAd.LoadAd(adUnitId, CreateAdRequest(), (rewardedInterstitialAd, error) =>
        {
            if (error != null)
            {
                PrintStatus("Rewarded Interstitial ad load failed with error: " + error);
                return;
            }

            this.rewardedInterstitialAd = rewardedInterstitialAd;
            PrintStatus("Rewarded Interstitial ad loaded.");

            // Register for ad events.
            this.rewardedInterstitialAd.OnAdDidPresentFullScreenContent += (sender, args) =>
            {
                PrintStatus("Rewarded Interstitial ad presented.");
            };
            this.rewardedInterstitialAd.OnAdDidDismissFullScreenContent += (sender, args) =>
            {
                PrintStatus("Rewarded Interstitial ad dismissed.");
                this.rewardedInterstitialAd = null;
            };
            this.rewardedInterstitialAd.OnAdFailedToPresentFullScreenContent += (sender, args) =>
            {
                PrintStatus("Rewarded Interstitial ad failed to present with error: "+
                                                                        args.AdError.GetMessage());
                this.rewardedInterstitialAd = null;
            };
            this.rewardedInterstitialAd.OnPaidEvent += (sender, args) =>
            {
                string msg = string.Format("{0} (currency: {1}, value: {2}",
                                            "Rewarded Interstitial ad received a paid event.",
                                            args.AdValue.CurrencyCode,
                                            args.AdValue.Value);
                PrintStatus(msg);
            };
            this.rewardedInterstitialAd.OnAdDidRecordImpression += (sender, args) =>
            {
                PrintStatus("Rewarded Interstitial ad recorded an impression.");
            };
        });
    }

    public void ShowRewardedInterstitialAd()
    {
        if (rewardedInterstitialAd != null)
        {
            rewardedInterstitialAd.Show((reward) =>
            {
                PrintStatus("Rewarded Interstitial ad Rewarded : " + reward.Amount);
                RequestAndLoadRewardedInterstitialAd();
            });
        }
        else
        {
            PrintStatus("Rewarded Interstitial ad is not ready yet.");
        }
    }

    #endregion    

    #region Utility

    ///<summary>
    /// Log the message and update the status text on the main thread.
    ///<summary>
    private void PrintStatus(string message)
    {
        Debug.Log(message);
        
    }

    #endregion
}
