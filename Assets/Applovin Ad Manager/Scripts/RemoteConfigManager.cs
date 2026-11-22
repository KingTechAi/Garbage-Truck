using System;
using Unity.Services.RemoteConfig;
using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Core;
using System.Threading.Tasks;

namespace MangoMango.Advertisement
{
    public struct UserAttributes { }
    public struct AppAttributes { }

    #region Enums
    public enum RCInt
    {
        ControllableBannerPosition,
        MRECBannerPosition,
        InterstitialSkipLevel,
        InterstitialSkipLevelComplete,
        AdDivisionValue,
        TimeBasedAdInt,
        ShowIAPAfter,
        ShowRewardedAfterTimeInterval,
        LoadingTime,
        RateUsPanelTimer,
        AdStrategyMode,
        AdDeliveryType,
        EventAdFrequency,
        SystemRewardedCoins,

        // ── Detection / pacing. These used to be literal floats in the ad manager and in the
        // screen tracker; they are Remote Config keys so every timing decision is tunable live.
        UITransitionDebounceMs,
        TrackerPollMs,
        TrackerDebounceMs,
        TrackerStartDelayMs,
        TimeBasedAdCountdown,
    }
    public enum RCString
    {
        InterstitialAdID,
        TopRightFixedBannerAdUnitId,
        ControllableBannerAdUnitId,
        RewardedAdID,
        MRecAdID,
        AppOpenAdUnitId,

        // Comma separated scene-name substrings. These replace the hard-coded word lists that used to
        // live inside IsUIScene().
        UISceneKeywords,
        GameplaySceneKeywords
    }
    public enum RCBool
    {
        EnableAds,
        TestAds,
        TopRightFixedBanner,
        ControllableBanner,
        ShowRewarded,
        MREC,
        BannerCheckAll,
        RewardCheckAll,
        InterstitialCheckAll,
        EnableAppOpenAD,
        AlternateLevelFail,
        AlternateLevelComplete,
        AdToLoadInGame,
        CharacterCarPurchaseRVRewardAds,
        ShopItem,
        ReviveAds,
        Pause,
        Failed,
        LevelComplete,
        PlaneSelectionToGP,
        ModeSelection,
        InGameInventory,
        GetCoins,
        GetPowerUpRewardAd,
        TimeBasedAdBool,
        ShowIAPAfterAd,
        ShowRewardedAfterTime,
        LevelUnlockAd,
        GetPowerup,
        CharacterCarPurchaseRV,
        reward2x,
        ShowAdTimer,

        // ── Navigation / fallback rules that used to be hard-coded branches.
        BackNavigationAds,
        ReturnToPreviousScreenAds,
        RewardedFallbackToInterstitial
    }
    public enum RCFloat
    {
        LoadingTime
    }
    #endregion

    [DefaultExecutionOrder(-950)]
    public class RemoteConfigManager : MonoBehaviour
    {
        #region Variables
        // Singleton instance
        public static RemoteConfigManager Instance;

        public Action onInitializationComplete;
        public Action onInitializationFail;

        /// <summary>
        /// True once a Remote Config response has actually been received. Callers use this to tell
        /// "Remote Config answered" apart from "Remote Config has not answered yet", because the
        /// getters fall back to the local Inspector defaults until the first response arrives.
        /// </summary>
        [HideInInspector] public bool HasFetchedConfig;
        #endregion

        #region Local Defaults
        // Remote Config is optional. Until the keys below are published in the Unity Cloud
        // dashboard, every getter falls back to these Inspector values, so the ads system is
        // fully usable without any Remote Config access.
        [Header("Ad Unit IDs (used until Remote Config provides them)")]
        public string localInterstitialAdUnitId = "";
        public string localRewardedAdUnitId = "";
        public string localBannerAdUnitId = "";
        public string localControllableBannerAdUnitId = "";
        public string localMRecAdUnitId = "";
        public string localAppOpenAdUnitId = "";

        [Header("Switches (used until Remote Config provides them)")]
        public bool localEnableAds = true;
        public bool localInterstitialCheckAll = true;
        public bool localBannerCheckAll = true;
        public bool localRewardCheckAll = true;
        public bool localShowRewarded = true;
        public bool localControllableBanner = true;
        public bool localTopRightFixedBanner = true;
        public bool localMREC = true;
        public bool localEnableAppOpenAD = false;
        public bool localTestAds = false;

