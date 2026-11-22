using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;

#if MangoMango_IAP
using MangoMango.IAP;
#endif

namespace MangoMango.Advertisement
{
    public class TimeBasedAd : MonoBehaviour
    {
        public static TimeBasedAd Instance { get; private set; }

        #region Variables
        private bool isAdEnabled { get => RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.GetBool(RCBool.EnableAds, false) && RemoteConfigManager.Instance.GetBool(RCBool.TimeBasedAdBool, false); }
        private float adInterval { get => RemoteConfigManager.Instance != null ? RemoteConfigManager.Instance.GetInt(RCInt.TimeBasedAdInt, 60) : 60f; }
        
        private Coroutine adCoroutine;
        private float currentTimer = 0f;

        private bool shouldShowIap { get => RemoteConfigManager.Instance == null || RemoteConfigManager.Instance.GetBool(RCBool.ShowIAPAfterAd, true); }
        private int showRemoveAdsAfter { get => RemoteConfigManager.Instance != null ? RemoteConfigManager.Instance.GetInt(RCInt.ShowIAPAfter, 1) : 1; }
        private int adsShownCount = 0;
        private int currentPanelIndex = 0;

        [Header("IAP Promotion Panels (Remove Ads & Best Bundle)")]
        [Tooltip("Assign panels/canvases here in sequence (e.g., Remove Ads panel, Best Bundle panel). They will alternate each time an IAP popup is shown after interstitial ads.")]
        public GameObject[] iapPanels;

        [Tooltip("First panel reference (Remove Ads). Used if iapPanels array is not populated.")]
        public GameObject removeAdsPanel;

        [Tooltip("Second panel reference (Best Bundle). Used if iapPanels array is not populated.")]
        public GameObject bundlePanel;
        #endregion

        #region Countdown UI Settings & References
        [Header("Time-Based Ad Countdown UI (5s Warning)")]
        [Tooltip("Enable or disable the bottom-right warning countdown before time-based ads (Inspector default). Remote Config 'ShowAdTimer' overrides this.")]
        [SerializeField] private bool enableCountdownUI = true;

        private bool isCountdownEnabled => enableCountdownUI && (RemoteConfigManager.Instance == null || RemoteConfigManager.Instance.GetBool(RCBool.ShowAdTimer, true));

        [Tooltip("Seconds before the ad when the warning UI appears. Fallback only: the live value is the Remote Config key 'TimeBasedAdCountdown'.")]
        [SerializeField] private float countdownDuration = 5.0f;

        /// <summary>Live countdown length, read from Remote Config.</summary>
        private float CountdownSeconds => RemoteConfigManager.Instance != null
            ? Mathf.Max(1f, RemoteConfigManager.Instance.GetInt(RCInt.TimeBasedAdCountdown, Mathf.RoundToInt(countdownDuration)))
            : countdownDuration;

        [Tooltip("Customizable text format. {0} is replaced by the remaining seconds (5, 4, 3, 2, 1).")]
        [SerializeField] private string countdownTextFormat = "Ad in {0}s...";

        [Header("Countdown UI References (Auto-created if unassigned)")]
        [Tooltip("Root GameObject or Canvas of the countdown warning.")]
        [SerializeField] private GameObject countdownCanvasRoot;

        [Tooltip("CanvasGroup for smooth fade-in/fade-out.")]
        [SerializeField] private CanvasGroup countdownCanvasGroup;

        [Tooltip("TextMeshProUGUI label displaying the countdown number/text.")]
        [SerializeField] private TMPro.TextMeshProUGUI countdownTMPText;

        [Tooltip("Optional standard legacy Text component fallback.")]
        [SerializeField] private UnityEngine.UI.Text countdownLegacyText;

        [Tooltip("Optional progress fill bar (Image with type = Filled).")]
        [SerializeField] private UnityEngine.UI.Image countdownProgressBar;

        [Tooltip("Optional background card image.")]
        [SerializeField] private UnityEngine.UI.Image countdownBgImage;

        [Tooltip("Optional AD badge background image.")]
        [SerializeField] private UnityEngine.UI.Image countdownBadgeImage;

        private Coroutine countdownFadeCoroutine;
        private bool isCountdownVisible = false;
        private int lastDisplayedSecond = -1;

