#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MangoMango.Advertisement.Editor
{
    public class AdsPackageExporterWindow : EditorWindow
    {
        public enum ExportPreset
        {
            FullAdsStrategyBundle, // Recommended: Ads Manager + MAX + EDM4U + Android Gradle
            MonetizationSuite,     // Ads + IAP
            AdsStrategyOnly,       // Applovin Ad Manager only
            Custom                 // Manual folder selection
        }

        private ExportPreset currentPreset = ExportPreset.FullAdsStrategyBundle;

        // Package components definition
        public class PackageComponent
        {
            public string Name;
            public string Path;
            public string Description;
            public bool IsRequired;
            public bool IsSelected;
            public bool Exists => Directory.Exists(Path) || File.Exists(Path);

            public int FileCount
            {
                get
                {
                    if (!Exists) return 0;
                    if (File.Exists(Path)) return 1;
                    return Directory.GetFiles(Path, "*.*", SearchOption.AllDirectories)
                        .Count(f => !f.EndsWith(".meta") && !f.EndsWith(".DS_Store"));
                }
            }

            public long TotalSizeBytes
            {
                get
                {
                    if (!Exists) return 0;
                    if (File.Exists(Path)) return new FileInfo(Path).Length;
                    return Directory.GetFiles(Path, "*.*", SearchOption.AllDirectories)
                        .Where(f => !f.EndsWith(".meta") && !f.EndsWith(".DS_Store"))
                        .Sum(f => new FileInfo(f).Length);
                }
            }
        }

        private List<PackageComponent> components = new List<PackageComponent>();
        private Vector2 scrollPos;
        private string customFileName = "MangoAdsStrategy_FullBundle.unitypackage";

        [MenuItem("Tools/Mango Ads/📦 Export Ads Package...", false, 50)]
        [MenuItem("Window/Mango Ads/📦 Export Ads Package...", false, 50)]
        public static void ShowWindow()
        {
            var window = GetWindow<AdsPackageExporterWindow>("Ads Package Exporter");
            window.minSize = new Vector2(560, 620);
            window.Show();
        }

        private void OnEnable()
        {
            InitComponents();
            ApplyPreset(currentPreset);
        }

        private void InitComponents()
        {
            components = new List<PackageComponent>
            {
                new PackageComponent
                {
                    Name = "🥭 Ads Strategy Engine & Dashboard",
                    Path = "Assets/Applovin Ad Manager",
                    Description = "Ad Manager, Remote Config, Time & Event Tracker scripts, Prefabs, Sprites, Icons & Strategy Dashboard.",
                    IsRequired = true,
                    IsSelected = true
                },
                new PackageComponent
                {
                    Name = "📱 AppLovin MAX SDK & Mediation Adapters",
                    Path = "Assets/MaxSdk",
                    Description = "AppLovin MAX SDK runtime, settings, plugins and mediation adapters (Google, UnityAds, Meta, Pangle, Mintegral, Vungle).",
                    IsRequired = true,
                    IsSelected = true
                },
                new PackageComponent
                {
                    Name = "⚙️ External Dependency Manager (EDM4U)",
                    Path = "Assets/ExternalDependencyManager",
                    Description = "Automated Android Resolver & iOS CocoaPods resolver for downloading native ad network libraries.",
                    IsRequired = true,
                    IsSelected = true
                },
                new PackageComponent
                {
                    Name = "🤖 Android Gradle & Jetifier Templates",
                    Path = "Assets/Plugins/Android",
                    Description = "mainTemplate.gradle, settingsTemplate.gradle, and gradleTemplate.properties preconfigured for MAX mediation.",
                    IsRequired = true,
                    IsSelected = true
                },
                new PackageComponent
                {
                    Name = "💎 In-App Purchases System (MangoMango IAP)",
                    Path = "Assets/IAP",
                    Description = "Optional: IAPManager, IAPData, Remove Ads flow, and purchase triggers.",
                    IsRequired = false,
                    IsSelected = false
                }
            };
        }

        private void ApplyPreset(ExportPreset preset)
        {
            currentPreset = preset;
            switch (preset)
            {
                case ExportPreset.FullAdsStrategyBundle:
                    SetSelected("Assets/Applovin Ad Manager", true);
                    SetSelected("Assets/MaxSdk", true);
                    SetSelected("Assets/ExternalDependencyManager", true);
                    SetSelected("Assets/Plugins/Android", true);
                    SetSelected("Assets/IAP", false);
                    customFileName = "MangoAdsStrategy_FullBundle.unitypackage";
                    break;

                case ExportPreset.MonetizationSuite:
                    SetSelected("Assets/Applovin Ad Manager", true);
                    SetSelected("Assets/MaxSdk", true);
                    SetSelected("Assets/ExternalDependencyManager", true);
                    SetSelected("Assets/Plugins/Android", true);
                    SetSelected("Assets/IAP", true);
                    customFileName = "MangoMonetization_AdsAndIAP_Bundle.unitypackage";
                    break;

                case ExportPreset.AdsStrategyOnly:
                    SetSelected("Assets/Applovin Ad Manager", true);
                    SetSelected("Assets/MaxSdk", false);
                    SetSelected("Assets/ExternalDependencyManager", false);
                    SetSelected("Assets/Plugins/Android", false);
                    SetSelected("Assets/IAP", false);
                    customFileName = "MangoAdsStrategy_EngineOnly.unitypackage";
                    break;

                case ExportPreset.Custom:
                    break;
            }
        }

        private void SetSelected(string path, bool selected)
        {
            var comp = components.FirstOrDefault(c => c.Path == path);
            if (comp != null) comp.IsSelected = selected;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            DrawHeader();
            EditorGUILayout.Space(8);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawPresetSelector();
            EditorGUILayout.Space(10);

            DrawComponentsList();
            EditorGUILayout.Space(10);

            DrawSummaryBox();
            EditorGUILayout.Space(10);

            EditorGUILayout.EndScrollView();

            DrawActionButtons();
            EditorGUILayout.Space(8);
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("📦 Mango Ads Strategy Package Exporter", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("❓ New Project Setup Guide", EditorStyles.miniButton))
            {
                AdsSetupAssistantWindow.ShowWindow();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Export the complete ads strategy, MAX SDK, mediation adapters, and gradle templates to import into any new project.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawPresetSelector()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎯 Export Preset:", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var newPreset = (ExportPreset)EditorGUILayout.EnumPopup("Select Preset", currentPreset);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyPreset(newPreset);
            }

            string presetDescription = "";
            switch (currentPreset)
            {
                case ExportPreset.FullAdsStrategyBundle:
                    presetDescription = "🌟 <b>Recommended</b>: Exports the complete Ads Strategy System, AppLovin MAX SDK, all Mediation Adapters, EDM4U Android Resolver, and Android Gradle templates. Everything needed for a new project!";
                    break;
                case ExportPreset.MonetizationSuite:
                    presetDescription = "💰 <b>Full Suite</b>: Exports everything in Full Ads Bundle PLUS the MangoMango In-App Purchase (IAP) system for Remove Ads and Bundles.";
                    break;
                case ExportPreset.AdsStrategyOnly:
                    presetDescription = "⚡ <b>Lightweight</b>: Exports only the Ads Strategy scripts, prefabs, and dashboard. Use this if the destination project already has MAX SDK installed.";
                    break;
                case ExportPreset.Custom:
                    presetDescription = "🛠️ <b>Custom Selection</b>: Manually check and uncheck individual components below.";
                    break;
            }

            EditorGUILayout.Space(2);
            var richStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { richText = true };
            EditorGUILayout.LabelField(presetDescription, richStyle);
            EditorGUILayout.EndVertical();
        }

        private void DrawComponentsList()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📁 Packages & Folders Included in Export:", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            for (int i = 0; i < components.Count; i++)
            {
                var comp = components[i];
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();

                bool prevSelected = comp.IsSelected;
                comp.IsSelected = EditorGUILayout.ToggleLeft(comp.Name, comp.IsSelected, EditorStyles.boldLabel);
                if (comp.IsSelected != prevSelected && currentPreset != ExportPreset.Custom)
                {
                    currentPreset = ExportPreset.Custom;
                }

                if (!comp.Exists)
                {
                    GUILayout.Label("⚠️ Missing", EditorStyles.miniBoldLabel, GUILayout.Width(65));
                }
                else
                {
                    string sizeStr = FormatBytes(comp.TotalSizeBytes);
                    GUILayout.Label($"✅ {comp.FileCount} files ({sizeStr})", EditorStyles.miniLabel, GUILayout.Width(130));
                }

                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Path: {comp.Path}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(comp.Description, EditorStyles.wordWrappedMiniLabel);
                EditorGUI.indentLevel--;

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSummaryBox()
        {
            var selected = components.Where(c => c.IsSelected && c.Exists).ToList();
            int totalFiles = selected.Sum(c => c.FileCount);
            long totalBytes = selected.Sum(c => c.TotalSizeBytes);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📊 Export Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Selected Components: {selected.Count} of {components.Count}");
            EditorGUILayout.LabelField($"Total Assets: {totalFiles} files (~{FormatBytes(totalBytes)})");

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Default Filename:", GUILayout.Width(110));
            customFileName = EditorGUILayout.TextField(customFileName);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.BeginVertical();

            var selected = components.Where(c => c.IsSelected && c.Exists).ToList();
            GUI.enabled = selected.Count > 0;

            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.3f);
            if (GUILayout.Button("📦 Export .unitypackage Now", GUILayout.Height(42)))
            {
                ExecuteExport(selected, customFileName);
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🚀 Open New Project Setup Assistant", GUILayout.Height(26)))
            {
                AdsSetupAssistantWindow.ShowWindow();
            }
            if (GUILayout.Button("Ad Strategy Dashboard", GUILayout.Height(26)))
            {
                AdStrategyDashboardWindow.ShowWindow();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        public static void ExecuteExport(List<PackageComponent> selectedComponents, string targetFileName = "MangoAdsStrategy_FullBundle.unitypackage")
        {
            if (selectedComponents == null || selectedComponents.Count == 0)
            {
                EditorUtility.DisplayDialog("Nothing Selected", "Please select at least one package component to export.", "OK");
                return;
            }

            // Prompt user for destination file path
            string savePath = EditorUtility.SaveFilePanel(
                "Export Ads Strategy Unity Package",
                "",
                targetFileName,
                "unitypackage"
            );

            if (string.IsNullOrEmpty(savePath)) return; // User cancelled

            try
            {
                EditorUtility.DisplayProgressBar("Exporting Ads Package", "Collecting assets...", 0.2f);

                // Collect asset root paths
                string[] exportPaths = selectedComponents.Select(c => c.Path).ToArray();

                EditorUtility.DisplayProgressBar("Exporting Ads Package", "Compiling package archive...", 0.5f);

                // Export unity package
                AssetDatabase.ExportPackage(
                    exportPaths,
                    savePath,
                    ExportPackageOptions.Recurse | ExportPackageOptions.Interactive
                );

                EditorUtility.DisplayProgressBar("Exporting Ads Package", "Generating setup guide...", 0.85f);

                // Also generate a companion instructions file right beside the .unitypackage
                string directory = System.IO.Path.GetDirectoryName(savePath);
                string instructionsPath = System.IO.Path.Combine(directory, "NEW_PROJECT_ADS_SETUP_GUIDE.txt");
                File.WriteAllText(instructionsPath, GenerateSetupGuide(selectedComponents));

                EditorUtility.ClearProgressBar();

                // Notify developer and reveal in Windows Explorer / Mac Finder
                bool openFolder = EditorUtility.DisplayDialog(
                    "📦 Export Complete!",
                    $"Successfully exported Ads Package to:\n{savePath}\n\nIncluded: {selectedComponents.Count} packages.\nA step-by-step setup guide was also created:\n{instructionsPath}\n\nWould you like to open the output folder?",
                    "Open Folder",
                    "Close"
                );

                if (openFolder)
                {
                    EditorUtility.RevealInFinder(savePath);
                }
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Export Failed", $"An error occurred during export:\n{ex.Message}", "OK");
                Debug.LogError($"[AdsPackageExporter] Export error: {ex}");
            }
        }

        private static string GenerateSetupGuide(List<PackageComponent> components)
        {
            bool hasMaxSdk = components.Any(c => c.Path.Contains("MaxSdk"));
            bool hasIap = components.Any(c => c.Path.Contains("IAP"));

            return $@"================================================================================
🥭 MANGO MANGO ADS STRATEGY PACKAGE - NEW PROJECT QUICK SETUP GUIDE
Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
================================================================================

Follow these 5 quick steps to implement the exact same ad strategy in your new project:

--------------------------------------------------------------------------------
STEP 1: INSTALL REQUIRED UNITY PACKAGES (UPM)
--------------------------------------------------------------------------------
Before or right after importing, make sure these UPM packages are installed:
1. Open Window > Package Manager in your new project.
2. Click '+' -> 'Add package by name...'
   - com.unity.remote-config   (Version 4.2.5 or latest compatible)
   - com.unity.ads.ios-support (Required for iOS App Tracking Transparency)
{(hasIap ? "   - com.unity.purchasing      (Required for In-App Purchases)\n" : "")}
💡 TIP: Once you import this package, you can also use our 1-Click Assistant:
        Tools > Mango Ads > 🚀 New Project Setup Assistant

--------------------------------------------------------------------------------
STEP 2: IMPORT THIS .UNITYPACKAGE
--------------------------------------------------------------------------------
1. In your new project, select: Assets > Import Package > Custom Package...
2. Choose this .unitypackage file and click 'Import'.

--------------------------------------------------------------------------------
STEP 3: RUN ANDROID RESOLVER (EDM4U)
--------------------------------------------------------------------------------
{(hasMaxSdk ? @"1. Go to: Assets > External Dependency Manager > Android Resolver > Force Resolve
2. EDM4U will resolve and link all mediation SDKs (Google AdMob, UnityAds, Meta,
   Pangle / ByteDance, Mintegral, Vungle / Liftoff).
3. Ensure in Project Settings > Player > Android:
   - 'Custom Main Gradle Template' is CHECKED
   - 'Custom Gradle Properties Template' is CHECKED
   - 'Custom Settings Gradle Template' is CHECKED" : "MAX SDK not included in this export.")}

--------------------------------------------------------------------------------
STEP 4: SETUP SPLASH / INITIAL SCENE PREFABS
--------------------------------------------------------------------------------
1. Open your game's first scene (e.g. Splash or MainMenu).
2. Drag and drop these prefabs into the scene hierarchy:
   - Assets/Applovin Ad Manager/Prefabs/Necessary/Applovin Ad Manager (Necessary).prefab
   - Assets/Applovin Ad Manager/Prefabs/Necessary/Remote Config Manager (Necessary).prefab
   - Assets/Applovin Ad Manager/Prefabs/Optional/Time Based Ad (Optional).prefab (if using gameplay timer ads)

--------------------------------------------------------------------------------
STEP 5: SETUP UI SCREEN TRACKER (FOR EVENT-BASED ADS)
--------------------------------------------------------------------------------
1. In your Menu scene, attach the 'UIScreenTracker' component to any GameObject.
2. Drag all your UI panel GameObjects (MainMenu, LevelSelect, Store, Settings, etc.)
   into the 'Tracked Screens' list.
3. Done! Screen transitions will now automatically trigger event-based interstitial
   and rewarded ads following your live cloud strategy!

--------------------------------------------------------------------------------
STEP 6: OPEN AD STRATEGY DASHBOARD
--------------------------------------------------------------------------------
Go to: Tools > Mango Ads > Ad Strategy & Remote Config Dashboard
- Verify your MAX SDK Key and Remote Config keys.
- Use the Live Testing Suite to test ads before publishing!

Happy Monetizing! 🥭
================================================================================";
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int i = 0;
            double dblSBytes = bytes;
            while (dblSBytes >= 1024 && i < suffixes.Length - 1)
            {
                dblSBytes /= 1024;
                i++;
            }
            return $"{dblSBytes:0.##} {suffixes[i]}";
        }
    }
}
#endif