        [Header("Numbers (used until Remote Config provides them)")]
        [Tooltip("0 TopCenter, 1 BottomCenter, 2 TopLeft, 3 TopRight, 4 BottomLeft, 5 BottomRight")]
        public int localControllableBannerPosition = 0;
        [Tooltip("0 BottomLeft, 1 BottomCenter, 2 BottomRight, 3 CenterLeft, 4 Centered, 5 CenterRight, 6 TopLeft, 7 TopCenter, 8 TopRight")]
        public int localMRECBannerPosition = 2;
        [Tooltip("0 Time-Based only, 1 Event-Based only, 2 Hybrid")]
        public int localAdStrategyMode = 1;
        [Tooltip("1 Interstitial only, 2 Rewarded only, 3 Alternating (0 = legacy interstitial)")]
        public int localAdDeliveryType = 3;
        public int localEventAdFrequency = 1;
        public int localSystemRewardedCoins = 0;
        public int localTimeBasedAdInt = 60;
        public float localLoadingTime = 3f;

        [Header("Detection & Pacing (used until Remote Config provides them)")]
        [Tooltip("Milliseconds that swallow a duplicate notification for the same screen.")]
        public int localUITransitionDebounceMs = 800;
        [Tooltip("Milliseconds between UIScreenTracker panel polls.")]
        public int localTrackerPollMs = 100;
        [Tooltip("Milliseconds between two reported panel changes.")]
        public int localTrackerDebounceMs = 500;
        [Tooltip("Milliseconds to wait before the first tracked panel is reported.")]
        public int localTrackerStartDelayMs = 150;
        [Tooltip("Seconds before a time-based ad when the warning countdown appears.")]
        public int localTimeBasedAdCountdown = 5;
        [Tooltip("Allow ads when the player presses Back (default: back navigation is ad-free).")]
        public bool localBackNavigationAds = false;
        [Tooltip("Allow ads when returning to a screen already visited (default: free).")]
        public bool localReturnToPreviousScreenAds = false;
        [Tooltip("When a Rewarded ad is due but not loaded, serve an Interstitial instead.")]
        public bool localRewardedFallbackToInterstitial = true;
        [Tooltip("Comma separated scene-name substrings that mark a UI/menu scene.")]
        public string localUISceneKeywords = "splash,track,menu,loading,select,garage";
        [Tooltip("Comma separated scene-name substrings that mark a gameplay scene.")]
        public string localGameplaySceneKeywords = "farming,game,level,play";

        [Header("Remote Config Environment (optional)")]
        [Tooltip("Environment ID from dashboards.unity.com > Remote Config > Environments. Leave empty to use the project's default environment.")]
        public string environmentId = "";
        #endregion

        #region Initialization
        // How long the ads manager should wait for a cloud answer before the local Inspector
        // defaults are treated as the live configuration.
        private const float FallbackNotifyDelaySeconds = 2f;

        // Retrieve and apply the current key-value pairs from the service on Awake:
        private async void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // initialize Unity's authentication and core services, however check for internet connection
            // in order to fail gracefully without throwing exception if connection does not exist
            bool servicesReady = false;
            if (HasInternetConnection())
            {
                try
                {
                    await InitializeRemoteConfigAsync();
                    servicesReady = true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"<b>[RemoteConfig]</b> Unity Services could not initialise, using local Inspector defaults: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning("<b>[RemoteConfig]</b> No internet connection, using local Inspector defaults.");
            }

            if (servicesReady)
            {
                // Add a listener to apply settings when successfully retrieved, then fetch.
                // Wrapped because Unity Services throws when it could not initialise (unlinked
                // project, service outage); the local Inspector defaults stay in force then.
                try
                {
                    // An explicit environment ID must be set before the first fetch.
                    if (!string.IsNullOrEmpty(environmentId))
                        RemoteConfigService.Instance.SetEnvironmentID(environmentId.Trim());

                    RemoteConfigService.Instance.FetchCompleted += OnInitializationComplete;
                    RemoteConfigService.Instance.FetchConfigs(new UserAttributes(), new AppAttributes());
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"<b>[RemoteConfig]</b> Fetch failed, using local Inspector defaults: {e.Message}");
                }
            }

            // Never leave the ads manager waiting on a response that will not arrive: after this
            // delay the local defaults are announced as the effective configuration.
            Invoke(nameof(NotifyFallbackIfNoResponse), FallbackNotifyDelaySeconds);
        }

        private static bool HasInternetConnection()
        {
            return Application.internetReachability != NetworkReachability.NotReachable;
        }

        private void NotifyFallbackIfNoResponse()
        {
            if (HasFetchedConfig) return;

            HasFetchedConfig = true;
            Debug.Log("<b>[RemoteConfig]</b> No cloud response yet - applying local Inspector defaults.");
            onInitializationComplete?.Invoke();
            LogEffectiveConfig();
        }

