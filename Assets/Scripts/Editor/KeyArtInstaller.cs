using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    public static class KeyArtInstaller
    {
        private const string DefaultModelPath = "Assets/ThirdParty/Downloaded/UnityAssets/goldkey.blend";
        private const string DefaultRustKeyPrefabPath = "Assets/ThirdParty/Downloaded/Rust Key/Prefabs/rust_key.prefab";
        private const string KeyPrefabPath = "Assets/Prefabs/Key.prefab";

        // Private art repo convention: keep overrides under Downloaded + Resources so runtime can load them via Resources.Load.
        private const string KeyArtOverrideResourcePath = "RoomBuilder2Overrides/KeyArt";
        private const string KeyArtOverridePrefabPath =
            "Assets/ThirdParty/Downloaded/RoomBuilder2Art/Resources/RoomBuilder2Overrides/KeyArt.prefab";

        [MenuItem("Tools/Art/Build Key Art Override (Rust Key, ThirdParty Downloaded)")]
        public static void BuildRustKeyArtOverride()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultRustKeyPrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Rust Key Prefab Missing",
                    $"Cannot load '{DefaultRustKeyPrefabPath}'.\n\nMake sure the pack is present under Assets/ThirdParty/Downloaded/.",
                    "OK");
                return;
            }

            BuildKeyArtOverrideFromModel(prefab, DefaultRustKeyPrefabPath);
        }

        [MenuItem("Tools/Art/Build Key Art Override (From Selected Prefab)")]
        public static void BuildSelectedPrefabAsKeyArtOverride()
        {
            var selected = Selection.activeObject as GameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog(
                    "Select a Prefab",
                    "Select a prefab asset (Project window), then run this command.\n\nExample:\nAssets/ThirdParty/Downloaded/Rust Key/Prefabs/rust_key.prefab",
                    "OK");
                return;
            }

            string selectedPath = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(selectedPath) || !PrefabUtility.IsPartOfPrefabAsset(selected))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Selection",
                    "Selected object is not a prefab asset. Please select a prefab in the Project window.",
                    "OK");
                return;
            }

            BuildKeyArtOverrideFromModel(selected, selectedPath);
        }

        [MenuItem("Tools/Art/Build Key Art Override (Gold Key, ThirdParty Downloaded)")]
        public static void BuildGoldKeyArtOverride()
        {
            var model = LoadGoldKeyModelAsset();
            if (model == null)
            {
                EditorUtility.DisplayDialog(
                    "Gold Key Model Missing",
                    "Could not load a model asset for the gold key.\n\n" +
                    "Expected one of:\n" +
                    "- Assets/ThirdParty/Downloaded/UnityAssets/goldkey.blend (requires Blender installed for Unity to import)\n" +
                    "- Assets/ThirdParty/Downloaded/UnityAssets/goldkey.fbx / goldkey.obj / goldkey.glb\n\n" +
                    "If you only have a .blend and Unity can’t import it, open it in Blender and export FBX to the same folder.",
                    "OK");
                return;
            }

            BuildKeyArtOverrideFromModel(model, AssetDatabase.GetAssetPath(model));
        }

        [MenuItem("Tools/Art/Validate Key Art Override")]
        public static void ValidateKeyArtOverride()
        {
            var prefab = Resources.Load<GameObject>(KeyArtOverrideResourcePath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Key Art Override Missing",
                    "No key art override found.\n\n" +
                    $"Expected a prefab at:\n{KeyArtOverridePrefabPath}\n\n" +
                    "Tip:\n- If you have the private art repo, clone/sync it into Assets/ThirdParty/Downloaded/RoomBuilder2Art/\n" +
                    "- Or run one of the Build Key Art Override tools under Tools/Art/",
                    "OK");
                return;
            }

            EditorUtility.DisplayDialog(
                "Key Art Override OK",
                $"Resources.Load(\"{KeyArtOverrideResourcePath}\") resolved to:\n{AssetDatabase.GetAssetPath(prefab)}",
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
            if (prefab.GetComponentInChildren<Collider>(true) == null) issues.Add("Missing Collider (interaction raycasts need a collider).");
            if (prefab.GetComponentInChildren<Renderer>(true) == null) issues.Add("Missing Renderer (should be visible or highlightable).");

            if (issues.Count == 0)
            {
                EditorUtility.DisplayDialog("Key Prefab OK", "No issues found.", "OK");
                return;
            }

            var msg = "Found:\n- " + string.Join("\n- ", issues);
            EditorUtility.DisplayDialog("Key Prefab Issues", msg, "OK");
        }

        private static GameObject LoadGoldKeyModelAsset()
        {
            // Prefer .blend if present; Unity imports it via Blender.
            var candidates = new[]
            {
                DefaultModelPath,
                "Assets/ThirdParty/Downloaded/UnityAssets/goldkey.fbx",
                "Assets/ThirdParty/Downloaded/UnityAssets/goldkey.obj",
                "Assets/ThirdParty/Downloaded/UnityAssets/goldkey.glb",
                "Assets/ThirdParty/Downloaded/UnityAssets/goldkey.gltf"
            };

            foreach (var path in candidates)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset != null) return asset;
            }

            // Last resort: find any model whose filename starts with goldkey in that folder.
            string[] guids = AssetDatabase.FindAssets("goldkey t:GameObject", new[] { "Assets/ThirdParty/Downloaded/UnityAssets" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset != null) return asset;
            }

            return null;
        }

        private static void BuildKeyArtOverrideFromModel(GameObject model, string modelPathForLog)
        {
            if (model == null)
            {
                EditorUtility.DisplayDialog("Model Missing", "Model is null.", "OK");
                return;
            }

            EnsureAssetFolderExists(Path.GetDirectoryName(KeyArtOverridePrefabPath)?.Replace("\\", "/"));

            var root = new GameObject("KeyArtOverride");
            try
            {
                GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                if (modelInstance == null) modelInstance = UnityEngine.Object.Instantiate(model);

                modelInstance.name = model.name;
                modelInstance.transform.SetParent(root.transform, worldPositionStays: false);
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;

                // If this art pack uses Built-in pipeline materials (Standard/Autodesk), upgrade them to URP Lit
                // so the mesh doesn't render magenta in URP projects.
                UpgradeMaterialsToUrpIfNeeded(modelInstance);

                // Auto-fit the model to roughly match the previous placeholder scale convention:
                // the old sphere mesh has ~1 unit local bounds and the prefab root carries the final scale (e.g., 0.3).
                // We scale the model so its local max dimension becomes ~1 unit.
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

                PrefabUtility.SaveAsPrefabAsset(root, KeyArtOverridePrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            Debug.Log($"[KeyArtInstaller] Built key art override at '{KeyArtOverridePrefabPath}' using model '{modelPathForLog}'.");
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

            // Capture common Standard properties before switching shader.
            Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            Texture normalTex = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            float bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1f;
            Texture metallicTex = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
            float glossiness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;

            mat.shader = urpLit;

            // Map to URP Lit property names.
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
                // Skip disabled renderers (e.g., placeholder root mesh).
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
            // Convert the 8 world corners into root-local space and encapsulate.
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
