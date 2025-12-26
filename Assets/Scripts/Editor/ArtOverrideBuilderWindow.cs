using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    public sealed class ArtOverrideBuilderWindow : EditorWindow
    {
        private GameObject _sourceAsset;
        private string _overrideName = "KeyArt";

        [MenuItem("Tools/Art/Art Override Builder...")]
        public static void Open()
        {
            var w = GetWindow<ArtOverrideBuilderWindow>("Art Override Builder");
            w.minSize = new Vector2(520, 220);
            w.Show();
        }

        public static void OpenWithSelection()
        {
            Open();
            var w = GetWindow<ArtOverrideBuilderWindow>();
            var selected = Selection.activeObject as GameObject;
            if (selected != null && PrefabUtility.IsPartOfPrefabAsset(selected))
            {
                w._sourceAsset = selected;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Build Art Override Prefab", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    "This builds a prefab under a Resources folder so runtime can load it via Resources.Load(...).");
                EditorGUILayout.LabelField(
                    $"Default output root: {KeyArtInstaller.OverridesFolderPath}");
            }

            EditorGUILayout.Space(6);

            _sourceAsset = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Source Prefab/Model", "Select a prefab/model asset from the Project window."),
                _sourceAsset,
                typeof(GameObject),
                allowSceneObjects: false);

            _overrideName = EditorGUILayout.TextField(
                new GUIContent(
                    "Override Name",
                    "Relative to RoomBuilder2Overrides/. Example: KeyArt => Resources.Load(\"RoomBuilder2Overrides/KeyArt\")"),
                _overrideName);

            _overrideName = (_overrideName ?? string.Empty).Replace("\\", "/").Trim();

            string resourcePath = string.IsNullOrEmpty(_overrideName)
                ? string.Empty
                : $"RoomBuilder2Overrides/{_overrideName}";

            string outFolder = KeyArtInstaller.OverridesFolderPath.Replace("\\", "/");
            string outPath = string.IsNullOrEmpty(_overrideName)
                ? string.Empty
                : $"{outFolder}/{_overrideName}.prefab";

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Resources.Load Path", resourcePath);
                EditorGUILayout.TextField("Output Prefab Path", outPath);
            }

            EditorGUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Build", GUILayout.Width(140), GUILayout.Height(28)))
                {
                    Build(resourcePath, outPath);
                }
            }
        }

        private void Build(string resourcePath, string outPrefabAssetPath)
        {
            if (_sourceAsset == null)
            {
                EditorUtility.DisplayDialog("Missing Source", "Pick a prefab/model asset first.", "OK");
                return;
            }

            if (!PrefabUtility.IsPartOfPrefabAsset(_sourceAsset))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Source",
                    "Source must be a prefab/model asset (Project window), not a scene instance.",
                    "OK");
                return;
            }

            if (_sourceAsset.GetComponentInChildren<ArtOverrideLoader>(true) != null)
            {
                EditorUtility.DisplayDialog(
                    "Invalid Source",
                    "Selected prefab contains ArtOverrideLoader.\n\n" +
                    "For art overrides, select a model/art prefab (e.g., a mesh prefab) instead of a gameplay prefab.",
                    "OK");
                return;
            }

            if (string.IsNullOrEmpty(_overrideName))
            {
                EditorUtility.DisplayDialog("Missing Override Name", "Enter an override name (e.g., KeyArt).", "OK");
                return;
            }

            if (string.IsNullOrEmpty(resourcePath) || string.IsNullOrEmpty(outPrefabAssetPath))
            {
                EditorUtility.DisplayDialog("Invalid Settings", "Computed paths are empty.", "OK");
                return;
            }

            if (!outPrefabAssetPath.Replace("\\", "/").Contains("/Resources/", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Output",
                    "Output prefab must be under a 'Resources' folder so runtime can load it.",
                    "OK");
                return;
            }

            string outDir = Path.GetDirectoryName(outPrefabAssetPath)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(outDir))
            {
                KeyArtInstaller.EnsureAssetFolderExistsPublic(outDir);
            }

            string sourcePath = AssetDatabase.GetAssetPath(_sourceAsset);
            KeyArtInstaller.BuildArtOverrideFromModelPublic(_sourceAsset, sourcePath, outPrefabAssetPath);

            var built = AssetDatabase.LoadAssetAtPath<GameObject>(outPrefabAssetPath);
            if (built != null)
            {
                EditorGUIUtility.PingObject(built);
                Selection.activeObject = built;
            }
        }
    }
}