        async Task InitializeRemoteConfigAsync()
        {
            // initialize handlers for unity game services
            await UnityServices.InitializeAsync();

            // remote config requires authentication for managing environment information
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        private void OnInitializationComplete(ConfigResponse response)
        {
            HasFetchedConfig = true;

            bool adsEnabled = GetBool(RCBool.EnableAds, false);
            int strategyMode = GetInt(RCInt.AdStrategyMode, 2);
            int deliveryType = GetInt(RCInt.AdDeliveryType, 3);
            int frequency = GetInt(RCInt.EventAdFrequency, 2);
            int coins = GetInt(RCInt.SystemRewardedCoins, 500);
            int timerInterval = GetInt(RCInt.TimeBasedAdInt, 60);
            bool showAdTimer = GetBool(RCBool.ShowAdTimer, true);

            string modeStr = strategyMode switch
            {
                0 => "Time-Based Only (0)",
                1 => "Event-Based Only (1)",
                2 => "Hybrid: Events in UI, Timers in Game (2)",
                _ => $"Unknown ({strategyMode})"
            };

            string deliveryStr = deliveryType switch
            {
                2 => "Only Rewarded + Coins (2)",
                3 => "Alternating (Interstitial <-> Rewarded) (3)",
                _ => "Only Interstitial (1)"
            };

            string freqStr = frequency <= 1
                ? "All Screen Transitions (1:1)"
                : $"Every {frequency} Screen Transitions (Alternating)";

            Debug.Log($"<color=#00FF88><b>╔═════════════════════════════════════════════════════════════════════════╗</b>\n" +
                      $"<b>║                   [RemoteConfig] Config Initialized!                    ║</b>\n" +
                      $"<b>╠═════════════════════════════════════════════════════════════════════════╣</b>\n" +
                      $"<b>║ • EnableAds:</b>             {(adsEnabled ? "<color=#00FF00><b>ENABLED (TRUE)</b></color>" : "<color=#FF3333><b>DISABLED (FALSE)</b></color>")}\n" +
                      $"<b>║ • Ad Strategy Mode:</b>      <b>{modeStr}</b>\n" +
                      $"<b>║ • Ad Delivery Type:</b>      <b>{deliveryStr}</b>\n" +
                      $"<b>║ • Event Ad Frequency:</b>    <b>{freqStr}</b>\n" +
                      $"<b>║ • Time-Based Interval:</b>   <b>{timerInterval} seconds</b>\n" +
                      $"<b>║ • Show Ad Timer Warning:</b> <b>{(showAdTimer ? "<color=#00FF00>YES (TRUE)</color>" : "<color=#FF3333>NO (FALSE)</color>")}</b>\n" +
                      $"<b>║ • System Rewarded Coins:</b> <b>+{coins} Coins</b>\n" +
                      $"<b>╚═════════════════════════════════════════════════════════════════════════╝</b></color>");

            onInitializationComplete?.Invoke();

            LogEffectiveConfig();
        }

        // One-shot report of every key the game can read, and whether the value in force came from
        // the cloud or from the local Inspector fallback. This is the check that "everything is
        // controlled by Remote Config": any line still marked LOCAL is a key that is not published
        // (or is spelled differently) in the Unity Cloud dashboard.
        private void LogEffectiveConfig()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>[RemoteConfig] Effective configuration — RC = from cloud, LOCAL = Inspector fallback</b>");

            foreach (RCBool key in Enum.GetValues(typeof(RCBool)))
                sb.AppendLine($"  {(RcHasKey(key.ToString()) ? "RC   " : "LOCAL")}  {key} = {GetBool(key)}");

            foreach (RCInt key in Enum.GetValues(typeof(RCInt)))
                sb.AppendLine($"  {(RcHasKey(key.ToString()) ? "RC   " : "LOCAL")}  {key} = {GetInt(key)}");

            foreach (RCString key in Enum.GetValues(typeof(RCString)))
                sb.AppendLine($"  {(RcHasKey(key.ToString()) ? "RC   " : "LOCAL")}  {key} = {GetString(key)}");

            foreach (RCFloat key in Enum.GetValues(typeof(RCFloat)))
                sb.AppendLine($"  {(RcHasKey(key.ToString()) ? "RC   " : "LOCAL")}  {key} = {GetFloat(key)}");

            Debug.Log(sb.ToString());
        }
        #endregion