        // Guard flags for the "countdown before any full-screen ad" flow driven by ApplovinAdManager.
        private bool isAdCountdownRunning = false;
        private bool skipNextAdCountdown = false;
        #endregion

        #region Helper Methods
        public bool IsRemoveAdsPurchased()
        {
            if (PlayerPrefs.GetInt("RemoveAdsPurchased", 0) == 1) return true;
            if (PlayerPrefs.GetInt("IsAdsRemoved", 0) == 1) return true;

#if MangoMango_IAP
            if (IAPManager.Instance != null)
            {
                bool purchased = IAPManager.Instance.IsPurchased(ProductName.RemoveAds) || 
                                 IAPManager.Instance.IsPurchased(ProductName.BestBundle);
                if (purchased)
                {
                    PlayerPrefs.SetInt("RemoveAdsPurchased", 1);
                    PlayerPrefs.Save();
                }
                return purchased;
            }
#endif
            return false;
        }

        private bool CanProcessAdTimer()
        {
            return Time.timeScale > 0f;
        }

        /// <summary>
        /// Checks if a time-based ad is genuinely eligible to be displayed in the current state/scene.
        /// Used so the countdown warning ONLY appears when a time-based ad will actually fire (not silenced).
        /// </summary>
        public bool CanShowTimeBasedAdInCurrentState()
        {
            if (!isAdEnabled || IsRemoveAdsPurchased() || !CanProcessAdTimer())
            {
                return false;
            }

            if (RemoteConfigManager.Instance == null)
            {
                return false;
            }

            if (!RemoteConfigManager.Instance.GetBool(RCBool.InterstitialCheckAll, true))
            {
                return false;
            }

            // Check that the ad kind the strategy will actually serve is loaded/ready, so the countdown
            // never appears for an ad that cannot be shown (e.g. under a Rewarded-only delivery type).
            if (ApplovinAdManager.Instance != null && !ApplovinAdManager.Instance.IsStrategyAdReady())
            {
                return false;
            }

            int strategyMode = ApplovinAdManager.Instance != null
                ? ApplovinAdManager.Instance.GetStrategyMode()
                : RemoteConfigManager.Instance.GetInt(RCInt.AdStrategyMode, 2);

            // In Event-Based Only mode (1), timer ad is silenced
            if (strategyMode == 1)
            {
                return false;
            }

            // In Hybrid mode (2), timer ad is silenced in UI/Menu scenes (only active during gameplay)
            if (strategyMode == 2)
            {
                bool isUI = true;
                if (ApplovinAdManager.Instance != null)
                {
                    isUI = ApplovinAdManager.Instance.IsUIScene();
                }
                else
                {
                    string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                    string lower = sceneName.ToLower();
                    isUI = lower.Contains("splash") || lower.Contains("track") || lower.Contains("menu") || 
                           lower.Contains("loading") || lower.Contains("select") || lower.Contains("garage") || 
                           !lower.Contains("farming");
                }

                if (isUI)
                {
                    return false;
                }
            }

            return true;
        }
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (Instance == null)
            {
                transform.SetParent(null);
                Instance = this;
                DontDestroyOnLoad(gameObject);
                EnsureCountdownUI();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private IEnumerator Start()
        {
            EnsureCountdownUI();
#if MangoMango_IAP
            while (IAPManager.Instance == null)
            {
                yield return null;
            }
            IAPManager.Instance.onPurchaseSuccess += OnPurchaseSuccess; 
#endif
            yield break;
        }

        private void OnDisable()
        {
            HideCountdownUI(true);
#if MangoMango_IAP
            if (IAPManager.Instance != null)
            {
                IAPManager.Instance.onPurchaseSuccess -= OnPurchaseSuccess; 
            }
#endif
        }

        /// <summary>
        /// Resets the ad timer whenever the app regains focus (e.g. after closing a full-screen ad).
        /// </summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                currentTimer = 0f; // Force timer to restart from 0 after returning from an ad!
                HideCountdownUI(true);
            }
        }
        #endregion

