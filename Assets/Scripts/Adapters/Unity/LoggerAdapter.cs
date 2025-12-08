using UnityEngine;
using ICoreLogger = MyGame.Core.ILogger;

namespace MyGame.Adapters.Unity
{
    public class LoggerAdapter : ICoreLogger
    {
        public void Log(string message) => Debug.Log($"[Core] {message}");
        public void LogWarning(string message) => Debug.LogWarning($"[Core] {message}");
        public void LogError(string message) => Debug.LogError($"[Core] {message}");
    }
}