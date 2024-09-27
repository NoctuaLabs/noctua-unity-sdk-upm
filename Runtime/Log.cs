using System.Diagnostics;
using UnityEngine;

namespace com.noctuagames.sdk
{
    internal interface ILogger
    {
        void Log(string message);
        void Warning(string message);
        void Error(string message);
    }
    
    internal class NoctuaUnityDebugLogger : ILogger
    {
        private readonly string _tag;
        private readonly object _context;
        
        internal NoctuaUnityDebugLogger()
        {
            _context = new object();
            _tag = $"[NoctuaSDK {new StackTrace().GetFrame(1).GetMethod().DeclaringType?.Name}]";
        }
        
        public void Log(string message)
        {
            UnityEngine.Debug.unityLogger.Log(LogType.Log, _tag, message);
        }

        public void Warning(string message)
        {
            UnityEngine.Debug.unityLogger.Log(LogType.Warning, _tag, message);
        }

        public void Error(string message)
        {
            UnityEngine.Debug.unityLogger.Log(LogType.Error, _tag, message);
        }
    }
}