        #region Ad Management & Timer Coroutine
        public void StartAdTimer()
        {
            EnsureCountdownUI();

            if (!isAdEnabled)
            {
                if (adCoroutine != null)
                {
                    StopCoroutine(adCoroutine);
                    adCoroutine = null;
                }
                HideCountdownUI(true);
                return;
            }

            if (IsRemoveAdsPurchased())
            {
                StopAdTimer();
                return;
            }

            if (adCoroutine == null)
            {
                currentTimer = 0f;
                HideCountdownUI(true);
                adCoroutine = StartCoroutine(TimeBasedAdCoroutine());
                string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                bool warningActive = isCountdownEnabled;
                Debug.Log($"<color=#00E676><b>[TimeBasedAd] ⏱️ Time-Based Ad Timer Started!</b> Interval: <b>{adInterval}s</b> (Warning: {(warningActive ? $"-{CountdownSeconds}s" : "DISABLED")}) | Scene: <i>{activeScene}</i></color>");
            }
        }

        private IEnumerator TimeBasedAdCoroutine()
        {
            currentTimer = 0f;
            HideCountdownUI(true);

            while (isAdEnabled)
            {
                if (IsRemoveAdsPurchased())
                {
                    StopAdTimer();
                    yield break;
                }

                // Accumulate time ONLY when game is unpaused AND timer ads are genuinely active (not silenced) in current state
                if (CanProcessAdTimer() && CanShowTimeBasedAdInCurrentState())
                {
                    currentTimer += Time.deltaTime;

                    float countdown = CountdownSeconds;
                    float totalInterval = Mathf.Max(countdown + 1f, adInterval);
                    float warningStartTime = totalInterval - countdown;

                    // Handle 5-Second Advance Countdown Warning ONLY once confirmed that timer ad is NOT silenced
                    if (isCountdownEnabled)
                    {
                        if (currentTimer >= warningStartTime && currentTimer < totalInterval)
                        {
                            float remainingSeconds = totalInterval - currentTimer;
                            int secondsLeft = Mathf.Clamp(Mathf.FloorToInt(remainingSeconds) + 1, 1, (int)countdown);
                            float progress = Mathf.Clamp01(remainingSeconds / countdown);
                            ShowCountdownUI(secondsLeft, progress);
                        }
                        else if (isCountdownVisible)
                        {
                            HideCountdownUI(true);
                        }
                    }
                    else if (isCountdownVisible)
                    {
                        HideCountdownUI(true);
                    }

                    // Interval elapsed (Timer hits 0) -> immediately HIDE countdown canvas and trigger the time-based ad
                    if (currentTimer >= totalInterval)
                    {
                        HideCountdownUI(true);
                        currentTimer = 0f;
                        ShowTimeBasedAd();
                    }
                }
                else
                {
                    // While silenced (in UI/Menu scene, paused, ad not ready, etc.), keep timer at 0 and UI hidden
                    currentTimer = 0f;
                    if (isCountdownVisible)
                    {
                        HideCountdownUI(true);
                    }
                }

                yield return null;
            }
        }

        private void ShowTimeBasedAd()
        {
            HideCountdownUI(true);

            if (!CanShowTimeBasedAdInCurrentState())
            {
                currentTimer = 0f;
                return;
            }

            // Reset timer again right before attempting to open the ad
            currentTimer = 0f;

            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            Debug.Log($"<color=#FF9100><b>[TimeBasedAd] ⏰ Timer Elapsed ({adInterval}s)!</b> Triggering ad in scene '<i>{sceneName}</i>'</color>");

            if (ApplovinAdManager.Instance != null)
            {
                // The timer's own card already announced this ad, so the manager must not run a
                // second countdown for it inside ShowStrategyAd.
                skipNextAdCountdown = true;
                try
                {
                    ApplovinAdManager.Instance.ShowStrategyAd("TimeBasedTimer");
                }
                finally
                {
                    skipNextAdCountdown = false;
                }
            }
            else
            {
                ApplovinAdManager.Instance?.ShowInterstitial(RCBool.TimeBasedAdBool);
            }
        }

        public void StopAdTimer()
        {
            if (adCoroutine != null)
            {
                StopCoroutine(adCoroutine);
                adCoroutine = null;
            }

            currentTimer = 0f;
            HideCountdownUI(true);
        }
        #endregion

