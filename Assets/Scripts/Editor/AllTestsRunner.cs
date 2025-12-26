using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// One-click runner for EditMode + PlayMode (+ optional Player) tests with XML report export.
    /// </summary>
    [FilePath("Library/AllTestsRunner.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class AllTestsRunner : ScriptableSingleton<AllTestsRunner>, ICallbacks
    {
        private const string PrefOutputDir = "RoomBuilder2.Tests.OutputDir";
        private const string PrefPlayerTarget = "RoomBuilder2.Tests.PlayerTarget";
        private const string PrefRestoreScenesOnFinish = "RoomBuilder2.Tests.RestoreScenesOnFinish";
        private const string PrefRunInTempScene = "RoomBuilder2.Tests.RunInTemporaryScene";
        private const string DefaultOutputDirName = "TestReports";
        private const string SessionWarnedAssetsOutput = "RoomBuilder2.Tests.WarnedAssetsOutput";
        private const string SessionActiveRun = "RoomBuilder2.Tests.ActiveRun";

        [SerializeField] private List<RunRequestData> _runs = new List<RunRequestData>();
        [SerializeField] private int _runIndex;
        [SerializeField] private bool _isRunning;
        [SerializeField] private bool _resumeRequested;
        [SerializeField] private string _runStamp;
        [SerializeField] private SceneSetupData[] _originalSceneSetup;
        [SerializeField] private bool _forceRestoreSceneSetup;
        [SerializeField] private string _originalActiveScenePath;
        [SerializeField] private bool _finalized;
        [SerializeField] private bool _finishDialogShown;
        [SerializeField] private bool _runNextQueued;
        [SerializeField] private bool _restoreQueued;
        [SerializeField] private List<string> _finishedRunKeys = new List<string>();
        [SerializeField] private bool _awaitingRunFinished;
        [SerializeField] private int _activeRunIndex = -1;

        [SerializeField] private string _lastEditReportPath;
        [SerializeField] private string _lastPlayReportPath;
        [SerializeField] private string _lastPlayerReportPath;
        private TestRunnerApi _api;

        [InitializeOnLoadMethod]
        private static void ResumePendingRunAfterDomainReload()
        {
            EditorApplication.delayCall += () =>
            {
                try
                {
                    if (instance == null) return;

                    // Prevent surprise auto-runs when scripts recompile: only resume if this editor session started a run.
                    if (!SessionState.GetBool(SessionActiveRun, false))
                    {
                        if (instance._isRunning)
                        {
                            instance.CancelInternal("Stale pending run cleared (no active run in this editor session).");
                        }
                        return;
                    }

                    if (!instance._isRunning) return;
                    if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

                    // PlayMode test runs typically trigger domain reload while entering play mode.
                    // Re-attach callbacks in play mode so we still receive RunFinished and can write reports + restore scenes.
                    if (EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        instance.EnsureCallbacksAttachedForOngoingRun();
                        return;
                    }

                    // IMPORTANT: Do not start/advance runs from domain reload callbacks.
                    // Unity Test Framework can trigger multiple reloads (especially for Player runs),
                    // and attempting to "resume" here causes duplicated Execute() calls and extra finish/restore loops.
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AllTestsRunner] Resume failed: {ex.Message}");
                }
            };
        }

        private void EnsureCallbacksAttachedForOngoingRun()
        {
            if (_api != null) return;
            try
            {
                _api = ScriptableObject.CreateInstance<TestRunnerApi>();
                _api.RegisterCallbacks(this);
                Debug.Log("[AllTestsRunner] Attached callbacks for ongoing PlayMode test run.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AllTestsRunner] Failed to attach callbacks during play mode: {ex.Message}");
            }
        }

        private void OnPlayModeStateChangedResume(PlayModeStateChange state)
        {
            if (!_resumeRequested) return;
            if (state != PlayModeStateChange.EnteredEditMode) return;

            _resumeRequested = false;
            Save(true);
            EditorApplication.playModeStateChanged -= OnPlayModeStateChangedResume;
            EditorApplication.delayCall += () =>
            {
                if (!_isRunning) return;
                if (_finalized) return;
                if (_awaitingRunFinished)
                {
                    // If we returned to EditMode while still awaiting results, the run was likely aborted (e.g., user pressed Stop).
                    // Avoid re-entering play mode in a loop; require Unity Test Framework to report an active run before retrying.
                    if (!IsUnityTestFrameworkRunActive())
                    {
                        CancelInternal("Canceled: PlayMode exited while awaiting test results (likely aborted).");
                        return;
                    }

                    QueueRunNext();
                    return;
                }
                if (IsUnityTestFrameworkRunActive())
                {
                    // Often true briefly right after leaving play mode due to test cleanup tasks.
                    // Queue a retry so "Run All" can continue to the next stage (e.g., Player run).
                    QueueRunNext();
                    return;
                }
                Debug.Log("[AllTestsRunner] Resuming pending test run after returning to EditMode...");
                QueueRunNext();
            };
        }

        [MenuItem("Tools/Tests/Run All (Edit + Play) and Export Reports")]
        public static void RunAll()
        {
            instance.StartRun(new[]
            {
                RunRequest.EditMode(),
                RunRequest.PlayModeInEditor()
            });
        }

        [MenuItem("Tools/Tests/Run All (Edit + Play + Player) and Export Reports")]
        public static void RunAllIncludingPlayer()
        {
            instance.StartRun(new[]
            {
                RunRequest.EditMode(),
                RunRequest.PlayModeInEditor(),
                RunRequest.PlayModeOnPlayer(GetPlayerTarget())
            });
        }

        [MenuItem("Tools/Tests/Run EditMode and Export Report")]
        public static void RunEditModeOnly()
        {
            instance.StartRun(new[] { RunRequest.EditMode() });
        }

        [MenuItem("Tools/Tests/Run PlayMode and Export Report")]
        public static void RunPlayModeOnly()
        {
            instance.StartRun(new[] { RunRequest.PlayModeInEditor() });
        }

        [MenuItem("Tools/Tests/Run Player (Active Build Target) and Export Report")]
        public static void RunPlayerOnly()
        {
            instance.StartRun(new[] { RunRequest.PlayModeOnPlayer(GetPlayerTarget()) });
        }

        [MenuItem("Tools/Tests/Set Report Output Folder...")]
        public static void SetOutputFolder()
        {
            string current = GetOutputDir();
            string selected = EditorUtility.OpenFolderPanel("Select test report output folder", current, "");
            if (string.IsNullOrEmpty(selected)) return;

            if (IsUnderAssets(selected))
            {
                string recommended = Path.Combine(GetProjectRoot(), DefaultOutputDirName);
                EditorUtility.DisplayDialog(
                    "Invalid Output Folder",
                    "Do not write reports under 'Assets/'. Unity Test Framework may warn about test-generated files not cleaned up, and the report can be affected by cleanup checks.\n\n" +
                    $"Recommended:\n{recommended}",
                    "OK");
                return;
            }

            EditorPrefs.SetString(PrefOutputDir, selected);
            Debug.Log($"[AllTestsRunner] Report output folder set to: {selected}");
        }

        [MenuItem("Tools/Tests/Reset Report Output Folder To Project TestReports")]
        public static void ResetOutputFolder()
        {
            string recommended = Path.Combine(GetProjectRoot(), DefaultOutputDirName);
            EditorPrefs.SetString(PrefOutputDir, recommended);
            Debug.Log($"[AllTestsRunner] Report output folder reset to: {recommended}");
        }

        [MenuItem("Tools/Tests/Set Player Target To Active Build Target")]
        public static void SetPlayerTargetToActiveBuildTarget()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            EditorPrefs.SetInt(PrefPlayerTarget, (int)target);
            Debug.Log($"[AllTestsRunner] Player test target set to: {target}");
        }

        [MenuItem("Tools/Tests/Open Report Output Folder")]
        public static void OpenOutputFolder()
        {
            string dir = GetOutputDir();
            Directory.CreateDirectory(dir);
            Debug.Log($"[AllTestsRunner] Opening report output folder: {dir}");
#if UNITY_EDITOR_WIN
            try
            {
                // Process.Start with a folder path can sometimes open the parent folder depending on environment/path encoding.
                // Explicitly launch Explorer with the directory path for consistent behavior.
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{dir}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AllTestsRunner] Failed to open output folder '{dir}': {ex.Message}");
            }
#else
            EditorUtility.RevealInFinder(dir);
#endif
        }

        [MenuItem("Tools/Tests/Restore Last Captured Scenes Now")]
        public static void RestoreLastCapturedScenesNow()
        {
            instance.RestoreCapturedScenesNow();
        }

        [MenuItem("Tools/Tests/Open Unity Editor Log")]
        public static void OpenUnityEditorLog()
        {
            string path = GetUnityEditorLogPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogWarning($"[AllTestsRunner] Unity Editor log not found at '{path}'.");
                return;
            }

