using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

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
            Log(LogType.Log, message, caller);
        }
        
        public void Warning(string message, [CallerMemberName] string caller = "")
        {
            Log(LogType.Warning, message, caller);
        }

        public void Error(string message, [CallerMemberName] string caller = "")
        {
            Log(LogType.Error, message, caller);
        }

        private void Log(LogType logType, string message, string caller = "")
        {
            var chunkSize = 800;

            if (message.Length < chunkSize)
            {
                Debug.unityLogger.Log(logType, $"[NoctuaSDK {_typeName}.{caller}]", message);
                
                return;
            }

            for (var i = 0; i < message.Length; i += chunkSize)
            {
                var chunk = message.Substring(i, Math.Min(chunkSize, message.Length - i));
                var totalChunks = (message.Length / chunkSize) + 1;
                var part = (i / chunkSize) + 1;
                
                var prefix = part == 1 ? "[BEGIN]" : "[CONT]";
                var suffix = part == totalChunks ? "[END]" : "[CONT]";
                
                Debug.unityLogger.Log(logType, $"[NoctuaSDK {_typeName}.{caller} {part}/{totalChunks}]", prefix + chunk + suffix);
            }
        }
    }
}