        #region Countdown UI Management (Bottom Right Canvas)
        /// <summary>
        /// Ensures the Countdown Canvas and its child visual hierarchy exist.
        /// If the user has assigned their own custom objects in the Inspector, those are used as-is.
        /// </summary>
        public void EnsureCountdownUI()
        {
            if (countdownCanvasRoot != null && countdownCanvasGroup != null)
            {
                return;
            }

            // Check if existing child canvas already exists under this transform
            Transform existingCanvas = transform.Find("Canvas_TimeBasedAd_Countdown");
            if (existingCanvas != null)
            {
                countdownCanvasRoot = existingCanvas.gameObject;
                countdownCanvasGroup = existingCanvas.GetComponentInChildren<CanvasGroup>(true);
                if (countdownTMPText == null) countdownTMPText = existingCanvas.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                if (countdownLegacyText == null) countdownLegacyText = existingCanvas.GetComponentInChildren<UnityEngine.UI.Text>(true);
                
                var card = existingCanvas.Find("Card_AdCountdown");
                if (card != null) card.gameObject.SetActive(false);

                var canvasComp = existingCanvas.GetComponent<Canvas>();
                if (canvasComp != null) canvasComp.enabled = false;

                countdownCanvasRoot.SetActive(false);
                if (countdownCanvasGroup != null) countdownCanvasGroup.alpha = 0f;
                return;
            }

            // Auto-generate high quality, responsive countdown canvas child
            GameObject canvasGO = new GameObject("Canvas_TimeBasedAd_Countdown");
            canvasGO.transform.SetParent(this.transform, false);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950; // Above HUD, non-blocking
            canvas.enabled = false;

            var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            countdownCanvasGroup = canvasGO.AddComponent<CanvasGroup>();
            countdownCanvasGroup.alpha = 0f;
            countdownCanvasGroup.interactable = false;
            countdownCanvasGroup.blocksRaycasts = false;

            // Card Container (Anchored to Bottom-Right)
            GameObject cardGO = new GameObject("Card_AdCountdown");
            cardGO.transform.SetParent(canvasGO.transform, false);

            RectTransform cardRect = cardGO.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(1f, 0f); // Bottom Right
            cardRect.anchorMax = new Vector2(1f, 0f);
            cardRect.pivot = new Vector2(1f, 0f);
            cardRect.anchoredPosition = new Vector2(-30f, 30f);
            cardRect.sizeDelta = new Vector2(210f, 48f);

            countdownBgImage = cardGO.AddComponent<UnityEngine.UI.Image>();
            countdownBgImage.color = new Color(0.08f, 0.09f, 0.14f, 0.88f); // Translucent Dark Glass

            // Gold Accent AD Badge
            GameObject badgeGO = new GameObject("Badge_AD");
            badgeGO.transform.SetParent(cardGO.transform, false);
            RectTransform badgeRect = badgeGO.AddComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 0.5f);
            badgeRect.anchorMax = new Vector2(0f, 0.5f);
            badgeRect.pivot = new Vector2(0f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(10f, 0f);
            badgeRect.sizeDelta = new Vector2(34f, 26f);

            countdownBadgeImage = badgeGO.AddComponent<UnityEngine.UI.Image>();
            countdownBadgeImage.color = new Color(1f, 0.76f, 0.03f, 0.95f); // Amber Gold

            GameObject badgeTextGO = new GameObject("BadgeText");
            badgeTextGO.transform.SetParent(badgeGO.transform, false);
            RectTransform badgeTextRect = badgeTextGO.AddComponent<RectTransform>();
            badgeTextRect.anchorMin = Vector2.zero;
            badgeTextRect.anchorMax = Vector2.one;
            badgeTextRect.sizeDelta = Vector2.zero;

            var badgeTMP = badgeTextGO.AddComponent<TMPro.TextMeshProUGUI>();
            badgeTMP.text = "AD";
            badgeTMP.fontSize = 13f;
            badgeTMP.fontStyle = TMPro.FontStyles.Bold;
            badgeTMP.alignment = TMPro.TextAlignmentOptions.Center;
            badgeTMP.color = new Color(0.12f, 0.12f, 0.12f, 1f);

            // Countdown Label
            GameObject textGO = new GameObject("Text_Countdown");
            textGO.transform.SetParent(cardGO.transform, false);
            RectTransform textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(50f, 4f);
            textRect.offsetMax = new Vector2(-10f, -4f);

            countdownTMPText = textGO.AddComponent<TMPro.TextMeshProUGUI>();
            countdownTMPText.text = string.Format(countdownTextFormat, 5);
            countdownTMPText.fontSize = 16f;
            countdownTMPText.fontStyle = TMPro.FontStyles.Bold;
            countdownTMPText.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            countdownTMPText.color = Color.white;

            // Thin Bottom Progress Bar Track & Fill
            GameObject trackGO = new GameObject("Progress_Track");
            trackGO.transform.SetParent(cardGO.transform, false);
            RectTransform trackRect = trackGO.AddComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0f, 0f);
            trackRect.anchorMax = new Vector2(1f, 0f);
            trackRect.pivot = new Vector2(0.5f, 0f);
            trackRect.anchoredPosition = new Vector2(0f, 0f);
            trackRect.sizeDelta = new Vector2(0f, 3f);

