using MangoMango.Advertisement;
using UnityEngine;

// Gameplay still calls this API (MainMenu / GameManger). The Google Mobile Ads and Unity Ads
// SDKs are gone; every call is now forwarded to the AppLovin MAX manager, which reads all of its
// switches and IDs from Remote Config.
public class AdsManager : MonoBehaviour
{
    public static AdsManager instance;

    // Kept so the existing _AdsManager prefab instance keeps its serialized data valid.
    public bool TestAds;
    public bool isThreeId;

    public string AdmobAppId;
    public string[] AdmobBannerId;
    public string[] AdmobSquareBannerId;
    public string[] AdmobInterstitialId;
    public string AdmobInterstitialRewardedId;
    public string[] AdmobRewardedVideoId;

    public string UnityGameID;
    public string UnityBannerID;
    public string UnityInterstitialID;
    public string UnityRewardedID;

    public bool Admob_Banner_Simple;
    public bool Admob_Banner_Smart;

    public GameObject GoogleAds;
    public GameObject UnityAds;

    private static ApplovinAdManager Ads => ApplovinAdManager.Instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else if (instance != this)
        {
            Destroy(this);
        }
    }

    public void AdsInit() { }

    #region Banner
    public void Show_AdMob_Banner() => Ads?.ShowBanner();
    public void Show_AdMob_RectangleBanner() => Ads?.ShowMRecBanner();
    public void Hide_AdMob_Banner() => Ads?.HideBanner();
    public void Hide_AdMob_RectangleBanner() => Ads?.HideMRecBanner();
    public void Destroy_AdMob_Banner() => Ads?.HideBanner();

    public void Show_Unity_Banner() => Ads?.ShowBanner();
    public void Hide_Unity_Banner() => Ads?.HideBanner();
    #endregion

    #region Interstitial / Rewarded
    // ShowStrategyAd applies AdStrategyMode, AdDeliveryType, EnableAds and every placement switch
    // from Remote Config, so the old hard-coded call sites stay behaviour-driven by the dashboard.
    public void Show_AdMob_Interstitial() => Ads?.ShowStrategyAd("Gameplay");
    public void Show_Unity_Interstitial() => Ads?.ShowStrategyAd("Gameplay");
    public void Show_AdMob_Interstitial_Rewarded() => Ads?.ShowStrategyAd("Gameplay");
    public void Show_Unity_Rewarded() => Ads?.ShowStrategyAd("Rewarded");
    public void Show_AdMob_Rewarded() => Ads?.ShowRewardedAd(null);

    // The gameplay placements below read their own Remote Config switches, so Pause / LevelComplete /
    // AdToLoadInGame can each be turned off from the dashboard. When a key is not published the
    // switch defaults to on, which keeps the current behaviour.
    public void Show_GameplayStart_Ad() => ShowPlacementAd(RCBool.AdToLoadInGame, "LevelStart");
    public void Show_Pause_Ad() => ShowPlacementAd(RCBool.Pause, "Pause");
    public void Show_LevelComplete_Ad() => ShowPlacementAd(RCBool.LevelComplete, "LevelComplete");

    private static void ShowPlacementAd(RCBool placement, string context)
    {
        var remoteConfig = MangoMango.Advertisement.RemoteConfigManager.Instance;
        if (remoteConfig != null && !remoteConfig.GetBool(placement, true))
        {
            Debug.Log($"[AdsManager] Placement '{placement}' is FALSE in Remote Config -> {context} ad skipped.");
            return;
        }

        Ads?.ShowStrategyAd(context);
    }

    public bool rewardCheck() => Ads != null && Ads.IsRewardedAdAvailable();
    #endregion

    public void OnInterstialOpening() { }
    public void OnInterstialClosed() { }

    public void EnableListners() { }
    public void DisableListners() { }
}
