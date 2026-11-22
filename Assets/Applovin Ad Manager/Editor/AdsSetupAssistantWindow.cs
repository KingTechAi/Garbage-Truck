#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace MangoMango.Advertisement.Editor
{
    public class AdsSetupAssistantWindow : EditorWindow
    {
        private Vector2 scrollPos;

        [MenuItem("Tools/Mango Ads/🚀 New Project Setup Assistant", false, 40)]
        [MenuItem("Window/Mango Ads/🚀 New Project Setup Assistant", false, 40)]
        public static void ShowWindow()
        {
            var window = GetWindow<AdsSetupAssistantWindow>("Ads Setup Assistant");
            window.minSize = new Vector2(560, 620);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            DrawHeader();
            EditorGUILayout.Space(8);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawStep1UPM();
            EditorGUILayout.Space(8);

            DrawStep2AndroidResolver();
            EditorGUILayout.Space(8);

            DrawStep3ScenePrefabs();
            EditorGUILayout.Space(8);

            DrawStep4UIScreenTracker();
            EditorGUILayout.Space(8);

            DrawStep5Dashboard();
            EditorGUILayout.Space(8);

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("🚀 Mango Ads Strategy - New Project Setup Assistant", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("📦 Export Package", EditorStyles.miniButton))
            {
                AdsPackageExporterWindow.ShowWindow();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Use this checklist to configure any new Unity project for the Mango Ads Strategy in under 2 minutes.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawStep1UPM()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("1️⃣ Unity Package Manager (UPM) Dependencies", EditorStyles.boldLabel);

            string manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
            bool manifestExists = File.Exists(manifestPath);
            string manifestContent = manifestExists ? File.ReadAllText(manifestPath) : "";

            bool hasRemoteConfig = manifestContent.Contains("com.unity.remote-config");
            bool hasIosSupport = manifestContent.Contains("com.unity.ads.ios-support");

            DrawStatusRow("com.unity.remote-config", hasRemoteConfig, "Required for live cloud ad parameters and switches.");
            DrawStatusRow("com.unity.ads.ios-support", hasIosSupport, "Required for iOS App Tracking Transparency (ATT).");

            if (!hasRemoteConfig || !hasIosSupport)
            {
                EditorGUILayout.Space(4);
                GUI.backgroundColor = new Color(0.2f, 0.75f, 1f);
                if (GUILayout.Button("➕ 1-Click Install Missing UPM Packages", GUILayout.Height(28)))
                {
                    InstallUPMPackages(manifestPath, manifestContent, !hasRemoteConfig, !hasIosSupport);
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                EditorGUILayout.HelpBox("All core UPM dependencies are properly installed in this project!", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void InstallUPMPackages(string manifestPath, string content, bool addRemoteConfig, bool addIosSupport)
        {
            try
            {
                if (string.IsNullOrEmpty(content)) return;

                int depsIndex = content.IndexOf("\"dependencies\": {", StringComparison.Ordinal);
                if (depsIndex == -1)
                {
                    EditorUtility.DisplayDialog("Error", "Could not locate 'dependencies' block in Packages/manifest.json.", "OK");
                    return;
                }

                int insertPoint = depsIndex + "\"dependencies\": {".Length;
                string toInsert = "\n";

                if (addRemoteConfig)
                {
                    toInsert += "    \"com.unity.remote-config\": \"4.2.5\",\n";
                }
                if (addIosSupport)
                {
                    toInsert += "    \"com.unity.ads.ios-support\": \"1.0.1\",\n";
                }

                string newContent = content.Insert(insertPoint, toInsert);
                File.WriteAllText(manifestPath, newContent);

                AssetDatabase.Refresh();
                UnityEditor.PackageManager.Client.Resolve();

                EditorUtility.DisplayDialog("Packages Added!", "Added Remote Config and iOS Support to manifest.json. Unity is now resolving packages.", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Installation Error", ex.Message, "OK");
            }
        }

        private void DrawStep2AndroidResolver()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("2️⃣ Android Resolver & Gradle Templates", EditorStyles.boldLabel);

            bool hasMainGradle = File.Exists("Assets/Plugins/Android/mainTemplate.gradle");
            bool hasSettingsGradle = File.Exists("Assets/Plugins/Android/settingsTemplate.gradle");
            bool hasProperties = File.Exists("Assets/Plugins/Android/gradleTemplate.properties");

            DrawStatusRow("Assets/Plugins/Android/mainTemplate.gradle", hasMainGradle, "Defines ad mediation dependencies.");
            DrawStatusRow("Assets/Plugins/Android/settingsTemplate.gradle", hasSettingsGradle, "Defines Pangle & Mintegral repositories.");
            DrawStatusRow("Assets/Plugins/Android/gradleTemplate.properties", hasProperties, "Enables AndroidX and Jetifier.");

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("⚙️ Force Resolve Android Dependencies (EDM4U)", GUILayout.Height(28)))
            {
                ExecuteForceResolve();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void ExecuteForceResolve()
        {
            bool executed = EditorApplication.ExecuteMenuItem("Assets/External Dependency Manager/Android Resolver/Force Resolve");
            if (executed)
            {
                Debug.Log("<color=green>[AdsSetupAssistant] Executed Android Resolver Force Resolve successfully.</color>");
            }
            else
            {
                EditorUtility.DisplayDialog("Resolver Notice", "Please go to the top menu:\nAssets > External Dependency Manager > Android Resolver > Force Resolve", "OK");
            }
        }

        private void DrawStep3ScenePrefabs()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("3️⃣ Splash / Initial Scene Prefab Setup", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Every game needs the core Ad Manager & Remote Config manager in its initial scene (DontDestroyOnLoad).", EditorStyles.wordWrappedMiniLabel);

            bool hasAdManagerInScene = UnityEngine.Object.FindAnyObjectByType<ApplovinAdManager>() != null;
            bool hasRemoteConfigInScene = UnityEngine.Object.FindAnyObjectByType<RemoteConfigManager>() != null;
            bool hasTimeBasedInScene = UnityEngine.Object.FindAnyObjectByType<TimeBasedAd>() != null;

            DrawStatusRow("Applovin Ad Manager (Necessary)", hasAdManagerInScene, "Core ad mediation controller.");
            DrawStatusRow("Remote Config Manager (Necessary)", hasRemoteConfigInScene, "Cloud parameters and live remote switches.");
            DrawStatusRow("Time Based Ad (Optional)", hasTimeBasedInScene, "Automatic interval timer ads during gameplay.");

            if (!hasAdManagerInScene || !hasRemoteConfigInScene)
            {
                EditorGUILayout.Space(4);
                GUI.backgroundColor = new Color(0.2f, 0.85f, 0.3f);
                if (GUILayout.Button("✨ 1-Click Add Missing Necessary Prefabs to Active Scene", GUILayout.Height(30)))
                {
                    AddPrefabsToCurrentScene(!hasAdManagerInScene, !hasRemoteConfigInScene, false);
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                EditorGUILayout.HelpBox("All necessary prefabs are present in the current scene!", MessageType.Info);
            }

            if (!hasTimeBasedInScene)
            {
                if (GUILayout.Button("➕ Add Time-Based Ad (Gameplay Timer) to Scene", GUILayout.Height(24)))
                {
                    AddPrefabsToCurrentScene(false, false, true);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void AddPrefabsToCurrentScene(bool addAdManager, bool addRemoteConfig, bool addTimeBased)
        {
            int addedCount = 0;

            if (addAdManager)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Applovin Ad Manager/Prefabs/Necessary/Applovin Ad Manager (Necessary).prefab");
                if (prefab != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    Undo.RegisterCreatedObjectUndo(instance, "Add Applovin Ad Manager");
                    addedCount++;
                }
            }

            if (addRemoteConfig)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Applovin Ad Manager/Prefabs/Necessary/Remote Config Manager (Necessary).prefab");
                if (prefab != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    Undo.RegisterCreatedObjectUndo(instance, "Add Remote Config Manager");
                    addedCount++;
                }
            }

            if (addTimeBased)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Applovin Ad Manager/Prefabs/Optional/Time Based Ad (Optional).prefab");
                if (prefab != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    Undo.RegisterCreatedObjectUndo(instance, "Add Time Based Ad");
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorUtility.DisplayDialog("Prefabs Added", $"Successfully added {addedCount} ad prefab(s) to '{SceneManager.GetActiveScene().name}'.", "OK");
            }
        }

        private void DrawStep4UIScreenTracker()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("4️⃣ Event-Based Ads (UIScreenTracker)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("To automatically trigger interstitial and rewarded ads when users navigate between menus (MainMenu -> Store -> LevelSelect), attach UIScreenTracker in your Menu scene.", EditorStyles.wordWrappedMiniLabel);

            bool hasTrackerInScene = UnityEngine.Object.FindAnyObjectByType<UIScreenTracker>() != null;
            DrawStatusRow("UIScreenTracker Component", hasTrackerInScene, "Tracks panel navigation and fires ad events.");

            if (!hasTrackerInScene)
            {
                EditorGUILayout.Space(4);
                if (GUILayout.Button("➕ Add UIScreenTracker to Current Scene", GUILayout.Height(26)))
                {
                    var go = new GameObject("UIScreenTracker");
                    go.AddComponent<UIScreenTracker>();
                    Undo.RegisterCreatedObjectUndo(go, "Create UIScreenTracker");
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    Selection.activeGameObject = go;
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawStep5Dashboard()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("5️⃣ Ad Strategy Dashboard & Live Testing", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Verify your live cloud keys, test ad placements, and inspect remote configurations in real-time.", EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📊 Open Ad Strategy Dashboard", GUILayout.Height(32)))
            {
                AdStrategyDashboardWindow.ShowWindow();
            }
            if (GUILayout.Button("📦 Export Package", GUILayout.Height(32)))
            {
                AdsPackageExporterWindow.ShowWindow();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawStatusRow(string title, bool isOk, string description)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(isOk ? "✅" : "⚠️", GUILayout.Width(22));
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel, GUILayout.Width(260));
            EditorGUILayout.LabelField(description, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
