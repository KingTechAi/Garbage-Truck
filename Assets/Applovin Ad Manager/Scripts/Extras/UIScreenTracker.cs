using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MangoMango.Advertisement;

namespace MangoMango.Advertisement
{
    /// <summary>
    /// Universal, zero-code UI Screen Transition Tracker.
    /// Drag and drop your UI Panel GameObjects into the list in the Inspector.
    /// Whenever a panel becomes active, it automatically notifies ApplovinAdManager.
    /// </summary>
    public class UIScreenTracker : MonoBehaviour
    {
        public static UIScreenTracker Instance { get; private set; }

        [Header("Tracked Screen Panels")]
        [Tooltip("Assign all your UI screen/panel GameObjects here (e.g. ModeSelect, LevelSelect, Store, InApps).")]
        public List<GameObject> trackedScreens = new List<GameObject>();

        [Header("Settings")]
        [Tooltip("Fallback value only. The live value is the Remote Config key 'TrackerDebounceMs' (milliseconds).")]
        [SerializeField] private float transitionDebounce = 0.5f;

        [Tooltip("If true, automatically finds and tracks all child panels of this GameObject on Awake if list is empty.")]
        [SerializeField] private bool autoTrackChildren = false;

        [Header("Ad On Click Buttons")]
        [Tooltip("Drag any Button here and pressing it plays an ad. The delivered kind follows the Remote Config key 'AdDeliveryType' (1 = Interstitial only, 2 = Rewarded only, 3 = Alternating).")]
        public List<Button> adOnClickButtons = new List<Button>();

        [Tooltip("Seconds after an on-click ad during which screen-transition ads are suppressed, so one click cannot serve two ads.")]
        [SerializeField] private float clickAdTransitionCooldown = 3f;

        /// <summary>Live debounce window, read from Remote Config.</summary>
        private float TransitionDebounce => RemoteConfigManager.Instance != null
            ? Mathf.Max(0f, RemoteConfigManager.Instance.GetInt(RCInt.TrackerDebounceMs, Mathf.RoundToInt(transitionDebounce * 1000f)) / 1000f)
            : transitionDebounce;

        /// <summary>Live poll period, read from Remote Config.</summary>
        private float PollInterval => RemoteConfigManager.Instance != null
            ? Mathf.Max(0.01f, RemoteConfigManager.Instance.GetInt(RCInt.TrackerPollMs, 100) / 1000f)
            : 0.1f;

        /// <summary>Live start delay before the first tracked screen is reported.</summary>
        private float StartDelay => RemoteConfigManager.Instance != null
            ? Mathf.Max(0f, RemoteConfigManager.Instance.GetInt(RCInt.TrackerStartDelayMs, 150) / 1000f)
            : 0.15f;

        private GameObject _lastActiveScreen = null;
        private float _lastTransitionTime = -10f;
        private Coroutine _trackerCoroutine;
        private readonly Stack<GameObject> _navigationStack = new Stack<GameObject>();
        private readonly List<Button> _boundAdButtons = new List<Button>();
        private float _clickAdCooldownUntil = -1f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            if (autoTrackChildren && (trackedScreens == null || trackedScreens.Count == 0))
            {
                AutoDetectChildPanels();
            }

            BindAdClickButtons();
        }

