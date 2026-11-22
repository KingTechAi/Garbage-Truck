#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using MangoMango.Advertisement;

namespace MangoMango.Advertisement.Editor
{
    [CustomEditor(typeof(UIScreenTracker))]
    public class UIScreenTrackerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var tracker = (UIScreenTracker)target;

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("📱 Universal UI Screen Tracker", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Drag and drop all screen GameObjects/Panels below. Whenever a screen becomes active, it automatically notifies the Ad Manager for Hybrid/Event ad pacing.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔍 Auto-Detect Panels from Scene Canvases", GUILayout.Height(28)))
            {
                Undo.RecordObject(tracker, "Auto-Detect Scene Panels");
                tracker.AutoDetectScenePanels();
                EditorUtility.SetDirty(tracker);
            }
            if (GUILayout.Button("🔍 Auto-Detect Child Panels", GUILayout.Height(28)))
            {
                Undo.RecordObject(tracker, "Auto-Detect Child Panels");
                tracker.AutoDetectChildPanels();
                EditorUtility.SetDirty(tracker);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            if (GUILayout.Button("🚀 Open Ad Strategy Dashboard", GUILayout.Height(30)))
            {
                AdStrategyDashboardWindow.ShowWindow();
            }
        }
    }
}
#endif