#if UNITY_EDITOR_WIN
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
#else
            EditorUtility.RevealInFinder(path);
#endif
            Debug.Log($"[AllTestsRunner] Opened Unity Editor log: {path}");
        }

        [MenuItem("Tools/Tests/Copy AllTestsRunner Logs (Last 400 Lines)")]
        public static void CopyRunnerLogsToClipboard()
        {
            try
            {
                string path = GetUnityEditorLogPath();
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    Debug.LogWarning($"[AllTestsRunner] Unity Editor log not found at '{path}'.");
                    return;
                }

                // Unity keeps Editor.log open; read with FileShare.ReadWrite.
                string tail = ReadFileTailTextShared(path, maxBytes: 1024 * 1024 * 2); // 2MB tail
                var filtered = tail
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                    .Where(l => !string.IsNullOrEmpty(l) && l.Contains("[AllTestsRunner]"))
                    .TakeLast(400)
                    .ToArray();

                string text = string.Join("\n", filtered);
                EditorGUIUtility.systemCopyBuffer = text;
                Debug.Log($"[AllTestsRunner] Copied {filtered.Length} AllTestsRunner log lines to clipboard.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AllTestsRunner] Failed to copy logs: {ex.Message}");
            }
        }

        [MenuItem("Tools/Tests/Copy Unity Editor Log Tail (Last 300 Lines)")]
        public static void CopyEditorLogTailToClipboard()
        {
            try
            {
                string path = GetUnityEditorLogPath();
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    Debug.LogWarning($"[AllTestsRunner] Unity Editor log not found at '{path}'.");
                    return;
                }

                string tail = ReadFileTailTextShared(path, maxBytes: 1024 * 1024 * 2);
                var lines = tail
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                    .Where(l => l != null)
                    .TakeLast(300)
                    .ToArray();

                EditorGUIUtility.systemCopyBuffer = string.Join("\n", lines);
                Debug.Log($"[AllTestsRunner] Copied last {lines.Length} Editor.log lines to clipboard.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AllTestsRunner] Failed to copy Editor.log tail: {ex.Message}");
            }
        }

        [MenuItem("Tools/Tests/Run In Temporary Scene (Avoid Modifying Current Scene)")]
        public static void ToggleRunInTemporaryScene()
        {
            bool next = !EditorPrefs.GetBool(PrefRunInTempScene, true);
            EditorPrefs.SetBool(PrefRunInTempScene, next);
            Debug.Log($"[AllTestsRunner] Run tests in temporary scene: {next}");
        }

        [MenuItem("Tools/Tests/Run In Temporary Scene (Avoid Modifying Current Scene)", true)]
        public static bool ToggleRunInTemporarySceneValidate()
        {
            Menu.SetChecked("Tools/Tests/Run In Temporary Scene (Avoid Modifying Current Scene)", EditorPrefs.GetBool(PrefRunInTempScene, true));
            return true;
        }

        [MenuItem("Tools/Tests/Cancel Pending Test Run")]
        public static void CancelPendingRun()
        {
            instance.CancelInternal("Canceled by user.");
        }

        [MenuItem("Tools/Tests/Force Clear Active Run Lock (Stuck Fix)")]
        public static void ForceClearActiveRunLock()
        {
            if (IsUnityTestFrameworkRunActive())
            {
                EditorUtility.DisplayDialog(
                    "Tests Still Running",
                    "Unity Test Framework reports an active run. Please wait for it to finish or stop the test run from the Test Runner window.",
                    "OK");
                return;
            }

            // Clear both our persisted state and session flag.
            SessionState.SetBool(SessionActiveRun, false);
            instance.CancelInternal("Force-cleared active run lock.");
            Debug.LogWarning("[AllTestsRunner] Force-cleared active run lock.");
        }

        [MenuItem("Tools/Tests/Print Active Scene Path")]
        public static void PrintActiveScenePath()
        {
            var s = EditorSceneManager.GetActiveScene();
            Debug.Log($"[AllTestsRunner] Active scene: name='{s.name}', path='{s.path}', isLoaded={s.isLoaded}");
        }

        [MenuItem("Tools/Tests/Restore Scenes From Disk After Run")]
        public static void ToggleRestoreScenesFromDiskAfterRun()
        {
            bool next = !EditorPrefs.GetBool(PrefRestoreScenesOnFinish, false);
            EditorPrefs.SetBool(PrefRestoreScenesOnFinish, next);
            Debug.Log($"[AllTestsRunner] Restore scenes from disk after run: {next}");
        }

        [MenuItem("Tools/Tests/Restore Scenes From Disk After Run", true)]
        public static bool ToggleRestoreScenesFromDiskAfterRunValidate()
        {
            Menu.SetChecked("Tools/Tests/Restore Scenes From Disk After Run", EditorPrefs.GetBool(PrefRestoreScenesOnFinish, false));
            return true;
        }

        private static BuildTarget GetPlayerTarget()
        {
            if (EditorPrefs.HasKey(PrefPlayerTarget))
            {
                return (BuildTarget)EditorPrefs.GetInt(PrefPlayerTarget);
            }
            return EditorUserBuildSettings.activeBuildTarget;
        }

        private static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string GetUnityEditorLogPath()
        {
#if UNITY_EDITOR_WIN
            // Typical path: %LOCALAPPDATA%\Unity\Editor\Editor.log
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Unity", "Editor", "Editor.log");
#elif UNITY_EDITOR_OSX
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Logs", "Unity", "Editor.log");
#else
            // Linux
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".config", "unity3d", "Editor.log");
#endif
        }

        private static string ReadFileTailTextShared(string path, int maxBytes)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return string.Empty;
            if (maxBytes <= 0) maxBytes = 1024 * 1024;

            // On Windows the log can be rotated/truncated; also some processes can hold it with restrictive share flags briefly.
            // Retry a few times with a permissive FileShare mask.
            const int attempts = 5;
            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        long len = fs.Length;
                        long start = Math.Max(0, len - maxBytes);
                        fs.Seek(start, SeekOrigin.Begin);

                        int toRead = (int)(len - start);
                        var buf = new byte[toRead];
                        int read = 0;
                        while (read < toRead)
                        {
                            int n = fs.Read(buf, read, toRead - read);
                            if (n <= 0) break;
                            read += n;
                        }

                        string text = Encoding.UTF8.GetString(buf, 0, read);

                        // If we started mid-line, drop the first partial line for clean output.
                        int firstNewline = text.IndexOf('\n');
                        if (start > 0 && firstNewline >= 0 && firstNewline + 1 < text.Length)
                        {
                            text = text.Substring(firstNewline + 1);
                        }

                        return text;
                    }
                }
                catch (IOException)
                {
                    if (i == attempts - 1) throw;
                    System.Threading.Thread.Sleep(50);
                }
            }

            return string.Empty;
        }

        private static string GetOutputDir()
        {
            string configured = EditorPrefs.GetString(PrefOutputDir, "");
            string fallback = Path.Combine(GetProjectRoot(), DefaultOutputDirName);

            if (string.IsNullOrEmpty(configured)) return fallback;

            if (IsUnderAssets(configured))
            {
                if (!SessionState.GetBool(SessionWarnedAssetsOutput, false))
                {
                    SessionState.SetBool(SessionWarnedAssetsOutput, true);
                    Debug.LogWarning($"[AllTestsRunner] Output folder '{configured}' is under Assets; using '{fallback}' instead to avoid test file cleanup issues.");
                }
                return fallback;
            }

            return configured;
        }

        private static bool IsUnderAssets(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string assets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return full.StartsWith(assets, StringComparison.OrdinalIgnoreCase);
        }

        private void StartRun(IEnumerable<RunRequest> runs)
        {
            if (IsAnyTestRunActive())
            {
                // Sometimes the editor can be left with a stale "active run" lock (domain reload / interrupted run),
                // even though the Unity Test Framework is idle. In that case, clear our lock and proceed.
                if (TryClearStaleActiveRunLock())
                {
                    Debug.LogWarning("[AllTestsRunner] Cleared stale active-run lock; starting a new run.");
                }
                else
                {
                    EditorUtility.DisplayDialog("Tests Running", "A test run is already active. Please wait for it to finish.", "OK");
                    return;
                }
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[AllTestsRunner] Aborted: user canceled saving modified scenes.");
                return;
            }

            bool runInTempScene = EditorPrefs.GetBool(PrefRunInTempScene, true);
            if (runInTempScene && string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path))
            {
                EditorUtility.DisplayDialog(
                    "Cannot Use Temporary Scene",
                    "The active scene is 'Untitled' (not saved), so it cannot be restored after running tests in a temporary scene.\n\n" +
                    "Save your scene first, or disable:\nTools/Tests/Run In Temporary Scene (Avoid Modifying Current Scene)",
                    "OK");
                return;
            }

            _runStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _lastEditReportPath = null;
            _lastPlayReportPath = null;
            _lastPlayerReportPath = null;
            _resumeRequested = false;
            _originalSceneSetup = null;
            _forceRestoreSceneSetup = false;
            _originalActiveScenePath = null;
            _finalized = false;
            _finishDialogShown = false;
            _runNextQueued = false;
            _restoreQueued = false;
            _finishedRunKeys.Clear();
            _awaitingRunFinished = false;
            _activeRunIndex = -1;

            // Defensive: if callbacks were left attached (domain reload edge cases), clear them now.
            try
            {
                if (_api != null) _api.UnregisterCallbacks(this);
            }
            catch
            {
                // ignore
            }
            _api = null;

            // Always capture scene setup if we're about to switch away to an isolated temp scene.
            // This prevents domain-reload / ExecuteAlways side-effects from mutating the user's open scene during test runs.
            if (runInTempScene || EditorPrefs.GetBool(PrefRestoreScenesOnFinish, false))
            {
                _originalSceneSetup = CaptureSceneSetup();
                _forceRestoreSceneSetup = runInTempScene;
                _originalActiveScenePath = EditorSceneManager.GetActiveScene().path;
                if (string.IsNullOrEmpty(_originalActiveScenePath))
                {
                    Debug.LogWarning("[AllTestsRunner] Active scene has no path (Untitled). Restore from disk may not return you to the same scene.");
                }
                Debug.Log($"[AllTestsRunner] Captured scene setup (count={_originalSceneSetup?.Length ?? 0}, active='{_originalActiveScenePath}', tempScene={runInTempScene}).");
            }
            SessionState.SetBool(SessionActiveRun, true);

            _runs = new List<RunRequestData>();
            foreach (var r in runs) _runs.Add(RunRequestData.From(r));
            _runIndex = 0;
            _isRunning = _runs.Count > 0;
            Save(true);

            if (!_isRunning)
            {
                EditorUtility.DisplayDialog("No Tests Selected", "No test runs were specified.", "OK");
                return;
            }

            if (runInTempScene)
            {
                // Run tests in an empty temporary scene so even EditMode runs can't disturb the user's current scene.
                // (Important when Enter Play Mode Options disable scene reload, or when scripts execute in edit mode.)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var active = EditorSceneManager.GetActiveScene();
                Debug.Log($"[AllTestsRunner] Switched to temporary scene (active='{active.path}').");
            }

            Debug.Log($"[AllTestsRunner] Queuing test run start (runs={_runs.Count}, tempScene={runInTempScene}).");

            // Kick off on the next editor tick to avoid edge cases where scene switching blocks immediate execution.
            EditorApplication.delayCall += () =>
            {
                if (!_isRunning) return;
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    Debug.Log("[AllTestsRunner] Delaying start: editor is compiling/updating...");
                    QueueRunNext();
                    return;
                }
                QueueRunNext();
            };
        }

        private static bool IsAnyTestRunActive()
        {
            // TestRunnerApi exposes IsRunActive() as internal in some package versions.
            // Use reflection to keep this tool working across Unity/Test Framework versions.
            if (IsUnityTestFrameworkRunActive()) return true;

            // Fallback: domain reload can null out _api mid-run; also consider our persisted state.
            return SessionState.GetBool(SessionActiveRun, false) || instance._awaitingRunFinished || instance._api != null || instance._isRunning;
        }

        private static bool IsStaleActiveRunLock()
        {
            // Stale if:
            // - Unity Test Framework is idle
            // - we're not currently awaiting a RunFinished
            // - we have no API instance registered
            // - but SessionState still thinks there's an active run
            if (IsUnityTestFrameworkRunActive()) return false;
            if (!SessionState.GetBool(SessionActiveRun, false)) return false;
            if (instance == null) return false;
            if (instance._awaitingRunFinished) return false;
            if (instance._api != null) return false;

            // If Unity is compiling/updating, we can't safely decide; avoid clearing in that case.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return false;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return false;

            return true;
        }

        private static bool TryClearStaleActiveRunLock()
        {
            if (!IsStaleActiveRunLock()) return false;

            try
            {
                instance.CancelInternal("Stale active-run lock cleared.");
            }
            catch
            {
                // Worst case: clear the session flag so the user can start a new run.
                SessionState.SetBool(SessionActiveRun, false);
            }

            return true;
        }

        private static bool IsUnityTestFrameworkRunActive()
        {
            try
            {
                var m = typeof(TestRunnerApi).GetMethod("IsRunActive", BindingFlags.NonPublic | BindingFlags.Static);
                if (m != null && m.ReturnType == typeof(bool))
                {
                    return (bool)m.Invoke(null, null);
                }
            }
            catch
            {
                // ignore and fall back
            }

            return false;
        }

        private void RunNext()
        {
            if (_finalized) return;

            // Prevent duplicate Execute() calls if multiple delayCall / domain-reload resumes fire.
            if (_awaitingRunFinished || IsUnityTestFrameworkRunActive())
            {
                // Unity Test Framework can still be "active" during IPostBuildCleanup; keep polling until idle,
                // otherwise we can get stuck in the temporary scene without finalizing/restoring.
                QueueRunNext();
                return;
            }

            if (!_isRunning || _runs == null || _runIndex >= _runs.Count)
            {
                _isRunning = false;
                _resumeRequested = false;
                _finalized = true;

                // Best-effort restore of user scenes at the very end (avoid interfering with player launcher).
                RestoreUserSceneSetupIfEnabled();
                _originalSceneSetup = null;
                SessionState.SetBool(SessionActiveRun, false);
                Save(true);

                // Safety: if we're still in an empty/untitled scene after restore, reopen the last saved active scene.
                var active = EditorSceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(active.path) && !string.IsNullOrEmpty(_originalActiveScenePath))
                {
                    try
                    {
                        EditorSceneManager.OpenScene(_originalActiveScenePath, OpenSceneMode.Single);
                        Debug.Log($"[AllTestsRunner] Forced reopen of original active scene: '{_originalActiveScenePath}'");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[AllTestsRunner] Failed to reopen original active scene '{_originalActiveScenePath}': {ex.Message}");
                    }
                }

                if (!_finishDialogShown)
                {
                    _finishDialogShown = true;
                    Save(true);
                    string summary = BuildSummaryMessage();
                    EditorUtility.DisplayDialog("Test Run Finished", summary, "OK");
                }
                return;
            }

            var run = _runs[_runIndex];

            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _api.RegisterCallbacks(this);
            _awaitingRunFinished = true;
            _activeRunIndex = _runIndex;
            Save(true);

            var filter = new Filter
            {
                testMode = run.testMode,
                targetPlatform = run.hasTargetPlatform ? run.targetPlatform : (BuildTarget?)null
            };
            var settings = new ExecutionSettings
            {
                filters = new[] { filter }
            };

            string targetStr = run.hasTargetPlatform ? $" target={run.targetPlatform}" : "";
            Debug.Log($"[AllTestsRunner] Starting {run.label} tests...{targetStr}");
            try
            {
                _api.Execute(settings);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AllTestsRunner] Failed to start {run.label} tests: {ex.Message}");
                CancelInternal($"Failed to start tests: {run.label}");
            }
        }

        public void RunStarted(ITestAdaptor testsToRun)
        {
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            if (_finalized) return;
            if (!_awaitingRunFinished) return;
            if (_activeRunIndex != _runIndex) return;
            if (_runs == null || _runIndex < 0 || _runIndex >= _runs.Count) return;

            var run = _runs[_runIndex];
            string finishedKey = $"{_runStamp}:{_runIndex}:{run.label}";
            if (_finishedRunKeys.Contains(finishedKey))
            {
                Debug.LogWarning($"[AllTestsRunner] Duplicate RunFinished ignored for {run.label} (key={finishedKey}).");
                return;
            }
            _finishedRunKeys.Add(finishedKey);
            _awaitingRunFinished = false;

            try
            {
                string reportPath = "(not written)";
                try
                {
                    string suffix = run.reportSuffix;
                    if (string.IsNullOrWhiteSpace(suffix))
                    {
                        suffix = !string.IsNullOrWhiteSpace(run.label)
                            ? run.label.ToLowerInvariant().Replace("(", "_").Replace(")", "").Replace(" ", "")
                            : run.kind.ToString().ToLowerInvariant();
                    }
                    reportPath = WriteReport(result, suffix);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AllTestsRunner] Failed to write report for {run.label}: {ex.Message}");
                }

                if (run.kind == RunKind.EditMode) _lastEditReportPath = reportPath;
                if (run.kind == RunKind.PlayModeInEditor) _lastPlayReportPath = reportPath;
                if (run.kind == RunKind.PlayModeOnPlayer) _lastPlayerReportPath = reportPath;

                Debug.Log($"[AllTestsRunner] {run.label} finished: {result.TestStatus} ({result.PassCount} passed, {result.FailCount} failed). Report: {reportPath}");

            }
            finally
            {
                try
                {
                    if (_api != null) _api.UnregisterCallbacks(this);
                }
                catch
                {
                    // ignore
                }
                _api = null;
                _activeRunIndex = -1;
            }

            _runIndex++;
            Save(true);

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _resumeRequested = true;
                EditorApplication.playModeStateChanged -= OnPlayModeStateChangedResume;
                EditorApplication.playModeStateChanged += OnPlayModeStateChangedResume;
                Save(true);
                return;
            }

            QueueRunNext();
        }

        private void QueueRunNext()
        {
            if (_finalized) return;
            if (_runNextQueued) return;
            _runNextQueued = true;
            Save(true);

            EditorApplication.delayCall += () =>
            {
                _runNextQueued = false;
                Save(true);
                RunNext();
            };
        }

        private string BuildSummaryMessage()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Reports exported:");
            if (!string.IsNullOrEmpty(_lastEditReportPath)) sb.AppendLine($"- EditMode: {_lastEditReportPath}");
            if (!string.IsNullOrEmpty(_lastPlayReportPath)) sb.AppendLine($"- PlayMode: {_lastPlayReportPath}");
            if (!string.IsNullOrEmpty(_lastPlayerReportPath)) sb.AppendLine($"- Player: {_lastPlayerReportPath}");
            if (string.IsNullOrEmpty(_lastEditReportPath) &&
                string.IsNullOrEmpty(_lastPlayReportPath) &&
                string.IsNullOrEmpty(_lastPlayerReportPath))
            {
                sb.AppendLine("- (none)");
            }
            return sb.ToString();
        }

        private string WriteReport(ITestResultAdaptor result, string suffix)
        {
            string outputDir = GetOutputDir();
            Directory.CreateDirectory(outputDir);

            string fileName = $"TestResults_{_runStamp}_{suffix}.xml";
            string absPath = Path.Combine(outputDir, fileName);
            string absNormalized = Path.GetFullPath(absPath);

            string xml = result != null ? result.ToXml().OuterXml : "<test-run result=\"Failed\"><failure><message>Null test result</message></failure></test-run>";
            if (!xml.StartsWith("<?xml", StringComparison.Ordinal))
            {
                xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + xml;
            }

            File.WriteAllText(absNormalized, xml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (!File.Exists(absNormalized))
            {
                Debug.LogError($"[AllTestsRunner] Report write did not produce a file: {absNormalized}");
            }
            return absNormalized;
        }

        private static SceneSetupData[] CaptureSceneSetup()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var data = new SceneSetupData[setup.Length];
            for (int i = 0; i < setup.Length; i++)
            {
                data[i] = new SceneSetupData
                {
                    path = setup[i].path,
                    isLoaded = setup[i].isLoaded,
                    isActive = setup[i].isActive
                };
            }
            return data;
        }

        private void RestoreUserSceneSetupIfEnabled()
        {
            bool shouldRestore = _forceRestoreSceneSetup || EditorPrefs.GetBool(PrefRestoreScenesOnFinish, false);
            if (!shouldRestore) return;
            if ((_originalSceneSetup == null || _originalSceneSetup.Length == 0) && string.IsNullOrEmpty(_originalActiveScenePath))
            {
                Debug.LogWarning("[AllTestsRunner] No captured scenes to restore.");
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // Don't touch scenes during play mode; restore after returning to EditMode (queue once).
                if (_restoreQueued) return;
                _restoreQueued = true;
                EditorApplication.playModeStateChanged -= OnPlayModeStateChangedRestore;
                EditorApplication.playModeStateChanged += OnPlayModeStateChangedRestore;
                Save(true);
                return;
            }

            try
            {
                // Force reload from disk to avoid play-mode changes persisting when "Reload Scene" is disabled.
                Debug.Log($"[AllTestsRunner] Restoring scenes from disk (fallbackActive='{_originalActiveScenePath}', count={_originalSceneSetup?.Length ?? 0})...");
                ReloadSceneSetupFromDisk(_originalSceneSetup, _originalActiveScenePath);
                var active = EditorSceneManager.GetActiveScene();
                Debug.Log($"[AllTestsRunner] Restored user scene setup (active='{active.path}').");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AllTestsRunner] Failed to restore scene setup: {ex.Message}");
            }
            finally
            {
                _restoreQueued = false;
                EditorApplication.playModeStateChanged -= OnPlayModeStateChangedRestore;
            }
        }

        private void OnPlayModeStateChangedRestore(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChangedRestore;
            _restoreQueued = false;
            EditorApplication.delayCall += RestoreUserSceneSetupIfEnabled;
        }

        private void RestoreCapturedScenesNow()
        {
            if ((_originalSceneSetup == null || _originalSceneSetup.Length == 0) && string.IsNullOrEmpty(_originalActiveScenePath))
            {
                Debug.LogWarning("[AllTestsRunner] No captured scene setup to restore.");
                return;
            }

            // Force restore even if the user disabled the preference; this is an explicit "undo test isolation" action.
            bool oldForce = _forceRestoreSceneSetup;
            try
            {
                _forceRestoreSceneSetup = true;
                RestoreUserSceneSetupIfEnabled();
            }
            finally
            {
                _forceRestoreSceneSetup = oldForce;
                Save(true);
            }
        }

        private void CancelInternal(string reason)
        {
            _isRunning = false;
            _resumeRequested = false;
            _runs = new List<RunRequestData>();
            _runIndex = 0;
            _runNextQueued = false;
            _finishDialogShown = false;

            try
            {
                if (_api != null) _api.UnregisterCallbacks(this);
            }
            catch
            {
                // ignore
            }
            _api = null;

            try
            {
                RestoreUserSceneSetupIfEnabled();
            }
            catch
            {
                // ignore
            }
            _originalSceneSetup = null;
            _forceRestoreSceneSetup = false;
            _originalActiveScenePath = null;
            _finalized = true;
            _restoreQueued = false;
            _finishedRunKeys.Clear();
            _awaitingRunFinished = false;
            _activeRunIndex = -1;

            SessionState.SetBool(SessionActiveRun, false);
            Save(true);
            Debug.Log($"[AllTestsRunner] {reason}");
        }

        private static void ReloadSceneSetupFromDisk(SceneSetupData[] data, string fallbackActiveScenePath)
        {
            if ((data == null || data.Length == 0) && string.IsNullOrEmpty(fallbackActiveScenePath)) return;

            // Filter out unsaved/untitled scenes (empty path).
            var scenes = new List<SceneSetupData>();
            if (data != null)
            {
                foreach (var s in data)
                {
                    if (string.IsNullOrEmpty(s.path)) continue;
                    scenes.Add(s);
                }
            }

            if (scenes.Count == 0)
            {
                // Nothing saved in the setup; try reopening the last active saved scene path.
                if (!string.IsNullOrEmpty(fallbackActiveScenePath))
                {
                    EditorSceneManager.OpenScene(fallbackActiveScenePath, OpenSceneMode.Single);
                }
                return;
            }

            // Open the active scene first (Single), then load the rest Additive.
            int activeIndex = scenes.FindIndex(s => s.isActive);
            if (activeIndex < 0) activeIndex = 0;

            var first = scenes[activeIndex];
            var activeScene = EditorSceneManager.OpenScene(first.path, OpenSceneMode.Single);

            // Open remaining loaded scenes.
            for (int i = 0; i < scenes.Count; i++)
            {
                if (i == activeIndex) continue;
                if (!scenes[i].isLoaded) continue;
                EditorSceneManager.OpenScene(scenes[i].path, OpenSceneMode.Additive);
            }

            // Restore active scene.
            if (activeScene.IsValid())
            {
                EditorSceneManager.SetActiveScene(activeScene);
            }

            // Prefer returning to the previously-active scene if it was captured.
            if (!string.IsNullOrEmpty(fallbackActiveScenePath))
            {
                try
                {
                    var desired = EditorSceneManager.GetSceneByPath(fallbackActiveScenePath);
                    if (desired.IsValid() && desired.isLoaded)
                    {
                        EditorSceneManager.SetActiveScene(desired);
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        private enum RunKind
        {
            EditMode,
            PlayModeInEditor,
            PlayModeOnPlayer
        }

        private struct RunRequest
        {
            public RunKind kind;
            public TestMode testMode;
            public BuildTarget? targetPlatform;
            public string label;
            public string reportSuffix;

            public static RunRequest EditMode()
            {
                return new RunRequest
                {
                    kind = RunKind.EditMode,
                    testMode = TestMode.EditMode,
                    targetPlatform = null,
                    label = "EditMode",
                    reportSuffix = "editmode"
                };
            }

            public static RunRequest PlayModeInEditor()
            {
                return new RunRequest
                {
                    kind = RunKind.PlayModeInEditor,
                    testMode = TestMode.PlayMode,
                    targetPlatform = null,
                    label = "PlayMode",
                    reportSuffix = "playmode"
                };
            }

            public static RunRequest PlayModeOnPlayer(BuildTarget target)
            {
                return new RunRequest
                {
                    kind = RunKind.PlayModeOnPlayer,
                    testMode = TestMode.PlayMode,
                    targetPlatform = target,
                    label = $"Player({target})",
                    reportSuffix = $"player_{target.ToString().ToLowerInvariant()}"
                };
            }
        }

        [Serializable]
        private struct RunRequestData
        {
            public RunKind kind;
            public TestMode testMode;
            public bool hasTargetPlatform;
            public BuildTarget targetPlatform;
            public string label;
            public string reportSuffix;

            public static RunRequestData From(RunRequest r)
            {
                return new RunRequestData
                {
                    kind = r.kind,
                    testMode = r.testMode,
                    hasTargetPlatform = r.targetPlatform.HasValue,
                    targetPlatform = r.targetPlatform.GetValueOrDefault(),
                    label = r.label,
                    reportSuffix = r.reportSuffix
                };
            }
        }

        [Serializable]
        private struct SceneSetupData
        {
            public string path;
            public bool isLoaded;
            public bool isActive;
        }

    }
}
