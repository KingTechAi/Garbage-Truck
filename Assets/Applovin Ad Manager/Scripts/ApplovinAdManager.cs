using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if MangoMango_IAP
using MangoMango.IAP;
#endif

namespace MangoMango.Advertisement
{
    [DefaultExecutionOrder(-900)]
    public class ApplovinAdManager : MonoBehaviour
    {
        public static ApplovinAdManager Instance;

#if  MangoMango_IAP
        private bool isRemoveAdsPurchased
        {
            get
            {
                // Checks local save memory and Apple/Google store receipts
                bool isLocallySaved = PlayerPrefs.GetInt("IsAdsRemoved", 0) == 1 || PlayerPrefs.GetInt("RemoveAdsPurchased", 0) == 1;
                bool isIapPurchased = IAPManager.Instance != null &&
                                     (IAPManager.Instance.IsPurchased(ProductName.RemoveAds) ||
                                      IAPManager.Instance.IsPurchased(ProductName.BestBundle));
                return isLocallySaved || isIapPurchased;
            }
        }
#endif

        #region Ad Ids
        private string InterstitialAdUnitId;
        private string RewardedAdUnitId;
        private string TopRightFixedBanner;
        private string ControllableBannerAdUnitId;
        private string MRecAdUnitId;
        private string AppOpenAdUnitId;
        // private const string RewardedInterstitialAdUnitId = "ENTER_ANDROID_REWARD_INTER_AD_UNIT_ID_HERE";
        #endregion

        #region Remote Config Snapshot
        // Snapshot of what is currently live inside MAX. A later Remote Config response is compared
        // against this so only the values that actually changed are re-applied to the live ads.
        private string appliedInterstitialId, appliedRewardedId;
        private string appliedControllableBannerId, appliedFixedBannerId, appliedMRecId;
        private int appliedControllableBannerPosition = int.MinValue;
        private int appliedMRecPosition = int.MinValue;
        private bool appOpenCallbacksRegistered;
        #endregion

        #region Scene Classification
        [Header("Scene Classification")]
        [Tooltip("List of scenes classified as UI / Menu scenes (used for Hybrid Mode & event-based screen transitions).")]
        public System.Collections.Generic.List<string> uiSceneNames = new System.Collections.Generic.List<string>()
        {
            "MainMenu", "Splash", "Loading", "LoadingA", "LoadingB"
        };

        [Tooltip("List of scenes where App Open ads should NOT be shown (e.g. initial Splash).")]
        public System.Collections.Generic.List<string> appOpenExcludedScenes = new System.Collections.Generic.List<string>()
        {
            "Splash", "ATTScene"
        };
        #endregion

        #region Other Variables
        private int _interstitialRetryAttempt;
        private int _rewardedRetryAttempt;
        private int _rewardedInterstitialRetryAttempt;
        private int AdDivisionValue;
        public delegate void RewardCallback();
        // Define an event for the reward callback
        private event RewardCallback OnRewardReceived;
        public Action onInitializationComplete;
        public Action onInitializationFail;
        public static Action<int> OnRewardedAdCoinsGranted;
        public static Action<bool> OnAdsRemovedStateChanged;
        bool isMaxSDKInitialized = false;
        private bool remoteConfigFetched;
        [HideInInspector] public bool isAppPausedByAd;

        /// <summary>
        /// True from the moment a full-screen ad is requested until it is dismissed or fails to show.
        /// It spans the pre-ad countdown as well, so a loading overlay can wait for the whole ad
        /// sequence instead of racing it. Unlike isAppPausedByAd it is always cleared on failure.
        /// </summary>
        [HideInInspector] public bool IsFullscreenAdVisible;
        #endregion

        #region Initialization
        private void Awake()
        {
            if (Instance == null) { Instance = this; }
            else if (Instance != null) { Destroy(gameObject); }
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // Set once a gameplay scene has loaded. Menu transitions before the first level (Splash ->
        // MainMenu at app launch) never trigger an event ad; afterwards RC drives them again.
        private bool hasStartedGameplay;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsUIScene())
            {
                hasStartedGameplay = true;
                ResetScreenHistory();
                HideMRecBanner();
            }
            else if (isMaxSDKInitialized && hasStartedGameplay)
            {
                // This project navigates between menus as scenes rather than panels, so a UI scene
                // load is the screen transition the event-based strategy (AdStrategyMode,
                // EventAdFrequency, UISceneKeywords) is written for.
                NotifyUIScreenTransition(scene.name);
            }

            // Banner visibility is a Remote Config decision, not a scene-type decision. ShowBanner()
            // re-checks EnableAds / BannerCheckAll / ControllableBanner / TopRightFixedBanner on every
            // call, so a banner that Remote Config says should be up is never left down on a screen.
            ShowBanner();
        }

        private IEnumerator Start()
        {
#if MangoMango_IAP
            // IAP is optional: the define can be on while the IAP Manager prefab is not in the
            // scene, so never assume the singleton exists.
            if (IAPManager.Instance != null)
            {
                IAPManager.Instance.onPurchaseSuccess += OnPurchaseSuccess;

                // If IAP is ALREADY done loading, check receipts right now!
                if (IAPManager.Instance.isInitialized)
                {
                    CheckReceiptsOnBoot();
                }
                else
                {
                    // Otherwise, wait for it to finish.
                    IAPManager.Instance.onInitializationComplete += CheckReceiptsOnBoot;
                }
            }
#endif
            if (RemoteConfigManager.Instance != null)
                RemoteConfigManager.Instance.onInitializationComplete += FetchRemoteValues;
            MaxSdkCallbacks.OnSdkInitializedEvent += InitializeAds;
            isAppPausedByAd = false;

            // Remote Config fetches from its own Awake, so it can answer before this Start runs. In
            // that case the event above never fires for us and the wait below would burn 10s.
            if (RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.HasFetchedConfig) FetchRemoteValues();

            yield return new WaitForSeconds(2);

            // Wait for Remote Config first: the ad unit IDs come from there, and
            // MaxSdk.CreateBanner / CreateMRec throw an ArgumentNullException on a null ID.
            float waited = 0f;
            while (!remoteConfigFetched && waited < 10f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!remoteConfigFetched)
            {
                Debug.LogWarning("<b>[ApplovinAdManager]</b> Remote Config did not answer in time - using default ad unit IDs.");
            }

            // Always pull the current values (Remote Config falls back to the "test" defaults),
            // so no ad unit ID can ever be null when MAX is initialised below.
            FetchRemoteValues();

            // The SDK key is no longer set from code: MaxSdk.SetSdkKey is deprecated, and on iOS it
            // still forwarded the old hardcoded key to the native SDK, overriding the current key
            // from the AppLovin Integration Manager (Assets/MaxSdk/Resources/AppLovinSettings.asset).
            MaxSdk.InitializeSdk();
        }

        private void RegisterAppOpenCallbacksIfEnabled()
        {
            if (appOpenCallbacksRegistered || RemoteConfigManager.Instance == null) return;
            if (!RemoteConfigManager.Instance.GetBool(RCBool.EnableAds)) return;
            if (!RemoteConfigManager.Instance.GetBool(RCBool.EnableAppOpenAD)) return;

            appOpenCallbacksRegistered = true;
            MaxSdkCallbacks.AppOpen.OnAdHiddenEvent += OnAppOpenDismissedEvent;
            ShowAppOpenIfReady();
        }

        private void InitializeAds(MaxSdkBase.SdkConfiguration configuration)
        {
            isMaxSDKInitialized = true;
            Debug.Log("MAX SDK Initialized");
            PlayerPrefs.SetInt("OnConsentGranted", 1);
            RegisterAppOpenCallbacksIfEnabled();

            InitializeInterstitialAds();
            InitializeRewardedAds();
            RegisterBannerCallbacks();
            RegisterMRecCallbacks();
            InitializeControllableBannerAds();
            InitializeFixedBannerAds();
            InitializeMRecAds();

            // Auto-start time-based ad timer when ads SDK is ready
            if (MangoMango.Advertisement.TimeBasedAd.Instance != null)
            {
                MangoMango.Advertisement.TimeBasedAd.Instance.StartAdTimer();
            }

            onInitializationComplete?.Invoke();
        }
        #endregion

