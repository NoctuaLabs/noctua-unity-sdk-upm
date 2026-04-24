#ifndef NoctuaCrashReporter_h
#define NoctuaCrashReporter_h

#ifdef __cplusplus
extern "C" {
#endif

// Callback delivered once per MXCrashDiagnostic. `jsonPayload` is a UTF-8 JSON
// string owned by the callee — copy before returning. Fields:
//   {
//     "error_type":   "SIGSEGV" | "SIGABRT" | ...,
//     "message":      human-readable termination reason,
//     "stack_trace":  call-stack summary (best effort),
//     "os_report_id": MXCrashDiagnostic.metaData.virtualMemoryRegionInfo hash or UUID,
//     "app_version":  MXMetaData.applicationBuildVersion,
//     "os_version":   MXMetaData.osVersion,
//     "signal":       POSIX signal number,
//     "exception_type": Mach exception type,
//     "exception_code": Mach exception code
//   }
typedef void (*NoctuaNativeCrashCallback)(const char* jsonPayload);

// Subscribe to MetricKit diagnostic payloads. Must be called after UIKit is up.
// iOS 14+. On earlier OS this is a no-op. The callback may fire multiple times
// (one per MXCrashDiagnostic in a single delivery).
void noctuaStartNativeCrashReporter(NoctuaNativeCrashCallback callback);

// Unsubscribe. Safe to call even if never subscribed.
void noctuaStopNativeCrashReporter(void);

#ifdef __cplusplus
}
#endif

#endif /* NoctuaCrashReporter_h */