        private IEnumerator Start()
        {
            // Clean null references from the list
            if (trackedScreens != null)
            {
                trackedScreens.RemoveAll(item => item == null);
            }

            Debug.Log($"<color=#00E5FF><b>[UIScreenTracker]</b> Initialized with <b>{trackedScreens?.Count ?? 0}</b> tracked panels.</color>");
            if (trackedScreens != null && trackedScreens.Count > 0)
            {
                for (int i = 0; i < trackedScreens.Count; i++)
                {
                    Debug.Log($"<color=#90CAF9><b>[UIScreenTracker]</b> Tracked Panel #{i + 1}: <i>{trackedScreens[i].name}</i> (Active: {trackedScreens[i].activeInHierarchy})</color>");
                }
            }

            // Wait a brief moment for UI managers (like MainMenuManager) to initialize and turn on the
            // pending panel. Delay comes from Remote Config (TrackerStartDelayMs).
            yield return new WaitForSeconds(StartDelay);

            // Establish initially active screen
            _lastActiveScreen = GetCurrentlyActiveScreen();
            if (_lastActiveScreen != null)
            {
                _navigationStack.Push(_lastActiveScreen);
                Debug.Log($"<color=#00E5FF><b>[UIScreenTracker]</b> Initial Active Screen: <b>{_lastActiveScreen.name}</b></color>");
                ApplovinAdManager.Instance?.NotifyUIScreenTransition(_lastActiveScreen.name);
            }
            
            // Start continuous monitor coroutine
            _trackerCoroutine = StartCoroutine(ScreenMonitorRoutine());
        }

        private void OnDestroy()
        {
            if (_trackerCoroutine != null)
            {
                StopCoroutine(_trackerCoroutine);
                _trackerCoroutine = null;
            }

            UnbindAdClickButtons();
        }

        private IEnumerator ScreenMonitorRoutine()
        {
            var wait = new WaitForSeconds(PollInterval); // Poll period comes from Remote Config (TrackerPollMs)

            while (true)
            {
                yield return wait;

                if (trackedScreens == null || trackedScreens.Count == 0) continue;

                GameObject currentlyActive = GetCurrentlyActiveScreen();
                if (currentlyActive == null || currentlyActive == _lastActiveScreen) continue;

                // Wait out the debounce instead of dropping the transition. Dropping it used to lose a
                // whole screen from the strategy count whenever two panels were swapped quickly.
                float elapsed = Time.unscaledTime - _lastTransitionTime;
                float debounce = TransitionDebounce;
                if (elapsed < debounce)
                {
                    yield return new WaitForSeconds(debounce - elapsed);

                    // Re-read: the player may have moved on to another panel while we waited.
                    currentlyActive = GetCurrentlyActiveScreen();
                    if (currentlyActive == null || currentlyActive == _lastActiveScreen) continue;
                }

                _lastTransitionTime = Time.unscaledTime;
                _lastActiveScreen = currentlyActive;

                // An on-click ad already played for the press that caused this panel change. Reporting the
                // transition as well used to serve a second ad from the same single click.
                if (Time.unscaledTime < _clickAdCooldownUntil)
                {
                    Debug.Log($"<color=#FFD54F><b>[UIScreenTracker]</b> ⏭️ Panel Change ➔ <b>{currentlyActive.name}</b> suppressed: an on-click ad just played.</color>");
                    continue;
                }

                Debug.Log($"<color=#00E5FF><b>[UIScreenTracker]</b> Panel Change Detected ➔ <b>{currentlyActive.name}</b></color>");
                ApplovinAdManager.Instance?.NotifyUIScreenTransition(currentlyActive.name);
            }
        }

        /// <summary>
        /// Subscribes every Button in the adOnClickButtons list. Listeners are added at runtime, so each
        /// button keeps its existing onClick calls (panel switching, scene loading) and the ad simply plays
        /// in addition to them.
        /// </summary>
        private void BindAdClickButtons()
        {
            if (adOnClickButtons == null) return;

            for (int i = 0; i < adOnClickButtons.Count; i++)
            {
                Button button = adOnClickButtons[i];
                if (button == null || _boundAdButtons.Contains(button)) continue;

                button.onClick.AddListener(() => PlayAdForButton(button));
                _boundAdButtons.Add(button);

                Debug.Log($"<color=#00E5FF><b>[UIScreenTracker]</b> 🖱️ On-click ad bound to button: <b>{button.name}</b></color>");
            }
        }

