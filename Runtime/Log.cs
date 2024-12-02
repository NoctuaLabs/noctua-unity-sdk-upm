using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Xml;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using UnityEditor;
using UnityEngine;

namespace com.noctuagames.sdk
{
    public interface ILogger
    {
        void Debug(string message, [CallerMemberName] string caller = "");
        void Info(string message, [CallerMemberName] string caller = "");
        void Warning(string message, [CallerMemberName] string caller = "");
        void Error(string message, [CallerMemberName] string caller = "");
        void Exception(Exception exception, [CallerMemberName] string caller = "");
    }
    
    public class NoctuaLogger : ILogger
    {
        private readonly string _typeName;
        
        public static void Init(GlobalConfig globalConfig)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Sentry(o =>
                {
                    o.Dsn = globalConfig?.Noctua?.SentryDsnUrl ?? "";
                    o.MinimumEventLevel = LogEventLevel.Error;
                })
                .MinimumLevel.Debug()
                .WriteTo.File(Path.Combine(Application.persistentDataPath, $"{Application.productName}-noctua-log.txt"), 
                              rollingInterval: RollingInterval.Day, 
                              fileSizeLimitBytes: 4 * 1024 * 1024, 
                              retainedFileCountLimit: 8, 
                              outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {Message}{NewLine}{Exception}")
#if UNITY_EDITOR
                .WriteTo.Sink(new UnityLogSink())
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
                .WriteTo.Sink(new AndroidLogSink())
#endif
#if UNITY_IOS && !UNITY_EDITOR
                .WriteTo.Sink(new IosLogSink())
#endif
                .CreateLogger();
        }

        public NoctuaLogger(Type type = null)
        {
            if (type == null)
            {
                var stackTrace = new StackTrace();
                var frame = stackTrace.GetFrame(1); // Get the calling method frame
                var method = frame.GetMethod();
                type = method.DeclaringType;
            }
            
            _typeName = type?.Name;
        }

        public void Debug(string message, [CallerMemberName] string memberName = "")
        {
            Log.Debug($"{_typeName}.{memberName}: {message}");
        }

        public void Info(string message, [CallerMemberName] string memberName = "")
        {
            Log.Information($"{_typeName}.{memberName}: {message}");
        }

        public void Warning(string message, [CallerMemberName] string memberName = "")
        {
            Log.Warning($"{_typeName}.{memberName}: {message}");
        }

        public void Error(string message, [CallerMemberName] string memberName = "")
        {
            Log.Error($"{_typeName}.{memberName}: {message}");
        }
        
        public void Exception(Exception exception, [CallerMemberName] string memberName = "")
        {
            Log.Error(exception, $"{_typeName}.{memberName}: {{ExceptionMessage}}", exception.Message);
        }
    }
    
    public class UnityLogSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
            var message = logEvent.RenderMessage();
            var level = logEvent.Level switch
            {
                LogEventLevel.Debug       => LogType.Log,
                LogEventLevel.Information => LogType.Log,
                LogEventLevel.Warning     => LogType.Warning,
                LogEventLevel.Error       => LogType.Error,
                _                         => LogType.Log
            };

            UnityEngine.Debug.unityLogger.Log(level, message);
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    public class AndroidLogSink : ILogEventSink
    {
        [DllImport("log")]
        private static extern int __android_log_write(int prio, string tag, string msg);

        private const int ANDROID_LOG_DEBUG = 3;
        private const int ANDROID_LOG_INFO = 4;
        private const int ANDROID_LOG_WARN = 5;
        private const int ANDROID_LOG_ERROR = 6;

        public void Emit(LogEvent logEvent)
        {
            var message = logEvent.RenderMessage();
            var level = logEvent.Level switch
            {
                LogEventLevel.Debug       => ANDROID_LOG_DEBUG,
                LogEventLevel.Information => ANDROID_LOG_INFO,
                LogEventLevel.Warning     => ANDROID_LOG_WARN,
                LogEventLevel.Error       => ANDROID_LOG_ERROR,
                _                         => ANDROID_LOG_DEBUG
            };

            __android_log_write(level, "NoctuaSDK", message);
        }
    }
#endif
    
#if UNITY_IOS && !UNITY_EDITOR
    public class IosLogSink : ILogEventSink
    {
        [DllImport("__Internal")]
        public static extern IntPtr os_log_create(string subsystem, string category);

        public enum OSLogType : byte
        {
            Default = 0,
            Info = 1,
            Debug = 2,
            Error = 16,
            Fault = 17
        }

        [DllImport("__Internal")]
        public static extern void OsLogWithType(IntPtr log, OSLogType type, string message);    
        
        private readonly IntPtr _log = os_log_create("com.noctuagames.sdk", "NoctuaSDK");


        public void Emit(LogEvent logEvent)
        {
            var message = logEvent.RenderMessage();
            
            var level = logEvent.Level switch
            {
                LogEventLevel.Debug       => OSLogType.Debug,
                LogEventLevel.Information => OSLogType.Info,
                LogEventLevel.Warning     => OSLogType.Default,
                LogEventLevel.Error       => OSLogType.Error,
                _                         => OSLogType.Default
            };
            
            OsLogWithType(_log, level, message);
        }
    }
#endif

    internal class GlobalExceptionLogger : MonoBehaviour
    {
        private readonly ILogger _log = new NoctuaLogger();

        void Awake()
        {
            Application.logMessageReceived += HandleLog;
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
            Application.logMessageReceivedThreaded += HandleLogThreaded;
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
            {
                _log.Error($"{logString}\n{stackTrace}");
            }
        }

        private void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            _log.Exception(ex);
        }

        private void HandleLogThreaded(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
            {
                _log.Error($"{logString}\n{stackTrace}");
            }
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= HandleLog;
            AppDomain.CurrentDomain.UnhandledException -= HandleUnhandledException;
            Application.logMessageReceivedThreaded -= HandleLogThreaded;
        }
    }
}