        #region Value Returning
        // Every read is defensive: when Remote Config is unreachable Unity Services throws out of
        // RemoteConfigService.Instance, and these getters are called from Update/OnGUI paths.
        // When the key is not published in Remote Config, the matching local Inspector value is used.
        private static bool RcHasKey(string keyString)
        {
            try
            {
                return !string.IsNullOrEmpty(keyString) && RemoteConfigService.Instance.appConfig.HasKey(keyString);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public int GetInt(RCInt key, int defaultValue = 0)
        {
            string keyString = Enum.GetName(typeof(RCInt), key);
            try
            {
                if (RcHasKey(keyString))
                    return RemoteConfigService.Instance.appConfig.GetInt(keyString, defaultValue);
            }
            catch (Exception)
            {
            }
            return LocalInt(key, defaultValue);
        }

        public string GetString(RCString key, string defaultValue = "")
        {
            string keyString = Enum.GetName(typeof(RCString), key);
            try
            {
                if (RcHasKey(keyString))
                {
                    // Cloud values are typed by hand in a dashboard, so a trailing space or tab is
                    // easy to introduce. MAX matches ad unit IDs exactly, so trim before use.
                    string value = RemoteConfigService.Instance.appConfig.GetString(keyString, defaultValue)?.Trim();
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }
            }
            catch (Exception)
            {
            }
            return LocalString(key, defaultValue);
        }

        public bool GetBool(RCBool key, bool defaultValue = false)
        {
            string keyString = Enum.GetName(typeof(RCBool), key);
            try
            {
                if (RcHasKey(keyString))
                    return RemoteConfigService.Instance.appConfig.GetBool(keyString, defaultValue);
            }
            catch (Exception)
            {
            }
            return LocalBool(key, defaultValue);
        }

        public float GetFloat(RCFloat key, float defaultValue = 0)
        {
            string keyString = Enum.GetName(typeof(RCFloat), key);
            try
            {
                if (RcHasKey(keyString))
                    return RemoteConfigService.Instance.appConfig.GetFloat(keyString, defaultValue);
            }
            catch (Exception)
            {
            }
            return key == RCFloat.LoadingTime ? localLoadingTime : defaultValue;
        }

        private int LocalInt(RCInt key, int fallback)
        {
            switch (key)
            {
                case RCInt.ControllableBannerPosition: return localControllableBannerPosition;
                case RCInt.MRECBannerPosition: return localMRECBannerPosition;
                case RCInt.AdStrategyMode: return localAdStrategyMode;
                case RCInt.AdDeliveryType: return localAdDeliveryType;
                case RCInt.EventAdFrequency: return localEventAdFrequency;
                case RCInt.SystemRewardedCoins: return localSystemRewardedCoins;
                case RCInt.TimeBasedAdInt: return localTimeBasedAdInt;
                case RCInt.UITransitionDebounceMs: return localUITransitionDebounceMs;
                case RCInt.TrackerPollMs: return localTrackerPollMs;
                case RCInt.TrackerDebounceMs: return localTrackerDebounceMs;
                case RCInt.TrackerStartDelayMs: return localTrackerStartDelayMs;
                case RCInt.TimeBasedAdCountdown: return localTimeBasedAdCountdown;
                default: return fallback;
            }
        }

        private string LocalString(RCString key, string fallback)
        {
            string value;
            switch (key)
            {
                case RCString.InterstitialAdID: value = localInterstitialAdUnitId; break;
                case RCString.RewardedAdID: value = localRewardedAdUnitId; break;
                case RCString.TopRightFixedBannerAdUnitId: value = localBannerAdUnitId; break;
                case RCString.ControllableBannerAdUnitId: value = localControllableBannerAdUnitId; break;
                case RCString.MRecAdID: value = localMRecAdUnitId; break;
                case RCString.AppOpenAdUnitId: value = localAppOpenAdUnitId; break;
                case RCString.UISceneKeywords: value = localUISceneKeywords; break;
                case RCString.GameplaySceneKeywords: value = localGameplaySceneKeywords; break;
                default: value = null; break;
            }

            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private bool LocalBool(RCBool key, bool fallback)
        {
            switch (key)
            {
                case RCBool.EnableAds: return localEnableAds;
                case RCBool.InterstitialCheckAll: return localInterstitialCheckAll;
                case RCBool.BannerCheckAll: return localBannerCheckAll;
                case RCBool.RewardCheckAll: return localRewardCheckAll;
                case RCBool.ShowRewarded: return localShowRewarded;
                case RCBool.ControllableBanner: return localControllableBanner;
                case RCBool.TopRightFixedBanner: return localTopRightFixedBanner;
                case RCBool.MREC: return localMREC;
                case RCBool.EnableAppOpenAD: return localEnableAppOpenAD;
                case RCBool.TestAds: return localTestAds;
                case RCBool.BackNavigationAds: return localBackNavigationAds;
                case RCBool.ReturnToPreviousScreenAds: return localReturnToPreviousScreenAds;
                case RCBool.RewardedFallbackToInterstitial: return localRewardedFallbackToInterstitial;
                default: return fallback;
            }
        }
        #endregion
    }
}