        #region IAP Methods
#if MangoMango_IAP
        private void OnPurchaseSuccess(ProductName name)
        {
            if (name == ProductName.RemoveAds || name == ProductName.BestBundle)
            {
                PlayerPrefs.SetInt("IsAdsRemoved", 1);
                PlayerPrefs.SetInt("RemoveAdsPurchased", 1);
                PlayerPrefs.Save();
                OnAdsRemovedStateChanged?.Invoke(true);
                TrySetPlayerSaveManagerAdsRemoved(true);

                // --- FIXED: Only destroy the banners if the IDs have actually downloaded! ---
                if (!IsPlaceholderAdUnitId(ControllableBannerAdUnitId)) MaxSdk.DestroyBanner(ControllableBannerAdUnitId);
                if (!IsPlaceholderAdUnitId(TopRightFixedBanner)) MaxSdk.DestroyBanner(TopRightFixedBanner);
                if (!IsPlaceholderAdUnitId(MRecAdUnitId)) MaxSdk.DestroyMRec(MRecAdUnitId);

                Debug.Log("Remove Ads Purchased, Ads will not be shown.");
            }
        }

        private void CheckReceiptsOnBoot()
        {
            if (isRemoveAdsPurchased)
            {
                Debug.Log("IAP Initialized: Player owns Remove Ads! Destroying banners...");

                // --- FIXED: Only destroy the banners if the IDs have actually downloaded! ---
                if (!IsPlaceholderAdUnitId(ControllableBannerAdUnitId)) MaxSdk.DestroyBanner(ControllableBannerAdUnitId);
                if (!IsPlaceholderAdUnitId(TopRightFixedBanner)) MaxSdk.DestroyBanner(TopRightFixedBanner);
                if (!IsPlaceholderAdUnitId(MRecAdUnitId)) MaxSdk.DestroyMRec(MRecAdUnitId);
            }
        }

#endif 
        #endregion

        #region Interstitial Ad Methods

        private void InitializeInterstitialAds()
        {
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterstitialLoadedEvent;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterstitialFailedEvent;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += InterstitialFailedToDisplayEvent;

            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += OnInterstitialDisplayedEvent;

            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterstitialDismissedEvent;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += OnInterstitialRevenuePaidEvent;

            appliedInterstitialId = InterstitialAdUnitId;
            LoadInterstitial();
        }

        public void LoadInterstitial()
        {
            MaxSdk.LoadInterstitial(InterstitialAdUnitId);
        }

        public bool IsInterstitialReady()
        {
            return !string.IsNullOrEmpty(InterstitialAdUnitId) && MaxSdk.IsInterstitialReady(InterstitialAdUnitId);
        }

        public void ShowInterstitial(RCBool adBool)
        {
#if MangoMango_IAP
            if (isRemoveAdsPurchased)
            {
                Debug.Log("<color=yellow><b>[ApplovinAdManager]</b> 🚫 Remove Ads is active -> Interstitial ad suppressed.</color>");
                return;
            }
#endif
            if (RemoteConfigManager.Instance != null && !RemoteConfigManager.Instance.GetBool(RCBool.EnableAds, true))
            {
                Debug.Log("<color=red><b>[ApplovinAdManager]</b> ❌ EnableAds is FALSE in Remote Config -> Interstitial ad suppressed.</color>");
                return;
            }

            var interstitialCheckAll = RemoteConfigManager.Instance != null ? RemoteConfigManager.Instance.GetBool(RCBool.InterstitialCheckAll, true) : true;
            var adPlacementEnabled = RemoteConfigManager.Instance != null ? RemoteConfigManager.Instance.GetBool(adBool, true) : true;

            if (!interstitialCheckAll)
            {
                Debug.Log("<color=red><b>[ApplovinAdManager]</b> ❌ InterstitialCheckAll is FALSE in Remote Config -> Interstitial ad suppressed.</color>");
                return;
            }

            if (!adPlacementEnabled)
            {
                Debug.Log($"<color=red><b>[ApplovinAdManager]</b> ❌ Placement '{adBool}' is FALSE in Remote Config -> Interstitial ad suppressed.</color>");
                return;
            }

            // MAX keys ads by unit ID; a null ID (Remote Config not applied yet) would throw.
            if (IsPlaceholderAdUnitId(InterstitialAdUnitId))
            {
                Debug.LogWarning("<b>[ApplovinAdManager]</b> ⏳ Please wait a moment, loading ad settings from Remote Config...");
                return;
            }

            isAppPausedByAd = true;
            IsFullscreenAdVisible = true;

            if (MaxSdk.IsInterstitialReady(InterstitialAdUnitId))
            {
                // Announce the ad with the shared countdown card first, then display it.
                if (TryStartAdCountdown(() => DisplayInterstitialAd(adBool)))
                {
                    return;
                }

                DisplayInterstitialAd(adBool);
            }
            else
            {
                Debug.LogWarning($"<b>[ApplovinAdManager]</b> ⚠️ Interstitial ad NOT ready yet for Unit '{InterstitialAdUnitId}'. Requesting new load...");
                isAppPausedByAd = false;
                IsFullscreenAdVisible = false;
                LoadInterstitial();
            }
        }

        /// <summary>
        /// Hands the pre-ad countdown card to TimeBasedAd. Returns true when the countdown took over:
        /// the ad is then opened by onComplete once the card finishes, so the caller must not show it.
        /// </summary>
        private bool TryStartAdCountdown(Action onComplete)
        {
            return MangoMango.Advertisement.TimeBasedAd.Instance != null &&
                   MangoMango.Advertisement.TimeBasedAd.Instance.TryStartAdCountdown(onComplete);
        }

        private void DisplayInterstitialAd(RCBool adBool)
        {
            if (!MaxSdk.IsInterstitialReady(InterstitialAdUnitId))
            {
                Debug.LogWarning($"<b>[ApplovinAdManager]</b> ⚠️ Interstitial ad NOT ready yet for Unit '{InterstitialAdUnitId}'. Requesting new load...");
                IsFullscreenAdVisible = false;
                LoadInterstitial();
                return;
            }

            isAppPausedByAd = true;
            IsFullscreenAdVisible = true;
            Debug.Log($"<color=#FFA726><b>[ApplovinAdManager]</b> 📺 <b>SHOWING INTERSTITIAL AD</b> (Placement: <i>{adBool}</i> | Unit: <i>{InterstitialAdUnitId}</i>)</color>");
            MaxSdk.ShowInterstitial(InterstitialAdUnitId);
        }

