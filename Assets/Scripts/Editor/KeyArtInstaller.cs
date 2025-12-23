using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyGame.EditorTools
{
    public static class KeyArtInstaller
    {
        private const string DefaultModelPath = "Assets/ThirdParty/Downloaded/UnityAssets/goldkey.blend";
        private const string DefaultRustKeyPrefabPath = "Assets/ThirdParty/Downloaded/Rust Key/Prefabs/rust_key.prefab";
        private const string KeyPrefabPath = "Assets/Prefabs/Key.prefab";
        private const string ArtRootName = "Art";

        [MenuItem("Tools/Art/Install Rust Key Art (ThirdParty Downloaded)")]
        public static void InstallRustKeyArt()
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

            InstallKeyArtFromModel(prefab, DefaultRustKeyPrefabPath);
        }

        [MenuItem("Tools/Art/Install Selected Prefab As Key Art")]
        public static void InstallSelectedPrefabAsKeyArt()
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

            InstallKeyArtFromModel(selected, selectedPath);
        }

        [MenuItem("Tools/Art/Install Gold Key Art (ThirdParty Downloaded)")]
        public static void InstallGoldKeyArt()
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

            InstallKeyArtFromModel(model, AssetDatabase.GetAssetPath(model));
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

        private static void InstallKeyArtFromModel(GameObject model, string modelPathForLog)
        {
            if (!File.Exists(KeyPrefabPath))
            {
                EditorUtility.DisplayDialog("Key Prefab Missing", $"Cannot find '{KeyPrefabPath}'.", "OK");
                return;
            }

            if (model == null)
            {
                EditorUtility.DisplayDialog("Model Missing", "Model is null.", "OK");
                return;
            }

            string prefabFullPath = Path.GetFullPath(KeyPrefabPath);
            var root = PrefabUtility.LoadPrefabContents(prefabFullPath);
            try
            {
                var artRoot = FindOrCreateChild(root.transform, ArtRootName);
                ClearChildren(artRoot);

                // Instantiate the model as a child under Art.
                GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                if (modelInstance == null) modelInstance = UnityEngine.Object.Instantiate(model);

                modelInstance.name = model.name;
                modelInstance.transform.SetParent(artRoot, worldPositionStays: false);
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;

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

                // Hide the old placeholder sphere mesh on the root, if present.
                var rootRenderer = root.GetComponent<Renderer>();
                if (rootRenderer != null) rootRenderer.enabled = false;

                // Ensure collider exists and fits the visible renderers.
                DisableChildColliders(modelInstance);
                EnsureCollider(root);

                PrefabUtility.SaveAsPrefabAsset(root, KeyPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Debug.Log($"[KeyArtInstaller] Installed key art into '{KeyPrefabPath}' using model '{modelPathForLog}'.");
        }

        private static Transform FindOrCreateChild(Transform parent, string childName)
        {
            var existing = parent.Find(childName);
            if (existing != null) return existing;

            var go = new GameObject(childName);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        private static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(t.GetChild(i).gameObject);
            }
        }

        private static void EnsureCollider(GameObject root)
        {
            // Keep existing collider type if present, but refit bounds.
            var col = root.GetComponent<Collider>();
            if (col == null) col = root.AddComponent<BoxCollider>();

            if (col is BoxCollider box)
            {
                if (TryComputeLocalBounds(root.transform, out var localBounds))
                {
                    box.center = localBounds.center;
                    box.size = localBounds.size;
                }
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