        private void UnbindAdClickButtons()
        {
            // RemoveAllListeners only clears runtime listeners; the buttons' persistent onClick entries stay.
            for (int i = 0; i < _boundAdButtons.Count; i++)
            {
                if (_boundAdButtons[i] != null)
                {
                    _boundAdButtons[i].onClick.RemoveAllListeners();
                }
            }

            _boundAdButtons.Clear();
        }

        /// <summary>
        /// Plays the ad for one button press. The delivered ad kind is read from the Remote Config key
        /// 'AdDeliveryType' (1 = Interstitial only, 2 = Rewarded only, 3 = Alternating), so the same
        /// setting drives every button in the list.
        /// </summary>
        private void PlayAdForButton(Button button)
        {
            var ads = ApplovinAdManager.Instance;
            if (ads == null)
            {
                Debug.LogWarning("<b>[UIScreenTracker]</b> ⚠️ On-click ad skipped: ApplovinAdManager.Instance is null (MAX has not woken up yet).");
                return;
            }

            string buttonName = button != null ? button.name : "Unknown";
            int deliveryType = RemoteConfigManager.Instance != null
                ? RemoteConfigManager.Instance.GetInt(RCInt.AdDeliveryType, 3)
                : 3;

            // Hold off transition ads for a moment: the click usually switches panels (MainMenu ➔ MachineSelection),
            // and that panel change must not serve a second ad for the same press.
            _clickAdCooldownUntil = Time.unscaledTime + Mathf.Max(0f, clickAdTransitionCooldown);

            string deliveryName = deliveryType == 2 ? "Rewarded" : (deliveryType == 3 ? "Alternating" : "Interstitial");
            Debug.Log($"<color=#00E5FF><b>[UIScreenTracker]</b> 🖱️ On-Click Ad ➔ Button: <b>{buttonName}</b> | RC AdDeliveryType = <b>{deliveryType}</b> ({deliveryName})</color>");

            // Bypasses only the AdStrategyMode 0 gate: a click placement must always try to play.
            ads.ShowStrategyAd($"UI_Click_{buttonName}", true);
        }

        private GameObject GetCurrentlyActiveScreen()
        {
            if (trackedScreens == null) return null;

            // Search the list back-to-front: later entries are the panels that open on top of the
            // earlier ones, so the last active entry is the screen the player is actually looking at.
            // (Returning the first active entry made every sub-panel invisible to the tracker as soon
            // as its parent panel stayed enabled.)
            for (int i = trackedScreens.Count - 1; i >= 0; i--)
            {
                var screen = trackedScreens[i];
                if (screen != null && screen.activeInHierarchy)
                {
                    return screen;
                }
            }
            return null;
        }

        /// <summary>
        /// Automatically populates trackedScreens from child GameObjects.
        /// </summary>
        public void AutoDetectChildPanels()
        {
            if (trackedScreens == null) trackedScreens = new List<GameObject>();
            
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i).gameObject;
                if (!trackedScreens.Contains(child))
                {
                    trackedScreens.Add(child);
                }
            }
        }

        /// <summary>
        /// Scans all Canvases in the current active scene and populates top-level panels.
        /// </summary>
        public void AutoDetectScenePanels()
        {
            if (trackedScreens == null) trackedScreens = new List<GameObject>();

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                for (int i = 0; i < canvas.transform.childCount; i++)
                {
                    var child = canvas.transform.GetChild(i).gameObject;
                    // Ignore common overlay objects like top bars, faders, or event systems
                    string lowerName = child.name.ToLower();
                    if (lowerName.Contains("topbar") || lowerName.Contains("fader") || lowerName.Contains("eventsystem") || lowerName.Contains("audio"))
                    {
                        continue;
                    }

                    if (!trackedScreens.Contains(child))
                    {
                        trackedScreens.Add(child);
                    }
                }
            }
            trackedScreens.RemoveAll(item => item == null);
        }
    }
}