        private void OnInterstitialLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log($"<color=#66BB6A><b>[ApplovinAdManager]</b> ✅ Interstitial Loaded successfully (Network: {adInfo.NetworkName})</color>");
            _interstitialRetryAttempt = 0;
            // if (AnalyticsManager.Instance != null)
            // {
            //     AnalyticsManager.Instance.LogAdRequest(GAAdType.Interstitial, "interstitial");
            // }
        }

        private void OnInterstitialFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            _interstitialRetryAttempt++;
            var retryDelay = Math.Pow(2, Math.Min(6, _interstitialRetryAttempt));
            Debug.LogWarning($"<b>[ApplovinAdManager]</b> ❌ Interstitial failed to load: {errorInfo.Message} (Code: {errorInfo.Code}). Retrying in {retryDelay:F1}s...");
            // if (AnalyticsManager.Instance != null)
            // {
            //     AnalyticsManager.Instance.LogAdFailed(GAAdType.Interstitial, "interstitial", errorInfo.Message);
            // }
            Invoke("LoadInterstitial", (float)retryDelay);
        }

        private void InterstitialFailedToDisplayEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            Debug.LogWarning($"<b>[ApplovinAdManager]</b> ❌ Interstitial failed to display: {errorInfo.Message} (Code: {errorInfo.Code})");
            IsFullscreenAdVisible = false;
            isAppPausedByAd = false;
            // if (AnalyticsManager.Instance != null)
            // {
            //     AnalyticsManager.Instance.LogAdFailed(GAAdType.Interstitial, "interstitial", errorInfo.Message);
            // }
            LoadInterstitial();
        }

        // ─── THE FIX 2: Freeze the game when the ad opens ───
        private void OnInterstitialDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("<color=#FFA726><b>[ApplovinAdManager]</b> 📺 Interstitial Displayed -> Game paused.</color>");
            IsFullscreenAdVisible = true;
            Time.timeScale = 0f;
            AudioListener.pause = true; // Mute game audio so it doesn't play over the ad!

            // if (AnalyticsManager.Instance != null)
            // {
            //     AnalyticsManager.Instance.LogAdShow(GAAdType.Interstitial, "interstitial");
            // }
        }

        private void OnInterstitialDismissedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            isAppPausedByAd = false;
            IsFullscreenAdVisible = false;
            Debug.Log("<color=#FFA726><b>[ApplovinAdManager]</b> 📺 Interstitial Dismissed -> Game resumed.</color>");

            // ─── THE FIX 3: Unfreeze the game when the ad closes ───
            Time.timeScale = 1f;
            AudioListener.pause = false;

            LoadInterstitial();

            // A full-screen ad covers the banner; put it back according to Remote Config.
            ShowBanner();

            if (MangoMango.Advertisement.TimeBasedAd.Instance != null)
            {
                MangoMango.Advertisement.TimeBasedAd.Instance.StartAdTimer();
                MangoMango.Advertisement.TimeBasedAd.Instance.CheckAndShowIAPPanel();
            }
        }

        private void OnInterstitialRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log($"<color=#FFD54F><b>[ApplovinAdManager]</b> 💵 Interstitial Revenue Paid: ${adInfo.Revenue:F4} ({adInfo.NetworkName})</color>");
            double revenue = adInfo.Revenue;
            SendRevenueStatus(adInfo, revenue);
        }

        #endregion

        #region Rewarded Ad Methods

        private void InitializeRewardedAds()
        {
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRewardedAdLoadedEvent;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnRewardedAdFailedEvent;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRewardedAdFailedToDisplayEvent;
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += OnRewardedAdDisplayedEvent;
            MaxSdkCallbacks.Rewarded.OnAdClickedEvent += OnRewardedAdClickedEvent;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRewardedAdDismissedEvent;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardedAdReceivedRewardEvent;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnRewardedAdRevenuePaidEvent;

            appliedRewardedId = RewardedAdUnitId;
            LoadRewardedAd();
        }

        private void LoadRewardedAd()
        {
            MaxSdk.LoadRewardedAd(RewardedAdUnitId);
        }

        public void ShowRewardedAd(RewardCallback callback)
        {
            // --- FIXED: Failsafe if they click the button before Remote Config finishes loading ---
            if (IsPlaceholderAdUnitId(RewardedAdUnitId))
            {
                Debug.LogWarning("<b>[ApplovinAdManager]</b> ⏳ Please wait a moment, loading ad settings from Remote Config...");
                return;
            }

            if (RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.GetBool(RCBool.ShowRewarded, true))
            {
                if (MaxSdk.IsRewardedAdReady(RewardedAdUnitId))
                {
                    // Announce the ad with the shared countdown card first, then display it.
                    if (TryStartAdCountdown(() => DisplayRewardedAd(callback)))
                    {
                        return;
                    }

                    DisplayRewardedAd(callback);
                }
                else
                {
                    Debug.LogWarning($"<b>[ApplovinAdManager]</b> ⚠️ Rewarded Ad was not ready yet for Unit '{RewardedAdUnitId}'. Requesting new load...");
                    LoadRewardedAd();
                }
            }
            else
            {
                Debug.Log("<color=red><b>[ApplovinAdManager]</b> ❌ ShowRewarded is FALSE in Remote Config -> Rewarded ad suppressed.</color>");
            }
        }

        private void DisplayRewardedAd(RewardCallback callback)
        {
            if (!MaxSdk.IsRewardedAdReady(RewardedAdUnitId))
            {
                Debug.LogWarning($"<b>[ApplovinAdManager]</b> ⚠️ Rewarded Ad was not ready yet for Unit '{RewardedAdUnitId}'. Requesting new load...");
                IsFullscreenAdVisible = false;
                LoadRewardedAd();
                return;
            }

            isAppPausedByAd = true;
            IsFullscreenAdVisible = true;
            OnRewardReceived = callback;
            Debug.Log($"<color=#66BB6A><b>[ApplovinAdManager]</b> 🎁 <b>SHOWING REWARDED AD</b> (Unit: <i>{RewardedAdUnitId}</i>)</color>");
            MaxSdk.ShowRewardedAd(RewardedAdUnitId);
        }

        public bool RewardChecks(RCBool rewardedAdBool)
        {
            if (RemoteConfigManager.Instance.GetBool(RCBool.RewardCheckAll))
            {
                if (RemoteConfigManager.Instance.GetBool(rewardedAdBool) && RewardCheckMAX())
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public bool RewardCheckMAX()
        {
            int numberOfTries = 3;
            int delayMilliseconds = 1000;

            for (int i = 0; i < numberOfTries; i++)
            {
                if (MaxSdk.IsRewardedAdReady(RewardedAdUnitId))
                {
                    return true;
                }
                System.Threading.Thread.Sleep(delayMilliseconds);
            }
            return false;
        }

        // Convenience wrapper used by other systems to check if a rewarded ad is available to show.
        public bool IsRewardedAdAvailable()
        {
            // Basic sanity checks: ad unit configured and remote config allows rewarded ads
            if (IsPlaceholderAdUnitId(RewardedAdUnitId)) return false;
            if (RemoteConfigManager.Instance == null) return false;
            if (!RemoteConfigManager.Instance.GetBool(RCBool.ShowRewarded, true)) return false;

            return MaxSdk.IsRewardedAdReady(RewardedAdUnitId);
        }

        private void OnRewardedAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log($"<color=#66BB6A><b>[ApplovinAdManager]</b> ✅ Rewarded Ad Loaded successfully (Network: {adInfo.NetworkName})</color>");
            _rewardedRetryAttempt = 0;
            // if (AnalyticsManager.Instance != null)
            // {
            //     AnalyticsManager.Instance.LogAdRequest(GAAdType.RewardedVideo, "rewarded");
            // }
        }

        private void OnRewardedAdFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            _rewardedRetryAttempt++;
            var retryDelay = Math.Pow(2, Math.Min(6, _rewardedRetryAttempt));
            Debug.LogWarning($"<b>[ApplovinAdManager]</b> ❌ Rewarded Ad failed to load: {errorInfo.Message} (Code: {errorInfo.Code}). Retrying in {retryDelay:F1}s...");
            // if (AnalyticsManager.Instance != null)
            // {
            //     AnalyticsManager.Instance.LogAdFailed(GAAdType.RewardedVideo, "rewarded", errorInfo.Message);
            // }
            Invoke("LoadRewardedAd", (float)retryDelay);
        }

        private void OnRewardedAdFailedToDisplayEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            Debug.LogWarning($"<b>[ApplovinAdManager]</b> ❌ Rewarded Ad failed to display: {errorInfo.Message} (Code: {errorInfo.Code})");
            // if (AnalyticsManager.Instance != null)
            // {
            //     AnalyticsManager.Instance.LogAdFailed(GAAdType.RewardedVideo, "rewarded", errorInfo.Message);
            // }
            LoadRewardedAd();
        }

        private void OnRewardedAdDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("<color=#66BB6A><b>[ApplovinAdManager]</b> 🎁 Rewarded Ad Displayed -> Game paused.</color>");
            IsFullscreenAdVisible = true;

            // ─── THE FIX 4: Freeze Rewarded Ads too! ───
            Time.timeScale = 0f;
            AudioListener.pause = true;

            // if (AnalyticsManager.Instance != null)
            // {
            //     AnalyticsManager.Instance.LogAdShow(GAAdType.RewardedVideo, "rewarded");
            // }
        }

        private void OnRewardedAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("<color=#66BB6A><b>[ApplovinAdManager]</b> 👆 Rewarded Ad clicked.</color>");
        }

        private void OnRewardedAdDismissedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            isAppPausedByAd = false;
            IsFullscreenAdVisible = false;
            Debug.Log("<color=#66BB6A><b>[ApplovinAdManager]</b> 🎁 Rewarded Ad Dismissed -> Game resumed.</color>");

            // ─── THE FIX 5: Unfreeze Rewarded Ads ───
            Time.timeScale = 1f;
            AudioListener.pause = false;

            LoadRewardedAd();

            // A full-screen ad covers the banner; put it back according to Remote Config.
            ShowBanner();

            // Keep the time-based pacing alive after a rewarded ad (no-op when already running).
            if (MangoMango.Advertisement.TimeBasedAd.Instance != null)
            {
                MangoMango.Advertisement.TimeBasedAd.Instance.StartAdTimer();
            }
        }

        private void OnRewardedAdReceivedRewardEvent(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log($"<color=#FFD700><b>[ApplovinAdManager]</b> 🏆 Rewarded Ad reward earned (Label: {reward.Label}, Amount: {reward.Amount})</color>");
            // if (AnalyticsManager.Instance != null)
            // {
            //     AnalyticsManager.Instance.LogAdRewardReceived("rewarded");
            // }
            OnRewardReceived?.Invoke();
            // Clear the callback so it doesn't fire multiple times
            OnRewardReceived = null;
        }

        private void OnRewardedAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log($"<color=#FFD54F><b>[ApplovinAdManager]</b> 💵 Rewarded Ad Revenue Paid: ${adInfo.Revenue:F4} ({adInfo.NetworkName})</color>");
            double revenue = adInfo.Revenue;
            SendRevenueStatus(adInfo, revenue);
        }

        #endregion

        #region Rewarded Interstitial Ad Methods

        private void InitializeRewardedInterstitialAds()
        {
            MaxSdkCallbacks.RewardedInterstitial.OnAdLoadedEvent += OnRewardedInterstitialAdLoadedEvent;
            MaxSdkCallbacks.RewardedInterstitial.OnAdLoadFailedEvent += OnRewardedInterstitialAdFailedEvent;
            MaxSdkCallbacks.RewardedInterstitial.OnAdDisplayFailedEvent += OnRewardedInterstitialAdFailedToDisplayEvent;
            MaxSdkCallbacks.RewardedInterstitial.OnAdDisplayedEvent += OnRewardedInterstitialAdDisplayedEvent;
            MaxSdkCallbacks.RewardedInterstitial.OnAdClickedEvent += OnRewardedInterstitialAdClickedEvent;
            MaxSdkCallbacks.RewardedInterstitial.OnAdHiddenEvent += OnRewardedInterstitialAdDismissedEvent;
            MaxSdkCallbacks.RewardedInterstitial.OnAdReceivedRewardEvent += OnRewardedInterstitialAdReceivedRewardEvent;
            MaxSdkCallbacks.RewardedInterstitial.OnAdRevenuePaidEvent += OnRewardedInterstitialAdRevenuePaidEvent;

            LoadRewardedInterstitialAd();
        }

        private void LoadRewardedInterstitialAd() { }

        private void ShowRewardedInterstitialAd() { }

        private void OnRewardedInterstitialAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("Rewarded interstitial ad loaded");
            _rewardedInterstitialRetryAttempt = 0;
        }

        private void OnRewardedInterstitialAdFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            _rewardedInterstitialRetryAttempt++;
            double retryDelay = Math.Pow(2, Math.Min(6, _rewardedInterstitialRetryAttempt));
            Debug.Log("Rewarded interstitial ad failed to load with error code: " + errorInfo.Code);
            Invoke("LoadRewardedInterstitialAd", (float)retryDelay);
        }

        private void OnRewardedInterstitialAdFailedToDisplayEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("Rewarded interstitial ad failed to display with error code: " + errorInfo.Code);
            LoadRewardedInterstitialAd();
        }

        private void OnRewardedInterstitialAdDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("Rewarded interstitial ad displayed");
        }

        private void OnRewardedInterstitialAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("Rewarded interstitial ad clicked");
        }

        private void OnRewardedInterstitialAdDismissedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("Rewarded interstitial ad dismissed");
            LoadRewardedInterstitialAd();
        }

        private void OnRewardedInterstitialAdReceivedRewardEvent(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("Rewarded interstitial ad received reward");
        }

        private void OnRewardedInterstitialAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("Rewarded interstitial ad revenue paid");
            double revenue = adInfo.Revenue;
            SendRevenueStatus(adInfo, revenue);
        }

        #endregion

        #region Banner Ad Methods

        // MAX has no "smart" (adaptive-width) banner - exactly two standard banners are used:
        // the controllable banner and the fixed right banner. Registered once so each banner
        // event is reported a single time instead of once per banner.
        private void RegisterBannerCallbacks()
        {
            MaxSdkCallbacks.Banner.OnAdLoadedEvent += OnBannerAdLoadedEvent;
            MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += OnBannerAdFailedEvent;
            MaxSdkCallbacks.Banner.OnAdClickedEvent += OnBannerAdClickedEvent;
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += OnBannerAdRevenuePaidEvent;
        }

        private void InitializeFixedBannerAds()
        {
            // Plain standard banner pinned to the top-right - no adaptive/smart width is requested.
            appliedFixedBannerId = TopRightFixedBanner;
            MaxSdk.CreateBanner(TopRightFixedBanner, BannerConfig(MaxSdkBase.AdViewPosition.TopRight));

            var transparentBlack = new Color(0f, 0f, 0f, 0f);
            MaxSdk.SetBannerBackgroundColor(TopRightFixedBanner, transparentBlack);
        }

        // Remote Config positions are plain int codes. These mappers are shared by the initial
        // create and by the live re-apply path, so both always agree on the same codes.
        private static MaxSdkBase.AdViewPosition ToBannerPosition(int position)
        {
            return position switch
            {
                0 => MaxSdkBase.AdViewPosition.TopCenter,
                1 => MaxSdkBase.AdViewPosition.BottomCenter,
                2 => MaxSdkBase.AdViewPosition.TopLeft,
                3 => MaxSdkBase.AdViewPosition.TopRight,
                4 => MaxSdkBase.AdViewPosition.BottomLeft,
                5 => MaxSdkBase.AdViewPosition.BottomRight,
                _ => MaxSdkBase.AdViewPosition.TopCenter
            };
        }

        // AdViewConfiguration defaults IsAdaptive to true. MAX has no "smart" (adaptive-width)
        // banner in use here - exactly two standard banners are - so the flag is cleared on every
        // banner creation to keep the standard, fixed-width banner the project has always shown.
        private static MaxSdkBase.AdViewConfiguration BannerConfig(MaxSdkBase.AdViewPosition position)
        {
            return new MaxSdkBase.AdViewConfiguration(position) { IsAdaptive = false };
        }

        private static MaxSdkBase.AdViewPosition ToAdViewPosition(int position)
        {
            return position switch
            {
                0 => MaxSdkBase.AdViewPosition.BottomLeft,
                1 => MaxSdkBase.AdViewPosition.BottomCenter,
                2 => MaxSdkBase.AdViewPosition.BottomRight,
                3 => MaxSdkBase.AdViewPosition.CenterLeft,
                4 => MaxSdkBase.AdViewPosition.Centered,
                5 => MaxSdkBase.AdViewPosition.CenterRight,
                6 => MaxSdkBase.AdViewPosition.TopLeft,
                7 => MaxSdkBase.AdViewPosition.TopCenter,
                8 => MaxSdkBase.AdViewPosition.TopRight,
                _ => MaxSdkBase.AdViewPosition.BottomLeft
            };
        }

        private static int ReadInt(RCInt key, int fallback)
        {
            try
            {
                return RemoteConfigManager.Instance != null
                    ? RemoteConfigManager.Instance.GetInt(key, fallback)
                    : fallback;
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        private static bool ReadBool(RCBool key, bool fallback)
        {
            try
            {
                return RemoteConfigManager.Instance != null
                    ? RemoteConfigManager.Instance.GetBool(key, fallback)
                    : fallback;
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        private static string ReadString(RCString key, string fallback)
        {
            try
            {
                return RemoteConfigManager.Instance != null
                    ? RemoteConfigManager.Instance.GetString(key, fallback)
                    : fallback;
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        private void InitializeControllableBannerAds()
        {
            appliedControllableBannerId = ControllableBannerAdUnitId;
            appliedControllableBannerPosition = ReadInt(RCInt.ControllableBannerPosition, 0);

            MaxSdk.CreateBanner(ControllableBannerAdUnitId, BannerConfig(ToBannerPosition(appliedControllableBannerPosition)));

            var transparentBlack = new Color(0f, 0f, 0f, 0f);
            MaxSdk.SetBannerBackgroundColor(ControllableBannerAdUnitId, transparentBlack);
        }

        public void ShowBanner()
        {
            // Remote Config is the only source of banner truth; without it there is nothing to read.
            if (RemoteConfigManager.Instance == null) return;

#if MangoMango_IAP
            if (isRemoveAdsPurchased)
            {
                Debug.Log("<color=yellow><b>[ApplovinAdManager]</b> Remove Ads Purchased -> Banner ad suppressed.</color>");
                return;
            }
#endif

            if (!RemoteConfigManager.Instance.GetBool(RCBool.EnableAds)) return;
            if (!RemoteConfigManager.Instance.GetBool(RCBool.BannerCheckAll)) return;

            if (RemoteConfigManager.Instance.GetBool(RCBool.ControllableBanner))
            {
                Debug.Log($"<color=#42A5F5><b>[ApplovinAdManager]</b> 🖼️ Showing Controllable Banner (Unit: {ControllableBannerAdUnitId})</color>");
                MaxSdk.ShowBanner(ControllableBannerAdUnitId);
            }
            if (RemoteConfigManager.Instance.GetBool(RCBool.TopRightFixedBanner))
            {
                Debug.Log($"<color=#42A5F5><b>[ApplovinAdManager]</b> 🖼️ Showing Top-Right Fixed Banner (Unit: {TopRightFixedBanner})</color>");
                MaxSdk.ShowBanner(TopRightFixedBanner);
            }
        }

        public void HideBanner()
        {
            if (!IsPlaceholderAdUnitId(ControllableBannerAdUnitId)) MaxSdk.HideBanner(ControllableBannerAdUnitId);
            if (!IsPlaceholderAdUnitId(TopRightFixedBanner)) MaxSdk.HideBanner(TopRightFixedBanner);
        }

        private static void OnBannerAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log($"<color=#66BB6A><b>[ApplovinAdManager]</b> ✅ Banner Loaded successfully (Network: {adInfo.NetworkName})</color>");
        }

        private static void OnBannerAdFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            Debug.LogWarning($"<b>[ApplovinAdManager]</b> ❌ Banner failed to load: {errorInfo.Message} (Code: {errorInfo.Code})");
        }

        private static void OnBannerAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("<color=#42A5F5><b>[ApplovinAdManager]</b> 👆 Banner clicked.</color>");
        }

        private void OnBannerAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log($"<color=#FFD54F><b>[ApplovinAdManager]</b> 💵 Banner Revenue Paid: ${adInfo.Revenue:F4} ({adInfo.NetworkName})</color>");
            double revenue = adInfo.Revenue;
            SendRevenueStatus(adInfo, revenue);
        }

        #endregion

        #region MREC Ad Methods

        private void RegisterMRecCallbacks()
        {
            MaxSdkCallbacks.MRec.OnAdLoadedEvent += OnMRecAdLoadedEvent;
            MaxSdkCallbacks.MRec.OnAdLoadFailedEvent += OnMRecAdLoadFailedEvent;
            MaxSdkCallbacks.MRec.OnAdClickedEvent += OnMRecAdClickedEvent;
            MaxSdkCallbacks.MRec.OnAdRevenuePaidEvent += OnMRecAdRevenuePaidEvent;
        }

        private void InitializeMRecAds()
        {
            appliedMRecId = MRecAdUnitId;
            appliedMRecPosition = ReadInt(RCInt.MRECBannerPosition, 2);

            MaxSdk.CreateMRec(MRecAdUnitId, new MaxSdkBase.AdViewConfiguration(ToAdViewPosition(appliedMRecPosition)));
        }


        public void ShowMRecBanner()
        {
#if MangoMango_IAP
            if (isRemoveAdsPurchased)
            {
                Debug.Log("<color=yellow><b>[ApplovinAdManager]</b> Remove Ads Purchased -> MRec ad suppressed.</color>");
                return;
            }
#endif

            if (!RemoteConfigManager.Instance.GetBool(RCBool.EnableAds)) return;
            if (!RemoteConfigManager.Instance.GetBool(RCBool.MREC)) return;

            Debug.Log($"<color=#AB47BC><b>[ApplovinAdManager]</b> 🔲 Showing MRec Banner (Unit: {MRecAdUnitId})</color>");
            MaxSdk.ShowMRec(MRecAdUnitId);
        }

        public void HideMRecBanner()
        {
            if (string.IsNullOrEmpty(MRecAdUnitId)) return;

            Debug.Log($"<color=#AB47BC><b>[ApplovinAdManager]</b> 🙈 Hiding MRec Banner (Unit: {MRecAdUnitId})</color>");
            MaxSdk.HideMRec(MRecAdUnitId);
        }

        private void OnMRecAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            if (RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.GetBool(RCBool.MREC, false))
            {
                Debug.Log($"<color=#66BB6A><b>[ApplovinAdManager]</b> ✅ MRec Loaded successfully (Network: {adInfo.NetworkName})</color>");
            }
        }

        private void OnMRecAdLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            if (RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.GetBool(RCBool.MREC, false))
            {
                Debug.LogWarning($"<b>[ApplovinAdManager]</b> ❌ MRec failed to load: {errorInfo.Message} (Code: {errorInfo.Code})");
            }
        }

        private void OnMRecAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("<color=#AB47BC><b>[ApplovinAdManager]</b> 👆 MRec clicked.</color>");
        }

        private void OnMRecAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log($"<color=#FFD54F><b>[ApplovinAdManager]</b> 💵 MRec Revenue Paid: ${adInfo.Revenue:F4} ({adInfo.NetworkName})</color>");
            double revenue = adInfo.Revenue;
            SendRevenueStatus(adInfo, revenue);
        }

        #endregion

        #region AppOpen
        public void OnAppOpenDismissedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("<color=#26A69A><b>[ApplovinAdManager]</b> 🚪 App Open Ad dismissed.</color>");
            MaxSdk.LoadAppOpenAd(AppOpenAdUnitId);
            ShowBanner();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus && isMaxSDKInitialized)
            {
                string activeScene = SceneManager.GetActiveScene().name;
                bool isExcluded = appOpenExcludedScenes != null && appOpenExcludedScenes.Contains(activeScene);
                if (!isExcluded)
                {
                    ShowAppOpenIfReady();
                }
            }
        }
        public void ShowAppOpenIfReady()
        {
#if MangoMango_IAP
            if (isRemoveAdsPurchased)
            {
                Debug.Log("<color=yellow><b>[ApplovinAdManager]</b> Remove Ads Purchased -> App Open ad suppressed.</color>");
                return;
            }
#endif
            if (!RemoteConfigManager.Instance.GetBool(RCBool.EnableAds)) return;
            if (!RemoteConfigManager.Instance.GetBool(RCBool.EnableAppOpenAD)) return;

            if (MaxSdk.IsAppOpenAdReady(AppOpenAdUnitId) && !isAppPausedByAd)
            {
                Debug.Log($"<color=#26A69A><b>[ApplovinAdManager]</b> 🚪 <b>SHOWING APP OPEN AD</b> (Unit: <i>{AppOpenAdUnitId}</i>)</color>");
                MaxSdk.ShowAppOpenAd(AppOpenAdUnitId);
                HideBanner();
            }
            else
            {
                MaxSdk.LoadAppOpenAd(AppOpenAdUnitId);
            }
        }
        #endregion

        #region Ad Strategy & Hybrid Handling
        [HideInInspector] public int uiScreenTransitionCount = 0;
        [HideInInspector] public int strategyAdCycleCount = 0;

        private string _currentActiveScreen = null;
        private readonly List<string> _globalScreenHistory = new List<string>();
        private float _lastTransitionTime = -10f;
        private bool _strategyModeWarningLogged;

        /// <summary>
        /// Gets the current active screen name tracked across scenes.
        /// </summary>
        public string GetCurrentActiveScreen() => _currentActiveScreen;

        /// <summary>
        /// Reads AdStrategyMode and normalises it to one of the three defined modes.
        /// 0 = Timers only, 1 = UI events only, 2 = Hybrid. Anything else is a dashboard typo and is
        /// treated as Hybrid, so no scene can silently fall outside every gate. The warning is emitted
        /// once per session - this method is called on every screen transition and every timer tick.
        /// </summary>
        public int GetStrategyMode()
        {
            int mode = RemoteConfigManager.Instance != null ? RemoteConfigManager.Instance.GetInt(RCInt.AdStrategyMode, 2) : 2;
            if (mode < 0 || mode > 2)
            {
                if (!_strategyModeWarningLogged)
                {
                    _strategyModeWarningLogged = true;
                    Debug.LogWarning($"<b>[AdStrategy]</b> ⚠️ RC AdStrategyMode = <b>{mode}</b> is not a defined mode (0 = Timers only, 1 = UI events only, 2 = Hybrid). Using <b>2 (Hybrid)</b>. This warning is logged once per session.");
                }
                return 2;
            }
            return mode;
        }

        /// <summary>
        /// True when an ad of the kind the current AdDeliveryType would serve is loaded and ready.
        /// Used by timer pacing so 'ready' always means the same thing the strategy will actually show.
        /// </summary>
        public bool IsStrategyAdReady()
        {
            int deliveryType = RemoteConfigManager.Instance != null
                ? RemoteConfigManager.Instance.GetInt(RCInt.AdDeliveryType, 3)
                : 3;

            switch (deliveryType)
            {
                case 2: return IsRewardedAdAvailable();                       // Only Rewarded
                case 3: return IsRewardedAdAvailable() || IsInterstitialReady(); // Alternating
                default: return IsInterstitialReady();                        // Only Interstitial
            }
        }

        /// <summary>
        /// Clears navigation history (e.g. when entering gameplay scene or resetting menus).
        /// </summary>
        public void ResetScreenHistory()
        {
            _globalScreenHistory.Clear();
            _currentActiveScreen = null;
        }

        /// <summary>
        /// Checks if current active scene is a UI/Menu scene or active gameplay based on configured scene list.
        /// </summary>
        public bool IsUIScene()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName)) return true;

            string lower = sceneName.ToLowerInvariant();

            // Both word lists are Remote Config strings, not compiled-in literals.
            if (ContainsAnyKeyword(lower, ReadString(RCString.UISceneKeywords, "splash,track,menu,loading,select,garage")))
            {
                return true;
            }

            if (uiSceneNames != null && uiSceneNames.Count > 0)
            {
                if (uiSceneNames.Exists(s => !string.IsNullOrEmpty(s) && (sceneName.Equals(s, System.StringComparison.OrdinalIgnoreCase) || sceneName.StartsWith(s, System.StringComparison.OrdinalIgnoreCase))))
                {
                    return true;
                }
            }

            // Anything that matches a gameplay keyword is gameplay; everything else is treated as UI.
            return !ContainsAnyKeyword(lower, ReadString(RCString.GameplaySceneKeywords, "farming,game,level,play"));
        }

        /// <summary>
        /// True when the scene name contains any of the comma separated keywords.
        /// </summary>
        private static bool ContainsAnyKeyword(string lowerSceneName, string commaSeparatedKeywords)
        {
            if (string.IsNullOrEmpty(commaSeparatedKeywords)) return false;

            foreach (string raw in commaSeparatedKeywords.Split(','))
            {
                string keyword = raw.Trim().ToLowerInvariant();
                if (keyword.Length > 0 && lowerSceneName.Contains(keyword)) return true;
            }

            return false;
        }

        /// <summary>
        /// Called when navigating between UI panels / screens (via UIScreenTracker, buttons, or scenes).
        /// Respects AdStrategyMode (0: Time-Based only, 1: Event-Based only, 2: Hybrid)
        /// and EventAdFrequency (1 = All screen transitions, 2 = Alternating screens, N = every Nth screen).
        /// Automatically tracks forward vs back navigation across scenes and ensures Back navigation is 100% ad-free.
        /// </summary>
        public void NotifyUIScreenTransition(string screenName = null, bool isBackNavigation = false)
        {
            if (string.IsNullOrEmpty(screenName)) return;

            string cleanScreenName = screenName.StartsWith("Back_") ? screenName.Substring(5) : screenName;

            // 1. Debounce rapid duplicate calls for the same screen (window comes from Remote Config)
            float duplicateDebounce = ReadInt(RCInt.UITransitionDebounceMs, 800) / 1000f;
            if (_currentActiveScreen == cleanScreenName && (Time.unscaledTime - _lastTransitionTime < duplicateDebounce))
            {
                return;
            }

            // 2. Explicit Back Navigation Check
            if (isBackNavigation || screenName.StartsWith("Back_"))
            {
                _lastTransitionTime = Time.unscaledTime;
                _currentActiveScreen = cleanScreenName;

                // Trim history back to this screen if it was previously visited
                int existingIdx = _globalScreenHistory.LastIndexOf(cleanScreenName);
                if (existingIdx >= 0)
                {
                    _globalScreenHistory.RemoveRange(existingIdx + 1, _globalScreenHistory.Count - (existingIdx + 1));
                }

                if (!ReadBool(RCBool.BackNavigationAds, false))
                {
                    Debug.Log($"<color=#90A4AE><b>[AdStrategy]</b> ⏪ <b>BACK NAVIGATION</b> to: <b>{cleanScreenName}</b> (History Depth: {_globalScreenHistory.Count}) ➔ <b>SKIPPED (RC BackNavigationAds = false)</b></color>");
                    return;
                }

                Debug.Log($"<color=#90A4AE><b>[AdStrategy]</b> ⏪ <b>BACK NAVIGATION</b> to: <b>{cleanScreenName}</b> (RC BackNavigationAds = true) ➔ evaluating strategy</color>");
                EvaluateStrategyAdForScreen(cleanScreenName);
                return;
            }

            // 3. Auto-Detect if returning to an existing screen in history (e.g. Back button pressed in UI)
            int historyIndex = _globalScreenHistory.IndexOf(cleanScreenName);
            if (historyIndex >= 0 && historyIndex < _globalScreenHistory.Count - 1)
            {
                _lastTransitionTime = Time.unscaledTime;
                _currentActiveScreen = cleanScreenName;
                _globalScreenHistory.RemoveRange(historyIndex + 1, _globalScreenHistory.Count - (historyIndex + 1));

                if (!ReadBool(RCBool.ReturnToPreviousScreenAds, false))
                {
                    Debug.Log($"<color=#90A4AE><b>[AdStrategy]</b> ⏪ <b>RETURNING TO PREVIOUS SCREEN</b>: <b>{cleanScreenName}</b> (Auto-detected as Back Nav, History Depth: {_globalScreenHistory.Count}) ➔ <b>SKIPPED (RC ReturnToPreviousScreenAds = false)</b></color>");
                    return;
                }

                Debug.Log($"<color=#90A4AE><b>[AdStrategy]</b> ⏪ <b>RETURNING TO PREVIOUS SCREEN</b>: <b>{cleanScreenName}</b> (RC ReturnToPreviousScreenAds = true) ➔ evaluating strategy</color>");
                EvaluateStrategyAdForScreen(cleanScreenName);
                return;
            }

            // 4. If it's the exact same screen currently at the top of the history, don't re-trigger
            if (_globalScreenHistory.Count > 0 && _globalScreenHistory[_globalScreenHistory.Count - 1] == cleanScreenName)
            {
                return;
            }

            // 5. Genuine Forward Navigation
            // The first screen of a session is recorded as the root of the stack, but it is no longer
            // exempt from ads: it is treated as a normal forward transition, so AdStrategyMode /
            // EventAdFrequency / AdDeliveryType decide whether it serves one.
            bool isRootScreen = _globalScreenHistory.Count == 0;

            _lastTransitionTime = Time.unscaledTime;
            _currentActiveScreen = cleanScreenName;
            _globalScreenHistory.Add(cleanScreenName);

            if (isRootScreen)
            {
                Debug.Log($"<color=#00E5FF><b>[AdStrategy]</b> 🏠 <b>ROOT SCREEN</b>: <b>{cleanScreenName}</b> (root of the screen stack, evaluated by the RC strategy)</color>");
            }

            EvaluateStrategyAdForScreen(cleanScreenName);
        }

        /// <summary>
        /// Runs the Remote Config strategy gates for a screen that has already been accepted as a
        /// navigation. Every decision below is read from Remote Config - there is no compiled-in
        /// mode, frequency or scene rule.
        /// </summary>
        private void EvaluateStrategyAdForScreen(string cleanScreenName)
        {
            if (RemoteConfigManager.Instance == null)
            {
                Debug.LogWarning("<b>[AdStrategy]</b> ⚠️ RemoteConfigManager Instance is null. Skipping transition check.");
                return;
            }

            if (!RemoteConfigManager.Instance.GetBool(RCBool.EnableAds, true))
            {
                Debug.Log("<color=red><b>[AdStrategy]</b> ❌ EnableAds is FALSE in Remote Config. Ad transition ignored.</color>");
                return;
            }

            int strategyMode = GetStrategyMode();

            // Mode 0 (Time-Based Only): UI transitions do not trigger ads
            if (strategyMode == 0)
            {
                Debug.Log($"<color=#78909C><b>[AdStrategy]</b> ⏱️ Mode 0 (Time-Based Only) Active ➔ Screen transition to '{cleanScreenName}' ignored.</color>");
                return;
            }

            // Mode 2 (Hybrid): UI transitions only trigger ads while in UI scenes
            if (strategyMode == 2 && !IsUIScene())
            {
                string activeScene = SceneManager.GetActiveScene().name;
                Debug.Log($"<color=#78909C><b>[AdStrategy]</b> 🎮 Mode 2 (Hybrid) Active in Gameplay Scene '<i>{activeScene}</i>' ➔ UI transitions silenced (Timer ads active in gameplay).</color>");
                return;
            }

            int frequency = RemoteConfigManager.Instance.GetInt(RCInt.EventAdFrequency, 2);
            uiScreenTransitionCount++;

            // Frequency 1: Show ad on ALL screen transitions
            if (frequency <= 1)
            {
                Debug.Log($"<color=#00E5FF><b>[AdStrategy]</b> ⏩ Forward Transition <b>#{uiScreenTransitionCount}</b> (All-Screen Mode 1:1) to: <b>{cleanScreenName}</b> ➔ <color=#00FF66><b>TRIGGERING AD</b></color></color>");
                ShowStrategyAd($"UI_Transition_{cleanScreenName}");
                return;
            }

            // Alternating Screen Mode (e.g. Frequency = 2)
            // 1st transition: AD, 2nd transition: NO AD, 3rd transition: AD, 4th transition: NO AD...
            int stepInCycle = ((uiScreenTransitionCount - 1) % frequency) + 1;
            bool shouldShowAd = (stepInCycle == 1);

            if (shouldShowAd)
            {
                Debug.Log($"<color=#00FF66><b>[AdStrategy]</b> ⏩ Forward Transition <b>#{uiScreenTransitionCount}</b> to: <b>{cleanScreenName}</b> [Alternating: <b>AD TURN</b> (Step {stepInCycle}/{frequency})] ➔ <color=#00FF00><b>TRIGGERING AD</b></color></color>");
                ShowStrategyAd($"UI_Transition_{cleanScreenName}");
            }
            else
            {
                Debug.Log($"<color=#FFD54F><b>[AdStrategy]</b> ⏩ Forward Transition <b>#{uiScreenTransitionCount}</b> to: <b>{cleanScreenName}</b> [Alternating: <b>NO AD TURN</b> (Step {stepInCycle}/{frequency})] ➔ <i>Ad-Free Screen (Skipped)</i></color>");
            }
        }

        /// <summary>
        /// Centralized ad delivery for system/timer/event ads.
        /// Respects AdDeliveryType (0: Only Interstitial, 1: Only Rewarded, 2: Alternating).
        /// <paramref name="bypassStrategyModeGate"/> is passed by explicit button placements (on-click ads),
        /// which must never be silenced by the AdStrategyMode 0 "Timers only" gate. Every other gate
        /// (EnableAds, Remove Ads, AdDeliveryType, RewardedFallbackToInterstitial) still applies.
        /// </summary>
        public void ShowStrategyAd(string context = "System", bool bypassStrategyModeGate = false)
        {
            // if (AnalyticsManager.Instance != null)
            // {
            //     AnalyticsManager.Instance.LogStrategyAdTriggered(AnalyticsManager.Instance.GetLevelString(null), context);
            // }

#if MangoMango_IAP
            if (isRemoveAdsPurchased)
            {
                Debug.Log($"<color=yellow><b>[AdStrategy]</b> 🚫 Remove Ads purchased by player. {context} ad aborted.</color>");
                return;
            }
#endif
            if (RemoteConfigManager.Instance != null && !RemoteConfigManager.Instance.GetBool(RCBool.EnableAds, true))
            {
                Debug.Log($"<color=red><b>[AdStrategy]</b> ❌ EnableAds is FALSE in Remote Config. {context} ad aborted.</color>");
                return;
            }

            int strategyMode = GetStrategyMode();
            bool isTimeBasedTimerAd = !string.IsNullOrEmpty(context) && context.Equals("TimeBasedTimer", System.StringComparison.OrdinalIgnoreCase);

            // In Time-Based Only mode (0), only TimeBasedTimer is allowed; other checkpoint/event ads are suppressed.
            // Explicit button placements (bypassStrategyModeGate) are never suppressed by this mode.
            if (!bypassStrategyModeGate && strategyMode == 0 && !isTimeBasedTimerAd)
            {
                Debug.Log($"<color=#78909C><b>[AdStrategy]</b> ⏱️ Mode 0 (Time-Based Only) Active ➔ Checkpoint / Event ad '{context}' suppressed.</color>");
                return;
            }

            // The delivered ad kind is chosen straight from the Remote Config integer, using 1-based
            // codes so the dashboard value reads like a menu:
            //   1 = Interstitial only
            //   2 = Rewarded only (+ SystemRewardedCoins)
            //   3 = Alternating (1 Interstitial, 1 Rewarded, 1 Interstitial ...)
            // 0 is kept as the legacy "interstitial" code, and any unrecognised value also falls back
            // to Interstitial - it is the one kind that always has a safe full-screen path.
            int deliveryType = RemoteConfigManager.Instance != null
                ? RemoteConfigManager.Instance.GetInt(RCInt.AdDeliveryType, 3)
                : 3;
            int coinsReward = RemoteConfigManager.Instance != null ? RemoteConfigManager.Instance.GetInt(RCInt.SystemRewardedCoins, 500) : 500;

            bool shouldShowRewarded = false;

            // Counted on every served strategy ad (not only in the Alternating branch) so the logged
            // cycle number always matches the number of ads actually delivered.
            strategyAdCycleCount++;

            switch (deliveryType)
            {
                case 2: // Rewarded only
                    shouldShowRewarded = true;
                    Debug.Log($"<color=#CE93D8><b>[AdStrategy]</b> RC AdDeliveryType = <b>2</b> ➔ Delivery Mode: <b>Only Rewarded</b> | Cycle #{strategyAdCycleCount} | Context: <i>{context}</i></color>");
                    break;

                case 3: // Alternating
                    shouldShowRewarded = (strategyAdCycleCount % 2 == 0);
                    Debug.Log($"<color=#CE93D8><b>[AdStrategy]</b> RC AdDeliveryType = <b>3</b> ➔ Delivery Mode: <b>Alternating</b> | Cycle #{strategyAdCycleCount} | Next Up: <b>{(shouldShowRewarded ? "REWARDED" : "INTERSTITIAL")}</b> | Context: <i>{context}</i></color>");
                    break;

                default: // 1 = Interstitial only (0 and unknown values land here)
                    shouldShowRewarded = false;
                    Debug.Log($"<color=#CE93D8><b>[AdStrategy]</b> RC AdDeliveryType = <b>{deliveryType}</b> ➔ Delivery Mode: <b>Only Interstitial</b> | Context: <i>{context}</i></color>");
                    break;
            }

            if (shouldShowRewarded)
            {
                if (IsRewardedAdAvailable())
                {
                    Debug.Log($"<color=#00E676><b>[AdStrategy]</b> 🎁 Serving <b>REWARDED AD</b> ({context}) [Cycle #{strategyAdCycleCount}] ➔ Reward: <b>+{coinsReward} Coins</b></color>");
                    ShowRewardedAd(() =>
                    {
                        GrantRewardCoins(coinsReward);
                        Debug.Log($"<color=#FFD700><b>[AdStrategy]</b> 💰 System Reward Granted: <b>+{coinsReward} Coins</b> successfully added to player balance!</color>");
                    });
                }
                else
                {
                    if (ReadBool(RCBool.RewardedFallbackToInterstitial, true))
                    {
                        Debug.LogWarning($"<b>[AdStrategy]</b> ⚠️ Rewarded Ad due but not ready yet. Falling back to Interstitial Ad ({context}) [RC RewardedFallbackToInterstitial = true].");
                        ShowInterstitial(RCBool.TimeBasedAdBool);
                    }
                    else
                    {
                        Debug.LogWarning($"<b>[AdStrategy]</b> ⚠️ Rewarded Ad due but not ready yet. No fallback [RC RewardedFallbackToInterstitial = false] ➔ ad skipped ({context}).");
                    }
                }
            }
            else
            {
                Debug.Log($"<color=#FFA726><b>[AdStrategy]</b> 📺 Serving <b>INTERSTITIAL AD</b> ({context}) [Cycle #{strategyAdCycleCount}]</color>");
                ShowInterstitial(RCBool.TimeBasedAdBool);
            }
        }
        #endregion

        #region Other Methods
        private void SendRevenueStatus(MaxSdkBase.AdInfo adInfo, double revenue)
        {
            // Logging logic here
        }

        public void FetchRemoteValues()
        {
            remoteConfigFetched = true;

            // Remote Config can be unreachable (no network / service not initialised). Every value is
            // read defensively so the ad unit IDs always land on a placeholder instead of null -
            // a null ID makes MaxSdk.CreateBanner / CreateMRec throw an ArgumentNullException.
            // MAX has no smart/adaptive banner: the two banners below are separate standard banners,
            // so they must never share a placeholder ID (MAX keys banners by ad unit ID).
            string ReadString(RCString key, string placeholder = "test")
            {
                try
                {
                    return RemoteConfigManager.Instance != null
                        ? RemoteConfigManager.Instance.GetString(key, placeholder)
                        : placeholder;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ApplovinAdManager] Remote Config read failed for {key}: {e.Message}");
                    return placeholder;
                }
            }

            InterstitialAdUnitId = ReadString(RCString.InterstitialAdID, "test_interstitial");
            // Controllable banner and right banner are two independent MAX banners.
            ControllableBannerAdUnitId = ReadString(RCString.ControllableBannerAdUnitId, "test_banner_controllable");
            TopRightFixedBanner = ReadString(RCString.TopRightFixedBannerAdUnitId, "test_banner_right");
            RewardedAdUnitId = ReadString(RCString.RewardedAdID, "test_rewarded");
            MRecAdUnitId = ReadString(RCString.MRecAdID, "test_mrec");
            AppOpenAdUnitId = ReadString(RCString.AppOpenAdUnitId, "test_appopen");

            try
            {
                AdDivisionValue = RemoteConfigManager.Instance != null
                    ? RemoteConfigManager.Instance.GetInt(RCInt.AdDivisionValue)
                    : 0;
            }
            catch (Exception)
            {
                AdDivisionValue = 0;
            }

            // Everything above is now in force. If MAX has already been initialised (a late or
            // second Remote Config response), push the new values onto the live ad objects.
            ApplyRemoteConfigToLiveAds();
        }

        /// <summary>
        /// Remote Config is the single source of truth, so a response that arrives after MAX has
        /// initialised must still take effect. Only what actually changed is re-applied; the
        /// on/off switches and strategy numbers are already read live at every call site.
        /// </summary>
        private void ApplyRemoteConfigToLiveAds()
        {
            if (!isMaxSDKInitialized) return;

            RegisterAppOpenCallbacksIfEnabled();

            if (appliedInterstitialId != InterstitialAdUnitId)
            {
                appliedInterstitialId = InterstitialAdUnitId;
                Debug.Log($"<color=#00FF88><b>[RemoteConfig]</b> Interstitial ad unit changed -> {InterstitialAdUnitId}</color>");
                LoadInterstitial();
            }

            if (appliedRewardedId != RewardedAdUnitId)
            {
                appliedRewardedId = RewardedAdUnitId;
                Debug.Log($"<color=#00FF88><b>[RemoteConfig]</b> Rewarded ad unit changed -> {RewardedAdUnitId}</color>");
                LoadRewardedAd();
            }

            // MAX keys banners and MRECs by ad unit ID, so a new ID is a new ad object.
            if (appliedControllableBannerId != ControllableBannerAdUnitId)
            {
                if (!IsPlaceholderAdUnitId(appliedControllableBannerId)) MaxSdk.DestroyBanner(appliedControllableBannerId);
                Debug.Log($"<color=#00FF88><b>[RemoteConfig]</b> Controllable banner ad unit changed -> {ControllableBannerAdUnitId}</color>");
                InitializeControllableBannerAds();
            }
            else
            {
                int controllablePosition = ReadInt(RCInt.ControllableBannerPosition, appliedControllableBannerPosition);
                if (appliedControllableBannerPosition != controllablePosition)
                {
                    appliedControllableBannerPosition = controllablePosition;
                    Debug.Log($"<color=#00FF88><b>[RemoteConfig]</b> Controllable banner position changed -> {controllablePosition}</color>");
                    MaxSdk.UpdateBannerPosition(ControllableBannerAdUnitId, ToBannerPosition(controllablePosition));
                }
            }

            if (appliedFixedBannerId != TopRightFixedBanner)
            {
                if (!IsPlaceholderAdUnitId(appliedFixedBannerId)) MaxSdk.DestroyBanner(appliedFixedBannerId);
                Debug.Log($"<color=#00FF88><b>[RemoteConfig]</b> Top-right fixed banner ad unit changed -> {TopRightFixedBanner}</color>");
                InitializeFixedBannerAds();
            }

            if (appliedMRecId != MRecAdUnitId)
            {
                if (!IsPlaceholderAdUnitId(appliedMRecId)) MaxSdk.DestroyMRec(appliedMRecId);
                Debug.Log($"<color=#00FF88><b>[RemoteConfig]</b> MRec ad unit changed -> {MRecAdUnitId}</color>");
                InitializeMRecAds();
            }
            else
            {
                int mrecPosition = ReadInt(RCInt.MRECBannerPosition, appliedMRecPosition);
                if (appliedMRecPosition != mrecPosition)
                {
                    appliedMRecPosition = mrecPosition;
                    Debug.Log($"<color=#00FF88><b>[RemoteConfig]</b> MRec position changed -> {mrecPosition}</color>");
                    MaxSdk.UpdateMRecPosition(MRecAdUnitId, ToAdViewPosition(mrecPosition));
                }
            }
        }

        // A placeholder ID is empty, or one of the local fallbacks MAX was given while Remote Config
        // had no real ad unit ID. Real MAX ad unit IDs never start with "test", so this keeps every
        // "is this a real ad unit" decision in one place.
        private static bool IsPlaceholderAdUnitId(string adUnitId)
        {
            return string.IsNullOrEmpty(adUnitId) || adUnitId.StartsWith("test", StringComparison.OrdinalIgnoreCase);
        }

        private void OnGUI()
        {
            if (RemoteConfigManager.Instance == null) return;
            if (!RemoteConfigManager.Instance.GetBool(RCBool.TestAds)) return;
            if (GUI.Button(new Rect(10, 10, 150, 100), "TEST APPLOVIN"))
            {
                MaxSdk.ShowMediationDebugger();
            }
        }

        /// <summary>
        /// Universal reward handler: notifies listeners, safely invokes PlayerSaveManager.AddCoins if present,
        /// or saves to PlayerPrefs("Coins"). Any game can subscribe to OnRewardedAdCoinsGranted.
        /// </summary>
        public static void GrantRewardCoins(int amount)
        {
            try
            {
                OnRewardedAdCoinsGranted?.Invoke(amount);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ApplovinAdManager] Exception in OnRewardedAdCoinsGranted listener: {ex.Message}");
            }

            // Safely invoke PlayerSaveManager.AddCoins if present in the project without compile-time hard dependency
            try
            {
                Type saveManagerType = Type.GetType("PlayerSaveManager");
                if (saveManagerType != null)
                {
                    var addCoinsMethod = saveManagerType.GetMethod("AddCoins", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (addCoinsMethod != null)
                    {
                        addCoinsMethod.Invoke(null, new object[] { amount });
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ApplovinAdManager] Could not invoke PlayerSaveManager.AddCoins: {ex.Message}");
            }

            // Fallback for new projects without a custom save manager
            int currentCoins = PlayerPrefs.GetInt("Coins", 0);
            PlayerPrefs.SetInt("Coins", currentCoins + amount);
            PlayerPrefs.Save();
        }

        private static void TrySetPlayerSaveManagerAdsRemoved(bool removed)
        {
            try
            {
                Type saveManagerType = Type.GetType("PlayerSaveManager");
                if (saveManagerType != null)
                {
                    var prop = saveManagerType.GetProperty("IsAdsRemoved", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (prop != null && prop.CanWrite)
                    {
                        prop.SetValue(null, removed);
                    }
                }
            }
            catch { }
        }
        #endregion
    }
}