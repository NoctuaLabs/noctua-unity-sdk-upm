using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace com.noctuagames.sdk
{
    internal interface ILogger
    {
        void Log(string message, string caller = "");
        void Warning(string message, string caller = "");
        void Error(string message, string caller = "");
    }
    
    internal class NoctuaUnityDebugLogger : ILogger
    {
        private readonly string _typeName;
        public object Context { get; }

        internal NoctuaUnityDebugLogger()
        {
            Context = new object();
            _typeName = new StackTrace().GetFrame(1).GetMethod().DeclaringType?.Name;
        }
        
        public void Log(string message, [CallerMemberName] string caller = "")
        {
            UnityEngine.Debug.unityLogger.Log(LogType.Log, $"[NoctuaSDK {_typeName}.{caller}]", message);
        }
        
        public void Warning(string message, [CallerMemberName] string caller = "")
        {
            UnityEngine.Debug.unityLogger.Log(LogType.Warning, $"[NoctuaSDK {_typeName}.{caller}]", message);
        }

        public void Error(string message, [CallerMemberName] string caller = "")
        {
            UnityEngine.Debug.unityLogger.Log(LogType.Error, $"[NoctuaSDK {_typeName}.{caller}]", message);
        }
    }
}