using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    public static class GuidReferenceAudit
    {
        private static readonly Regex GuidRegex = new Regex(@"guid:\s*([0-9a-f]{32})", RegexOptions.Compiled);

        [MenuItem("Tools/Project/Audit Missing GUID References")]
        public static void AuditMissingGuids()
        {
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string assetsRoot = Path.Combine(projectRoot, "Assets");

                var guidToMetaPath = BuildGuidIndex(assetsRoot);
                var missing = FindMissingGuidReferences(assetsRoot, guidToMetaPath);

                string reportPath = WriteReport(projectRoot, missing);

                int missingCount = missing.Sum(kv => kv.Value.Count);
                Debug.Log($"[GuidReferenceAudit] Missing GUID refs: {missingCount} across {missing.Count} files. Report: {reportPath}");

                if (missingCount == 0)
                {
                    EditorUtility.DisplayDialog("GUID Audit", "No missing GUID references found under Assets.", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("GUID Audit", $"Found {missingCount} missing GUID references.\n\nReport:\n{reportPath}", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GuidReferenceAudit] Failed: {ex}");
            }
        }

        private static Dictionary<string, string> BuildGuidIndex(string assetsRoot)
        {
            var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var metaPath in Directory.EnumerateFiles(assetsRoot, "*.meta", SearchOption.AllDirectories))
            {
                try
                {
                    string guid = ReadMetaGuid(metaPath);
                    if (string.IsNullOrEmpty(guid)) continue;
                    if (!index.ContainsKey(guid)) index[guid] = metaPath;
                }
                catch
                {
                    // ignore unreadable meta files
                }
            }

            return index;
        }

        private static string ReadMetaGuid(string metaPath)
        {
            foreach (var line in File.ReadLines(metaPath))
            {
                if (!line.StartsWith("guid:", StringComparison.Ordinal)) continue;
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                return parts.Length >= 2 ? parts[1].Trim() : null;
            }

            return null;
        }

        private static Dictionary<string, HashSet<string>> FindMissingGuidReferences(
            string assetsRoot,
            Dictionary<string, string> guidToMetaPath)
        {
            var missingByFile = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in EnumerateYamlLikeFiles(assetsRoot))
            {
                string text;
                try
                {
                    text = File.ReadAllText(path);
                }
                catch
                {
                    continue;
                }

                // Fast skip.
                if (!text.Contains("guid:", StringComparison.Ordinal)) continue;

                foreach (Match m in GuidRegex.Matches(text))
                {
                    string guid = m.Groups[1].Value;
                    if (IsBuiltinGuid(guid)) continue;
                    if (guidToMetaPath.ContainsKey(guid)) continue;

                    if (!missingByFile.TryGetValue(path, out var set))
                    {
                        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        missingByFile[path] = set;
                    }

                    set.Add(guid);
                }
            }

            return missingByFile;
        }

        private static IEnumerable<string> EnumerateYamlLikeFiles(string assetsRoot)
        {
            // Unity YAML assets we commonly care about.
            string[] exts =
            {
                ".prefab", ".unity", ".asset", ".mat", ".anim", ".controller", ".overrideController",
                ".playable", ".mask", ".shader", ".shadersubgraph", ".subgraph", ".asmdef", ".asmref"
            };

            foreach (var path in Directory.EnumerateFiles(assetsRoot, "*.*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(path);
                if (!exts.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;
                yield return path;
            }
        }

        private static bool IsBuiltinGuid(string guid)
        {
            // "0000..e000.." style guids are built-in Unity resources; still written in YAML but not backed by .meta.
            // We treat all-zero as built-in too.
            if (string.IsNullOrEmpty(guid)) return true;
            if (guid == "00000000000000000000000000000000") return true;
            if (guid.StartsWith("0000000000000000e", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string WriteReport(string projectRoot, Dictionary<string, HashSet<string>> missingByFile)
        {
            string tempDir = Path.Combine(projectRoot, "Temp");
            Directory.CreateDirectory(tempDir);
            string reportPath = Path.Combine(tempDir, "GuidAuditReport.txt");

            using (var w = new StreamWriter(reportPath))
            {
                w.WriteLine($"GUID reference audit - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                w.WriteLine();

                foreach (var kv in missingByFile.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                {
                    w.WriteLine(kv.Key);
                    foreach (var g in kv.Value.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    {
                        w.WriteLine($"  - {g}");
                    }

                    w.WriteLine();
                }
            }

            return reportPath;
        }
    }
}

