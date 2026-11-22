#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.RemoteConfig;
using MangoMango.Advertisement;

namespace MangoMango.Advertisement.Editor
{
    public class AdStrategyDashboardWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private int selectedTab = 0;
        private readonly string[] tabs = { "Live Cloud Values", "Ad Strategy & Modes", "Scene Setup", "Master Switches", "Ad Unit IDs", "Live Testing Suite", "Cloud CSV Reference", "📦 Export Package" };

        private string customSceneInput = "";
        private string searchFilter = "";
        private static bool isFetching = false;
        private static string lastFetchStatus = "Click 'Fetch Live Config' to connect and retrieve keys.";
        private static DateTime lastFetchTime;

        [MenuItem("Tools/Ad Strategy & Remote Config Dashboard", false, 100)]
        [MenuItem("Window/Mango Ads/Ad Strategy Dashboard", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<AdStrategyDashboardWindow>("Ad Strategy Dashboard");
            window.minSize = new Vector2(550, 620);
            window.Show();
        }

        private void OnEnable()
        {
            // Auto fetch if in play mode and already initialized
            if (Application.isPlaying && RemoteConfigManager.Instance != null)
            {
                lastFetchStatus = "Connected to live Runtime Remote Config.";
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            DrawHeader();
            EditorGUILayout.Space(10);

            selectedTab = GUILayout.Toolbar(selectedTab, tabs, GUILayout.Height(30));
            EditorGUILayout.Space(10);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            switch (selectedTab)
            {
                case 0:
                    DrawLiveCloudValuesTab();
                    break;
                case 1:
                    DrawStrategyModesTab();
                    break;
                case 2:
                    DrawSceneSetupTab();
                    break;
                case 3:
                    DrawMasterSwitchesTab();
                    break;
                case 4:
                    DrawAdUnitIDsTab();
                    break;
                case 5:
                    DrawLiveTestingTab();
                    break;
                case 6:
                    DrawCloudCSVReferenceTab();
                    break;
                case 7:
                    DrawExportPackageTab();
                    break;
            }

            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("🥭 Mango Mango Ad Strategy & Remote Config Dashboard", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("📦 Export Package", EditorStyles.miniButton, GUILayout.Width(110)))
            {
                AdsPackageExporterWindow.ShowWindow();
            }
            if (GUILayout.Button("❓ Setup Guide", EditorStyles.miniButton, GUILayout.Width(90)))
            {
                AdsSetupAssistantWindow.ShowWindow();
            }
            EditorGUILayout.EndHorizontal();
            
            bool isPlaying = Application.isPlaying;
            string status = isPlaying ? "🟢 Running in Play Mode" : "⚪ Editor Mode (Static / Live Fetch Available)";
            EditorGUILayout.LabelField("State:", status);

            if (isPlaying && ApplovinAdManager.Instance != null)
            {
                string sceneType = ApplovinAdManager.Instance.IsUIScene() ? "🖥️ UI / Menu Scene (Event-based ads)" : "🚜 Gameplay Scene (Time-based ads)";
                EditorGUILayout.LabelField("Active Scene:", $"{SceneManager.GetActiveScene().name} -> {sceneType}");
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawLiveCloudValuesTab()
        {
            EditorGUILayout.LabelField("☁️ Live Remote Config Cloud Inspector", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Fetch and inspect the exact values currently returned by Unity Gaming Services Remote Config in real-time.", MessageType.Info);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = !isFetching;
            if (GUILayout.Button(isFetching ? "⏳ Fetching from Cloud..." : "🔄 Fetch Live Config from Cloud", GUILayout.Height(32)))
            {
                FetchLiveRemoteConfig();
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Status:", lastFetchStatus, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("🔍 Filter Keys:", GUILayout.Width(90));
            searchFilter = EditorGUILayout.TextField(searchFilter);
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                searchFilter = "";
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            DrawRemoteConfigTable();
        }

        private async void FetchLiveRemoteConfig()
        {
            isFetching = true;
            lastFetchStatus = "Connecting to Unity Gaming Services...";
            Repaint();

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

                lastFetchStatus = "Fetching remote configurations from cloud...";
                Repaint();

                RemoteConfigService.Instance.FetchCompleted += OnEditorFetchCompleted;
                RemoteConfigService.Instance.FetchConfigs(new UserAttributes(), new AppAttributes());
            }
            catch (Exception ex)
            {
                isFetching = false;
                lastFetchStatus = $"❌ Error: {ex.Message}";
                Repaint();
            }
        }

        private void OnEditorFetchCompleted(ConfigResponse response)
        {
            RemoteConfigService.Instance.FetchCompleted -= OnEditorFetchCompleted;
            isFetching = false;
            lastFetchTime = DateTime.Now;

            if (response.status == ConfigRequestStatus.Success)
            {
                lastFetchStatus = $"✅ Successfully Fetched at {lastFetchTime:HH:mm:ss} (Origin: {response.requestOrigin})";
            }
            else
            {
                lastFetchStatus = $"⚠️ Fetch Response: {response.status}";
            }
            Repaint();
        }

        private void DrawRemoteConfigTable()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Key Name", EditorStyles.boldLabel, GUILayout.Width(200));
            GUILayout.Label("Type", EditorStyles.boldLabel, GUILayout.Width(70));
            GUILayout.Label("Cloud Value", EditorStyles.boldLabel, GUILayout.Width(180));
            GUILayout.Label("Source / Status", EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            var appConfig = RemoteConfigService.Instance?.appConfig;
            bool hasAppConfig = appConfig != null;

            // 1. Booleans
            foreach (RCBool keyEnum in Enum.GetValues(typeof(RCBool)))
            {
                string keyName = keyEnum.ToString();
                if (!MatchesFilter(keyName)) continue;

                bool val = hasAppConfig ? appConfig.GetBool(keyName, false) : false;
                bool existsInCloud = hasAppConfig && appConfig.HasKey(keyName);

                DrawTableRow(keyName, "bool", val ? "TRUE" : "FALSE", val ? Color.green : Color.red, existsInCloud);
            }

            // 2. Integers
            foreach (RCInt keyEnum in Enum.GetValues(typeof(RCInt)))
            {
                string keyName = keyEnum.ToString();
                if (!MatchesFilter(keyName)) continue;

                int val = hasAppConfig ? appConfig.GetInt(keyName, 0) : 0;
                bool existsInCloud = hasAppConfig && appConfig.HasKey(keyName);

                DrawTableRow(keyName, "int", val.ToString(), new Color(0.3f, 0.8f, 1f), existsInCloud);
            }

            // 3. Strings
            foreach (RCString keyEnum in Enum.GetValues(typeof(RCString)))
            {
                string keyName = keyEnum.ToString();
                if (!MatchesFilter(keyName)) continue;

                string val = hasAppConfig ? appConfig.GetString(keyName, "—") : "—";
                bool existsInCloud = hasAppConfig && appConfig.HasKey(keyName);

                DrawTableRow(keyName, "string", string.IsNullOrEmpty(val) ? "(empty)" : val, new Color(1f, 0.8f, 0.3f), existsInCloud);
            }

            // 4. Floats
            foreach (RCFloat keyEnum in Enum.GetValues(typeof(RCFloat)))
            {
                string keyName = keyEnum.ToString();
                if (!MatchesFilter(keyName)) continue;

                float val = hasAppConfig ? appConfig.GetFloat(keyName, 0f) : 0f;
                bool existsInCloud = hasAppConfig && appConfig.HasKey(keyName);

                DrawTableRow(keyName, "float", val.ToString("F1"), new Color(0.8f, 0.5f, 1f), existsInCloud);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTableRow(string key, string type, string value, Color valueColor, bool existsInCloud)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(key, GUILayout.Width(200));
            GUILayout.Label(type, EditorStyles.miniLabel, GUILayout.Width(70));

            GUIStyle valStyle = new GUIStyle(EditorStyles.boldLabel);
            valStyle.normal.textColor = valueColor;
            GUILayout.Label(value, valStyle, GUILayout.Width(180));

            string sourceText = existsInCloud ? "☁️ Cloud" : "⚪ Default";
            GUILayout.Label(sourceText, EditorStyles.miniLabel, GUILayout.Width(100));

            EditorGUILayout.EndHorizontal();
        }

        private bool MatchesFilter(string keyName)
        {
            if (string.IsNullOrEmpty(searchFilter)) return true;
            return keyName.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawStrategyModesTab()
        {
            EditorGUILayout.LabelField("🎯 High-Level Strategy Modes", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select how ads are triggered across the game. Configurable remotely via 'AdStrategyMode'.", MessageType.Info);

            int currentMode = RemoteConfigManager.Instance != null 
                ? RemoteConfigManager.Instance.GetInt(RCInt.AdStrategyMode, 2) 
                : 2;

            string[] modeOptions = new string[]
            {
                "0: Time-Based Ads Only (Session Timers only, UI events ignored)",
                "1: Event-Based Ads Only (UI Transitions / Level Events only, Timers silenced)",
                "2: Hybrid Mode (UI Screen Transitions + Gameplay Timers) [Recommended]"
            };

            int selectedMode = EditorGUILayout.Popup("Ad Strategy Mode", currentMode, modeOptions);
            if (selectedMode != currentMode)
            {
                Debug.Log($"[Dashboard] Changed AdStrategyMode to: {selectedMode}");
            }

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("📦 Ad Delivery Format (System Ads)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Controls whether system/timer ads serve Interstitials, Rewarded ads with coins, or Alternate.", MessageType.Info);

            int currentDelivery = RemoteConfigManager.Instance != null 
                ? RemoteConfigManager.Instance.GetInt(RCInt.AdDeliveryType, 3) 
                : 3;

            string[] deliveryOptions = new string[]
            {
                "0: Legacy — identical to 1 (Only Interstitials)",
                "1: Only Interstitials (Standard full-screen) [Recommended]",
                "2: Only Rewarded (Rewarded Video + System Coins)",
                "3: Alternating (1 Interstitial ⇄ 1 Rewarded + Coins)"
            };

            int selectedDelivery = EditorGUILayout.Popup("Ad Delivery Type", currentDelivery, deliveryOptions);
            if (selectedDelivery != currentDelivery)
            {
                Debug.Log($"[Dashboard] Changed AdDeliveryType to: {selectedDelivery}");
            }

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("⏱️ Pacing & Counters", EditorStyles.boldLabel);

            int eventFreq = RemoteConfigManager.Instance != null ? RemoteConfigManager.Instance.GetInt(RCInt.EventAdFrequency, 2) : 2;
            int timeInt = RemoteConfigManager.Instance != null ? RemoteConfigManager.Instance.GetInt(RCInt.TimeBasedAdInt, 60) : 60;
            int systemCoins = RemoteConfigManager.Instance != null ? RemoteConfigManager.Instance.GetInt(RCInt.SystemRewardedCoins, 500) : 500;
            int loadingTime = RemoteConfigManager.Instance != null ? RemoteConfigManager.Instance.GetInt(RCInt.LoadingTime, 3) : 3;

            string freqDescription = eventFreq <= 1 ? "Show Ad on ALL Screen Transitions (1:1)" : (eventFreq == 2 ? "ALTERNATING Screens (1st: Ad, 2nd: No Ad, 3rd: Ad...)" : $"Every {eventFreq}th Screen");
            EditorGUILayout.LabelField("Event Screen Frequency:", $"{eventFreq} -> {freqDescription}", EditorStyles.boldLabel);
            EditorGUILayout.IntField("Time-Based Timer Interval (Seconds):", timeInt);
            EditorGUILayout.IntField("System Rewarded Coins Payout:", systemCoins);
            EditorGUILayout.IntField("Loading Screen Minimum Duration (Seconds):", loadingTime);
        }

        private void DrawSceneSetupTab()
        {
            EditorGUILayout.LabelField("🎬 Dynamic Scene Setup (No Hardcoding)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Configure which scenes in your build are UI / Menu scenes (for Hybrid mode) and which scenes exclude App Open ads. All scenes are dynamically resolved without hardcoding.", MessageType.Info);

            ApplovinAdManager targetManager = GetTargetAdManager();

            if (targetManager == null)
            {
                EditorGUILayout.HelpBox("Could not locate ApplovinAdManager in scene or prefabs.", MessageType.Warning);
                return;
            }

            SerializedObject serializedObj = new SerializedObject(targetManager);
            SerializedProperty uiScenesProp = serializedObj.FindProperty("uiSceneNames");
            SerializedProperty appOpenExcludedProp = serializedObj.FindProperty("appOpenExcludedScenes");

            serializedObj.Update();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Build Settings Scenes & Classification", EditorStyles.boldLabel);

            var buildScenes = EditorBuildSettings.scenes;
            if (buildScenes.Length == 0)
            {
                EditorGUILayout.HelpBox("No scenes found in Build Settings! Open File > Build Settings to add scenes.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Scene Name", EditorStyles.boldLabel, GUILayout.Width(180));
                GUILayout.Label("Is UI / Menu?", EditorStyles.boldLabel, GUILayout.Width(110));
                GUILayout.Label("Exclude App Open?", EditorStyles.boldLabel, GUILayout.Width(130));
                EditorGUILayout.EndHorizontal();

                foreach (var scene in buildScenes)
                {
                    if (string.IsNullOrEmpty(scene.path)) continue;
                    string sceneName = Path.GetFileNameWithoutExtension(scene.path);

                    bool isUI = ContainsStringProperty(uiScenesProp, sceneName);
                    bool isExcluded = ContainsStringProperty(appOpenExcludedProp, sceneName);

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(sceneName, GUILayout.Width(180));

                    bool newUI = EditorGUILayout.Toggle(isUI, GUILayout.Width(110));
                    if (newUI != isUI)
                    {
                        SetStringProperty(uiScenesProp, sceneName, newUI);
                    }

                    bool newExcluded = EditorGUILayout.Toggle(isExcluded, GUILayout.Width(130));
                    if (newExcluded != isExcluded)
                    {
                        SetStringProperty(appOpenExcludedProp, sceneName, newExcluded);
                    }

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Add Custom Scene Name", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            customSceneInput = EditorGUILayout.TextField(customSceneInput);
            if (GUILayout.Button("Add as UI Scene", GUILayout.Width(130)) && !string.IsNullOrEmpty(customSceneInput))
            {
                SetStringProperty(uiScenesProp, customSceneInput.Trim(), true);
                customSceneInput = "";
            }
            if (GUILayout.Button("Add as Excluded", GUILayout.Width(120)) && !string.IsNullOrEmpty(customSceneInput))
            {
                SetStringProperty(appOpenExcludedProp, customSceneInput.Trim(), true);
                customSceneInput = "";
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.PropertyField(uiScenesProp, new GUIContent("Configured UI Scene Names"), true);
            EditorGUILayout.PropertyField(appOpenExcludedProp, new GUIContent("Configured App Open Excluded Scenes"), true);

            if (serializedObj.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(targetManager);
            }

            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("📱 Active Scene UI Screen Panels (GameObject Tracker)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Instead of writing code, you can drop your UI Screen/Panel GameObjects into the UIScreenTracker. It detects transitions automatically when any panel becomes active.", MessageType.Info);

            var sceneTracker = UnityEngine.Object.FindFirstObjectByType<UIScreenTracker>();
            if (sceneTracker != null)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Tracker Object in Scene:", sceneTracker.gameObject.name, EditorStyles.boldLabel);
                
                SerializedObject trackerObj = new SerializedObject(sceneTracker);
                SerializedProperty trackedPanelsProp = trackerObj.FindProperty("trackedScreens");
                trackerObj.Update();

                EditorGUILayout.PropertyField(trackedPanelsProp, new GUIContent("Assigned Screen Panels"), true);

                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("🔍 Auto-Detect Panels from Canvases", GUILayout.Height(26)))
                {
                    Undo.RecordObject(sceneTracker, "Auto-Detect Panels");
                    sceneTracker.AutoDetectScenePanels();
                    EditorUtility.SetDirty(sceneTracker);
                }
                if (GUILayout.Button("🔍 Auto-Detect Child Panels", GUILayout.Height(26)))
                {
                    Undo.RecordObject(sceneTracker, "Auto-Detect Child Panels");
                    sceneTracker.AutoDetectChildPanels();
                    EditorUtility.SetDirty(sceneTracker);
                }
                EditorGUILayout.EndHorizontal();

                if (trackerObj.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(sceneTracker);
                }
                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.HelpBox("No UIScreenTracker found in the current scene.", MessageType.Info);
                if (GUILayout.Button("➕ Create UI Screen Tracker in Current Scene", GUILayout.Height(30)))
                {
                    GameObject trackerGO = new GameObject("UI Screen Tracker");
                    var tracker = trackerGO.AddComponent<UIScreenTracker>();
                    tracker.AutoDetectScenePanels();
                    Undo.RegisterCreatedObjectUndo(trackerGO, "Create UI Screen Tracker");
                    Selection.activeGameObject = trackerGO;
                }
            }
        }

        private ApplovinAdManager GetTargetAdManager()
        {
            if (Application.isPlaying && ApplovinAdManager.Instance != null)
            {
                return ApplovinAdManager.Instance;
            }

            var inScene = UnityEngine.Object.FindFirstObjectByType<ApplovinAdManager>();
            if (inScene != null) return inScene;

            string[] guids = AssetDatabase.FindAssets("t:Prefab Applovin Ad Manager");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    var manager = prefab.GetComponent<ApplovinAdManager>();
                    if (manager != null) return manager;
                }
            }
            return null;
        }

        private bool ContainsStringProperty(SerializedProperty listProp, string value)
        {
            for (int i = 0; i < listProp.arraySize; i++)
            {
                if (listProp.GetArrayElementAtIndex(i).stringValue == value) return true;
            }
            return false;
        }

        private void SetStringProperty(SerializedProperty listProp, string value, bool enable)
        {
            int foundIndex = -1;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                if (listProp.GetArrayElementAtIndex(i).stringValue == value)
                {
                    foundIndex = i;
                    break;
                }
            }

            if (enable && foundIndex == -1)
            {
                listProp.InsertArrayElementAtIndex(listProp.arraySize);
                listProp.GetArrayElementAtIndex(listProp.arraySize - 1).stringValue = value;
            }
            else if (!enable && foundIndex != -1)
            {
                listProp.DeleteArrayElementAtIndex(foundIndex);
            }
        }

        private void DrawMasterSwitchesTab()
        {
            EditorGUILayout.LabelField("🎛️ Master Remote Config Toggles", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("These reflect the current live state from RemoteConfigManager.", MessageType.Info);

            bool enableAds = RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.GetBool(RCBool.EnableAds, false);
            bool showRewarded = RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.GetBool(RCBool.ShowRewarded, true);
            bool bannerCheck = RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.GetBool(RCBool.BannerCheckAll, true);
            bool appOpen = RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.GetBool(RCBool.EnableAppOpenAD, true);
            bool mrec = RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.GetBool(RCBool.MREC, false);
            bool timeBased = RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.GetBool(RCBool.TimeBasedAdBool, true);
            bool showAdTimer = RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.GetBool(RCBool.ShowAdTimer, true);
            bool testAds = RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.GetBool(RCBool.TestAds, false);

            EditorGUILayout.Toggle("EnableAds (Master Switch)", enableAds);
            EditorGUILayout.Toggle("ShowRewarded (Master Rewarded)", showRewarded);
            EditorGUILayout.Toggle("BannerCheckAll", bannerCheck);
            EditorGUILayout.Toggle("EnableAppOpenAD", appOpen);
            EditorGUILayout.Toggle("MREC", mrec);
            EditorGUILayout.Toggle("TimeBasedAdBool", timeBased);
            EditorGUILayout.Toggle("ShowAdTimer (5s Warning UI)", showAdTimer);
            EditorGUILayout.Toggle("TestAds (Mediation Debugger)", testAds);
        }

        private void DrawAdUnitIDsTab()
        {
            EditorGUILayout.LabelField("🔑 AppLovin MAX Ad Unit IDs", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("These are fetched from Remote Config on startup.", MessageType.Info);

            string inter = RemoteConfigManager.Instance != null ? RemoteConfigManager.Instance.GetString(RCString.InterstitialAdID, "76bd07d04c6330fd") : "76bd07d04c6330fd";
            string banner = RemoteConfigManager.Instance != null ? RemoteConfigManager.Instance.GetString(RCString.ControllableBannerAdUnitId, "b358af0e91ed216c") : "b358af0e91ed216c";
            string fixedBanner = RemoteConfigManager.Instance != null ? RemoteConfigManager.Instance.GetString(RCString.TopRightFixedBannerAdUnitId, "a1e501fc80d19503") : "a1e501fc80d19503";
            string rewarded = RemoteConfigManager.Instance != null ? RemoteConfigManager.Instance.GetString(RCString.RewardedAdID, "dbbdb8f006d91a71") : "dbbdb8f006d91a71";
            string mrec = RemoteConfigManager.Instance != null ? RemoteConfigManager.Instance.GetString(RCString.MRecAdID, "01c165091ce75803") : "01c165091ce75803";
            string appOpen = RemoteConfigManager.Instance != null ? RemoteConfigManager.Instance.GetString(RCString.AppOpenAdUnitId, "52ed65ceead96750") : "52ed65ceead96750";

            EditorGUILayout.TextField("Interstitial Ad ID:", inter);
            EditorGUILayout.TextField("Controllable Banner ID:", banner);
            EditorGUILayout.TextField("Top-Right Fixed Banner ID:", fixedBanner);
            EditorGUILayout.TextField("Rewarded Ad ID:", rewarded);
            EditorGUILayout.TextField("MREC Ad ID:", mrec);
            EditorGUILayout.TextField("App Open Ad ID:", appOpen);
        }

        private void DrawLiveTestingTab()
        {
            EditorGUILayout.LabelField("🧪 Live Testing & Simulation Suite", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to run live ad tests, simulate screen transitions, and test callbacks.", MessageType.Warning);
                return;
            }

            if (ApplovinAdManager.Instance == null)
            {
                EditorGUILayout.HelpBox("ApplovinAdManager is not present in the current active scene.", MessageType.Error);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Live Runtime Status", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Active Scene Classification:", ApplovinAdManager.Instance.IsUIScene() ? "🖥️ UI / Menu Scene" : "🚜 Gameplay Scene");
            EditorGUILayout.LabelField("UI Screen Transition Counter:", $"{ApplovinAdManager.Instance.uiScreenTransitionCount}");
            EditorGUILayout.LabelField("Strategy Ad Cycle Counter:", $"{ApplovinAdManager.Instance.strategyAdCycleCount}");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            GUILayout.Label("Simulations & Ad Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("⏩ Simulate Forward Nav (+1)", GUILayout.Height(28)))
            {
                ApplovinAdManager.Instance.NotifyUIScreenTransition("Simulated_Forward_Panel", false);
            }
            if (GUILayout.Button("⏪ Simulate Back Nav (Ad-Free)", GUILayout.Height(28)))
            {
                ApplovinAdManager.Instance.NotifyUIScreenTransition("Simulated_Back_Panel", true);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("⚡ Fire Strategy Ad Now", GUILayout.Height(28)))
            {
                ApplovinAdManager.Instance.ShowStrategyAd("Dashboard_Manual_Trigger");
            }
            if (GUILayout.Button("🔄 Reset Counters & History", GUILayout.Height(28)))
            {
                ApplovinAdManager.Instance.uiScreenTransitionCount = 0;
                ApplovinAdManager.Instance.strategyAdCycleCount = 0;
                ApplovinAdManager.Instance.ResetScreenHistory();
                Debug.Log("<color=#00E5FF><b>[Dashboard]</b> UI Transition counter, Ad Cycle counter, and Screen History reset to 0.</color>");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📺 Test Interstitial Ad", GUILayout.Height(26)))
            {
                ApplovinAdManager.Instance.ShowInterstitial(RCBool.TimeBasedAdBool);
            }
            if (GUILayout.Button("🎁 Test Rewarded Ad (+Coins)", GUILayout.Height(26)))
            {
                ApplovinAdManager.Instance.ShowRewardedAd(() =>
                {
                    ApplovinAdManager.GrantRewardCoins(500);
                    Debug.Log("<color=green>[Dashboard] Test Reward Completed! +500 Coins added.</color>");
                });
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("👁 Show Banner", GUILayout.Height(24)))
            {
                ApplovinAdManager.Instance.ShowBanner();
            }
            if (GUILayout.Button("🙈 Hide Banner", GUILayout.Height(24)))
            {
                ApplovinAdManager.Instance.HideBanner();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("👁 Show MREC", GUILayout.Height(24)))
            {
                ApplovinAdManager.Instance.ShowMRecBanner();
            }
            if (GUILayout.Button("🙈 Hide MREC", GUILayout.Height(24)))
            {
                ApplovinAdManager.Instance.HideMRecBanner();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            if (GUILayout.Button("🐞 Open AppLovin Mediation Debugger", GUILayout.Height(30)))
            {
                MaxSdk.ShowMediationDebugger();
            }
        }

        private void DrawCloudCSVReferenceTab()
        {
            EditorGUILayout.LabelField("☁️ Unity Cloud Remote Config Keys", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Below are the exact keys and recommended values to add to your Unity Cloud Dashboard for the 3-mode strategy.", MessageType.Info);

            string csvContent = 
@"AdStrategyMode,int,2
AdDeliveryType,int,3
EventAdFrequency,int,2
SystemRewardedCoins,int,500
TimeBasedAdInt,int,60
TimeBasedAdCountdown,int,5
LoadingTime,int,3
UITransitionDebounceMs,int,800
TrackerPollMs,int,100
TrackerDebounceMs,int,500
TrackerStartDelayMs,int,150
BackNavigationAds,bool,false
ReturnToPreviousScreenAds,bool,false
RewardedFallbackToInterstitial,bool,true
UISceneKeywords,string,splash,track,menu,loading,select,garage
GameplaySceneKeywords,string,farming,game,level,play
ShowRewardedAfterTime,bool,true
ShowRewardedAfterTimeInterval,int,1
EnableAds,bool,true
ShowRewarded,bool,true
BannerCheckAll,bool,true
ControllableBanner,bool,true
TopRightFixedBanner,bool,true
MREC,bool,true
EnableAppOpenAD,bool,true
TimeBasedAdBool,bool,true
ShowAdTimer,bool,true";

            EditorGUILayout.TextArea(csvContent, GUILayout.Height(220));

            if (GUILayout.Button("📋 Copy New Strategy Keys to Clipboard", GUILayout.Height(32)))
            {
                EditorGUIUtility.systemCopyBuffer = csvContent;
                Debug.Log("<color=green>[Dashboard] Remote Config Keys copied to clipboard!</color>");
            }
        }

        private void DrawExportPackageTab()
        {
            EditorGUILayout.LabelField("📦 Export Ads Strategy Package (.unitypackage)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Export all essential ads systems, AppLovin MAX SDK, mediation adapters, EDM4U Android resolver, and gradle templates into a clean .unitypackage to use in any new project!", MessageType.Info);

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("✨ What Will Be Exported:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• 🥭 Assets/Applovin Ad Manager (Scripts, Prefabs, Trackers, Sprites, Dashboard)", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• 📱 Assets/MaxSdk (MAX SDK + Google, Meta, UnityAds, ByteDance, Mintegral, Vungle adapters)", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• ⚙️ Assets/ExternalDependencyManager (EDM4U native Android & iOS library resolver)", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• 🤖 Assets/Plugins/Android (Preconfigured gradle, settings & properties templates)", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• 💎 Assets/IAP (Optional In-App Purchases for Remove Ads & Bundles)", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.3f);
            if (GUILayout.Button("📦 Open Ads Package Exporter...", GUILayout.Height(38)))
            {
                AdsPackageExporterWindow.ShowWindow();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(6);
            if (GUILayout.Button("🚀 Open New Project Setup Assistant...", GUILayout.Height(30)))
            {
                AdsSetupAssistantWindow.ShowWindow();
            }
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("AppLovin Ad Manager v2.0 • Hybrid Strategy Enabled", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("📦 Export Package", EditorStyles.toolbarButton))
            {
                AdsPackageExporterWindow.ShowWindow();
            }
            if (GUILayout.Button("❓ Setup Guide", EditorStyles.toolbarButton))
            {
                AdsSetupAssistantWindow.ShowWindow();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
