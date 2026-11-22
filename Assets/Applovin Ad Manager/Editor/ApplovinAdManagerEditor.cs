#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using MangoMango.Advertisement;

namespace MangoMango.Advertisement.Editor
{
    [CustomEditor(typeof(ApplovinAdManager))]
    public class ApplovinAdManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("🥭 AppLovin Ad Manager", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Use the dedicated Dashboard for testing, strategy configuration, and scene setup.", MessageType.Info);
            
            if (GUILayout.Button("🚀 Open Ad Strategy Dashboard", GUILayout.Height(30)))
            {
                AdStrategyDashboardWindow.ShowWindow();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📦 Export Ads Package", GUILayout.Height(26)))
            {
                AdsPackageExporterWindow.ShowWindow();
            }
            if (GUILayout.Button("❓ Setup Assistant", GUILayout.Height(26)))
            {
                AdsSetupAssistantWindow.ShowWindow();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            DrawDefaultInspector();
        }
    }
}
#endif