            var trackImg = trackGO.AddComponent<UnityEngine.UI.Image>();
            trackImg.color = new Color(0.25f, 0.25f, 0.3f, 0.4f);

            GameObject fillGO = new GameObject("Progress_Fill");
            fillGO.transform.SetParent(trackGO.transform, false);
            RectTransform fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            countdownProgressBar = fillGO.AddComponent<UnityEngine.UI.Image>();
            countdownProgressBar.color = new Color(1f, 0.76f, 0.03f, 1f);
            countdownProgressBar.type = UnityEngine.UI.Image.Type.Filled;
            countdownProgressBar.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            countdownProgressBar.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            countdownProgressBar.fillAmount = 1f;

            countdownCanvasRoot = canvasGO;
            cardGO.SetActive(false);
            canvasGO.SetActive(false); // Created initially hidden
        }

        /// <summary>
        /// Updates the countdown UI with remaining seconds and progress bar fill.
        /// Only shown when a time-based (timer) ad is genuinely about to fire.
        /// </summary>
        public void ShowCountdownUI(int secondsLeft, float progress)
        {
            if (!CanShowTimeBasedAdInCurrentState())
            {
                HideCountdownUI(true);
                return;
            }

            RenderCountdownUI(secondsLeft, progress);
        }

        /// <summary>
        /// Draws the countdown card with no state eligibility check, so the same warning UI can also
        /// be shown before event-based interstitials and rewarded ads.
        /// </summary>
        private void RenderCountdownUI(int secondsLeft, float progress)
        {
            if (secondsLeft <= 0)
            {
                HideCountdownUI(true);
                return;
            }

            EnsureCountdownUI();

            if (countdownCanvasRoot == null || countdownCanvasGroup == null)
            {
                return;
            }

            if (!countdownCanvasRoot.activeSelf)
            {
                var canvasComp = countdownCanvasRoot.GetComponent<Canvas>();
                if (canvasComp != null) canvasComp.enabled = true;

                var cardTransform = countdownCanvasRoot.transform.Find("Card_AdCountdown");
                if (cardTransform != null) cardTransform.gameObject.SetActive(true);

                countdownCanvasRoot.SetActive(true);
            }

            // Update Text
            if (secondsLeft != lastDisplayedSecond)
            {
                lastDisplayedSecond = secondsLeft;
                string formatted = string.Format(countdownTextFormat, secondsLeft);
                if (countdownTMPText != null)
                {
                    countdownTMPText.text = formatted;
                }
                if (countdownLegacyText != null)
                {
                    countdownLegacyText.text = formatted;
                }
            }

            // Update Progress Bar
            if (countdownProgressBar != null)
            {
                countdownProgressBar.fillAmount = Mathf.Clamp01(progress);
            }

            // Fade In
            if (!isCountdownVisible)
            {
                isCountdownVisible = true;
                if (countdownFadeCoroutine != null) StopCoroutine(countdownFadeCoroutine);
                countdownFadeCoroutine = StartCoroutine(FadeCanvasGroupRoutine(countdownCanvasGroup, countdownCanvasGroup.alpha, 1f, 0.2f));
            }
        }

        /// <summary>
        /// Hides the countdown warning UI immediately.
        /// </summary>
        public void HideCountdownUI(bool immediate = true)
        {
            isCountdownVisible = false;
            lastDisplayedSecond = -1;

            if (countdownFadeCoroutine != null)
            {
                StopCoroutine(countdownFadeCoroutine);
                countdownFadeCoroutine = null;
            }

            if (countdownCanvasGroup != null)
            {
                countdownCanvasGroup.alpha = 0f;
            }

            if (countdownCanvasRoot != null)
            {
                var cardTransform = countdownCanvasRoot.transform.Find("Card_AdCountdown");
                if (cardTransform != null) cardTransform.gameObject.SetActive(false);

                var canvasComp = countdownCanvasRoot.GetComponent<Canvas>();
                if (canvasComp != null) canvasComp.enabled = false;

                countdownCanvasRoot.SetActive(false);
            }
        }

        /// <summary>
        /// Shows the countdown warning card before ANY full-screen ad (event interstitial, timer ad or
        /// rewarded ad). Returns true when the countdown took over: onComplete fires once the card has
        /// finished, and the caller must NOT display the ad itself. Returns false when the caller should
        /// display the ad immediately.
        /// </summary>
        public bool TryStartAdCountdown(Action onComplete)
        {
            // The timer's own card already announced this ad.
            if (skipNextAdCountdown)
            {
                skipNextAdCountdown = false;
                return false;
            }

            if (onComplete == null || isAdCountdownRunning || !isCountdownEnabled || !CanProcessAdTimer())
            {
                return false;
            }

            EnsureCountdownUI();
            if (countdownCanvasRoot == null || countdownCanvasGroup == null)
            {
                return false;
            }

            isAdCountdownRunning = true;
            StartCoroutine(AdCountdownRoutine(onComplete));
            return true;
        }

        /// <summary>
        /// Unscaled countdown that runs on real time, so it also works while an ad transition has
        /// timeScale at 0, then fires the actual ad display callback.
        /// </summary>
        private IEnumerator AdCountdownRoutine(Action onComplete)
        {
            float countdown = CountdownSeconds;
            float remaining = countdown;

            while (remaining > 0f)
            {
                int secondsLeft = Mathf.Clamp(Mathf.CeilToInt(remaining), 1, Mathf.Max(1, Mathf.RoundToInt(countdown)));
                RenderCountdownUI(secondsLeft, Mathf.Clamp01(remaining / countdown));
                yield return null;
                remaining -= Time.unscaledDeltaTime;
            }

            HideCountdownUI(true);
            isAdCountdownRunning = false;
            onComplete?.Invoke();
        }

        private IEnumerator FadeCanvasGroupRoutine(CanvasGroup cg, float startAlpha, float targetAlpha, float duration, Action onComplete = null)
        {
            if (cg == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            cg.alpha = targetAlpha;
            onComplete?.Invoke();
        }
        #endregion

        #region IAP Panels Rotation & Handling (Remove Ads & Best Bundle)
        public List<GameObject> GetConfiguredPanels()
        {
            var list = new List<GameObject>();
            if (iapPanels != null && iapPanels.Length > 0)
            {
                foreach (var p in iapPanels)
                {
                    if (p != null && !list.Contains(p))
                    {
                        list.Add(p);
                    }
                }
            }

            // Fallback to individual fields if array is empty
            if (list.Count == 0)
            {
                if (removeAdsPanel != null) list.Add(removeAdsPanel);
                if (bundlePanel != null) list.Add(bundlePanel);
            }
            return list;
        }

        public bool IsAnyIAPPanelActive()
        {
            var panels = GetConfiguredPanels();
            foreach (var p in panels)
            {
                if (p != null && p.activeInHierarchy)
                {
                    var cg = p.GetComponentInChildren<CanvasGroup>();
                    if (cg == null || cg.alpha > 0.1f)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Centralized hook called whenever ANY interstitial ad finishes displaying and dismisses.
        /// Alternates between Remove Ads popup and Best Bundle popup.
        /// </summary>
        public void CheckAndShowIAPPanel()
        {
            if (IsRemoveAdsPurchased())
            {
                Debug.Log("<color=yellow>[IAPPromotion] 🚫 Remove Ads already purchased. Promotion popup skipped.</color>");
                return;
            }

            if (!shouldShowIap)
            {
                Debug.Log("<color=#78909C>[IAPPromotion] ShowIAPAfterAd is FALSE in Remote Config. Promotion popup skipped.</color>");
                return;
            }

            if (IsAnyIAPPanelActive())
            {
                Debug.Log("<color=#78909C>[IAPPromotion] An IAP panel is already currently open on screen.</color>");
                return;
            }

            adsShownCount++;
            Debug.Log($"<color=#BA68C8><b>[IAPPromotion]</b> Interstitial Ad Dismissed! Count: <b>{adsShownCount}/{showRemoveAdsAfter}</b></color>");

            if (adsShownCount >= showRemoveAdsAfter)
            {
                StartCoroutine(DelayedShowIAPPanelRoutine());
                adsShownCount = 0;
            }
        }

        private IEnumerator DelayedShowIAPPanelRoutine()
        {
            // Wait a brief frame for app focus and timescale to settle cleanly
            yield return new WaitForSecondsRealtime(0.15f);

            if (IsRemoveAdsPurchased() || !shouldShowIap || IsAnyIAPPanelActive())
            {
                yield break;
            }

            ShowNextIAPPanel();
        }

        public void ShowNextIAPPanel()
        {
            var panels = GetConfiguredPanels();
            if (panels.Count == 0)
            {
                Debug.LogWarning("[IAPPromotion] No IAP panels (Remove Ads / Bundle) assigned on TimeBasedAd.");
                return;
            }

            // Close other panels cleanly
            for (int i = 0; i < panels.Count; i++)
            {
                if (panels[i] != null)
                {
                    panels[i].SetActive(false);
                }
            }

            int index = currentPanelIndex % panels.Count;
            GameObject panelToShow = panels[index];

            if (panelToShow != null)
            {
                panelToShow.SetActive(true);

                // 1. Ensure parent/child Canvas components are enabled
                var parentCanvas = panelToShow.GetComponentInParent<Canvas>(true);
                if (parentCanvas != null)
                {
                    parentCanvas.gameObject.SetActive(true);
                    parentCanvas.enabled = true;
                }

                var innerCanvases = panelToShow.GetComponentsInChildren<Canvas>(true);
                foreach (var c in innerCanvases)
                {
                    c.gameObject.SetActive(true);
                    c.enabled = true;
                }

                // 2. Ensure all CanvasGroups (in panel and children) are fully visible and interactive
                var canvasGroups = panelToShow.GetComponentsInChildren<CanvasGroup>(true);
                foreach (var cg in canvasGroups)
                {
                    cg.gameObject.SetActive(true);
                    cg.alpha = 1f;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                }

                // 3. Ensure all child transforms/popups are active in hierarchy
                EnsureAllChildrenActive(panelToShow.transform);

                // 4. Hook close/dismiss buttons
                HookCloseButtons(panelToShow);

                Debug.Log($"<color=#E91E63><b>[IAPPromotion] 🎁 Displaying Alternative IAP Popup ({index + 1}/{panels.Count}):</b> <i>{panelToShow.name}</i></color>");
            }

            // Cycle to the next panel for subsequent interstitial ads (1 -> 2 -> 1 -> 2 ...)
            currentPanelIndex = (currentPanelIndex + 1) % panels.Count;
        }

        private void EnsureAllChildrenActive(Transform parent)
        {
            if (parent == null) return;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child != null)
                {
                    child.gameObject.SetActive(true);
                    EnsureAllChildrenActive(child);
                }
            }
        }

        private void HookCloseButtons(GameObject panel)
        {
            if (panel == null) return;
            var buttons = panel.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                string btnName = btn.gameObject.name.ToLower();
                if (btnName.Contains("close") || btnName.Contains("cross") || btnName.Contains("exit") || 
                    btnName.Contains("cancel") || btnName.Contains("nothanks") || btnName.Contains("dismiss") || btnName.Contains("back"))
                {
                    btn.onClick.RemoveListener(HideAllIAPPanels);
                    btn.onClick.AddListener(HideAllIAPPanels);
                }
            }
        }

        public void HideAllIAPPanels()
        {
            var panels = GetConfiguredPanels();
            foreach (var p in panels)
            {
                if (p != null)
                {
                    var canvasGroups = p.GetComponentsInChildren<CanvasGroup>(true);
                    foreach (var cg in canvasGroups)
                    {
                        cg.alpha = 0f;
                        cg.interactable = false;
                        cg.blocksRaycasts = false;
                    }

                    p.SetActive(false);
                }
            }
        }

#if MangoMango_IAP
        private void OnPurchaseSuccess(ProductName name)
        {
            if (name == ProductName.BestBundle || name == ProductName.RemoveAds)
            {
                PlayerPrefs.SetInt("RemoveAdsPurchased", 1);
                PlayerPrefs.Save();
                StopAdTimer();
                HideAllIAPPanels();
            }
        }
#endif
        #endregion
    }
}