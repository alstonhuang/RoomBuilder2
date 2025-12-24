using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    public static class KeyArtInstaller
    {
        private const string KeyPrefabPath = "Assets/Prefabs/Key.prefab";
        private const string OverridesFolder =
            "Assets/ThirdParty/Downloaded/RoomBuilder2Art/Resources/RoomBuilder2Overrides";

        [MenuItem("Tools/Art/Build Art Override Prefab...")]
        public static void BuildArtOverridePrefabFromSelection()
        {
            var selected = Selection.activeObject as GameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog(
                    "Select a Prefab",
                    "Select a prefab/model asset (Project window), then run this command.",
                    "OK");
                return;
            }

            string selectedPath = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(selectedPath) || !PrefabUtility.IsPartOfPrefabAsset(selected))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Selection",
                    "Selected object is not a prefab/model asset. Please select a prefab/model asset in the Project window.",
                    "OK");
                return;
            }

            EnsureAssetFolderExists(OverridesFolder);

            string outPath = EditorUtility.SaveFilePanelInProject(
                "Save Art Override Prefab",
                selected.name,
                "prefab",
                "Save a prefab under a Resources folder so runtime can load it via Resources.Load(...)",
                OverridesFolder);

            if (string.IsNullOrEmpty(outPath)) return;

            string resourcePath = TryGetResourcesPath(outPath);
            if (string.IsNullOrEmpty(resourcePath))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Output Path",
                    "Art override prefab must be saved under a 'Resources' folder.\n\n" +
                    $"Recommended:\n{OverridesFolder}",
                    "OK");
                return;
            }

            BuildArtOverrideFromModel(selected, selectedPath, outPath);
            Debug.Log($"[KeyArtInstaller] Built art override '{outPath}' (Resources.Load(\"{resourcePath}\")).");
        }

        [MenuItem("Tools/Art/Validate Selected Art Override Prefab")]
        public static void ValidateSelectedArtOverridePrefab()
        {
            var selected = Selection.activeObject as GameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Select a Prefab", "Select an override prefab in the Project window.", "OK");
                return;
            }

            string path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path) || !PrefabUtility.IsPartOfPrefabAsset(selected))
            {
                EditorUtility.DisplayDialog("Invalid Selection", "Selected object is not a prefab asset.", "OK");
                return;
            }

            string resourcePath = TryGetResourcesPath(path);
            if (string.IsNullOrEmpty(resourcePath))
            {
                EditorUtility.DisplayDialog(
                    "Not Under Resources",
                    "This prefab is not under any 'Resources' folder, so runtime cannot load it via Resources.Load(...).",
                    "OK");
                return;
            }

            var resolved = Resources.Load<GameObject>(resourcePath);
            bool ok = resolved == selected;
            EditorUtility.DisplayDialog(
                ok ? "Override OK" : "Override Warning",
                ok
                    ? $"Resources.Load(\"{resourcePath}\") resolves to:\n{path}"
                    : $"Resources.Load(\"{resourcePath}\") resolves to a different asset.\n\nSelected:\n{path}\nResolved:\n{AssetDatabase.GetAssetPath(resolved)}",
                "OK");
        }

        [MenuItem("Tools/Art/Validate Key Prefab")]
        public static void ValidateKeyPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KeyPrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Key Prefab Missing", $"Cannot find '{KeyPrefabPath}'.", "OK");
                return;
            }

            var issues = new List<string>();

            if (prefab.GetComponent<KeyController>() == null) issues.Add("Missing KeyController component.");
            if (prefab.GetComponent<Interactable>() == null) issues.Add("Missing Interactable component.");
            if (prefab.GetComponent<ArtOverrideLoader>() == null) issues.Add("Missing ArtOverrideLoader component.");
            if (prefab.GetComponentInChildren<Collider>(true) == null)
                issues.Add("Missing Collider (interaction raycasts need a collider).");
            if (prefab.GetComponentInChildren<Renderer>(true) == null)
                issues.Add("Missing Renderer (should be visible or highlightable).");

            if (issues.Count == 0)
            {
                EditorUtility.DisplayDialog("Key Prefab OK", "No issues found.", "OK");
                return;
            }

            var msg = "Found:\n- " + string.Join("\n- ", issues);
            EditorUtility.DisplayDialog("Key Prefab Issues", msg, "OK");
        }

        private static string TryGetResourcesPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            assetPath = assetPath.Replace("\\", "/");
            int idx = assetPath.LastIndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            string rel = assetPath.Substring(idx + "/Resources/".Length);
            if (rel.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                rel = rel.Substring(0, rel.Length - ".prefab".Length);
            }
            return rel;
        }

        private static void BuildArtOverrideFromModel(GameObject model, string modelPathForLog, string outPrefabAssetPath)
        {
            if (model == null)
            {
                EditorUtility.DisplayDialog("Model Missing", "Model is null.", "OK");
                return;
            }

            EnsureAssetFolderExists(Path.GetDirectoryName(outPrefabAssetPath)?.Replace("\\", "/"));

            var root = new GameObject("ArtOverride");
            try
            {
                GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                if (modelInstance == null) modelInstance = UnityEngine.Object.Instantiate(model);

                modelInstance.name = model.name;
                modelInstance.transform.SetParent(root.transform, worldPositionStays: false);
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;

                UpgradeMaterialsToUrpIfNeeded(modelInstance);

                if (TryComputeLocalBounds(modelInstance.transform, out var modelLocalBounds))
                {
                    float maxDim = Mathf.Max(modelLocalBounds.size.x, modelLocalBounds.size.y, modelLocalBounds.size.z);
                    if (maxDim > 0.0001f)
                    {
                        float factor = 1f / maxDim;
                        modelInstance.transform.localScale = modelInstance.transform.localScale * factor;
                    }
                }

                DisableChildColliders(modelInstance);

                PrefabUtility.SaveAsPrefabAsset(root, outPrefabAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            Debug.Log($"[KeyArtInstaller] Built art override at '{outPrefabAssetPath}' using model '{modelPathForLog}'.");
        }

        private static void EnsureAssetFolderExists(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;
            folderPath = folderPath.Replace("\\", "/");
            if (folderPath == "Assets") return;
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
            string name = Path.GetFileName(folderPath);
            EnsureAssetFolderExists(parent);

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void DisableChildColliders(GameObject modelInstance)
        {
            if (modelInstance == null) return;
            foreach (var c in modelInstance.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;
            }
        }

        private static void UpgradeMaterialsToUrpIfNeeded(GameObject modelInstance)
        {
            if (modelInstance == null) return;

            var renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    TryUpgradeMaterialToUrpLit(m);
                }
            }
        }

        private static void TryUpgradeMaterialToUrpLit(Material mat)
        {
            if (mat == null) return;
            if (mat.shader == null) return;

            string shaderName = mat.shader.name ?? string.Empty;
            bool looksBuiltin =
                shaderName.Equals("Standard", StringComparison.OrdinalIgnoreCase) ||
                shaderName.IndexOf("Autodesk", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!looksBuiltin) return;

            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;

            Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            Texture normalTex = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            float bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1f;
            Texture metallicTex = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
            float glossiness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;

            mat.shader = urpLit;

            if (mainTex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

            if (normalTex != null && mat.HasProperty("_BumpMap"))
            {
                EnsureTextureIsNormalMap(normalTex);
                mat.SetTexture("_BumpMap", normalTex);
                if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", bumpScale);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (metallicTex != null && mat.HasProperty("_MetallicGlossMap"))
            {
                mat.SetTexture("_MetallicGlossMap", metallicTex);
                mat.EnableKeyword("_METALLICGLOSSMAP");
            }
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", glossiness);

            EditorUtility.SetDirty(mat);
        }

        private static void EnsureTextureIsNormalMap(Texture tex)
        {
            if (tex == null) return;
            string path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path)) return;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            if (importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }

        private static bool TryComputeLocalBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasAny = false;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (!r.enabled) continue;

                var wb = r.bounds;
                var local = WorldBoundsToLocal(root, wb);
                if (!hasAny)
                {
                    bounds = local;
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(local);
                }
            }

            return hasAny;
        }

        private static Bounds WorldBoundsToLocal(Transform root, Bounds worldBounds)
        {
            Vector3 c = worldBounds.center;
            Vector3 e = worldBounds.extents;
            var corners = new[]
            {
                new Vector3(c.x - e.x, c.y - e.y, c.z - e.z),
                new Vector3(c.x - e.x, c.y - e.y, c.z + e.z),
                new Vector3(c.x - e.x, c.y + e.y, c.z - e.z),
                new Vector3(c.x - e.x, c.y + e.y, c.z + e.z),
                new Vector3(c.x + e.x, c.y - e.y, c.z - e.z),
                new Vector3(c.x + e.x, c.y - e.y, c.z + e.z),
                new Vector3(c.x + e.x, c.y + e.y, c.z - e.z),
                new Vector3(c.x + e.x, c.y + e.y, c.z + e.z),
            };

            var b = new Bounds(root.InverseTransformPoint(corners[0]), Vector3.zero);
            for (int i = 1; i < corners.Length; i++)
            {
                b.Encapsulate(root.InverseTransformPoint(corners[i]));
            }
            return b;
        }
    }
}

