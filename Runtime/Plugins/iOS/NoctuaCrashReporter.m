#import "NoctuaCrashReporter.h"
#import <Foundation/Foundation.h>

#if __IPHONE_OS_VERSION_MAX_ALLOWED >= 140000
#import <MetricKit/MetricKit.h>
#endif

// MetricKit delivers diagnostics asynchronously, typically on the NEXT app
// launch after a crash. Registering early (SDK init) is sufficient because
// MetricKit persists undelivered payloads across launches.

static NoctuaNativeCrashCallback gCrashCallback = NULL;

#if __IPHONE_OS_VERSION_MAX_ALLOWED >= 140000
API_AVAILABLE(ios(14.0))
@interface NoctuaCrashReporterSubscriber : NSObject <MXMetricManagerSubscriber>
+ (instancetype)shared;
@end

@implementation NoctuaCrashReporterSubscriber

+ (instancetype)shared {
    static NoctuaCrashReporterSubscriber *instance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        instance = [[NoctuaCrashReporterSubscriber alloc] init];
    });
    return instance;
}

- (void)didReceiveDiagnosticPayloads:(NSArray<MXDiagnosticPayload *> *)payloads
    API_AVAILABLE(ios(14.0)) {
    @try {
        for (MXDiagnosticPayload *payload in payloads) {
            if (![payload respondsToSelector:@selector(crashDiagnostics)]) continue;
            NSArray<MXCrashDiagnostic *> *crashes = payload.crashDiagnostics;
            if (crashes == nil || crashes.count == 0) continue;

            for (MXCrashDiagnostic *crash in crashes) {
                NSDictionary *json = [self serializeCrash:crash payload:payload];
                if (json == nil) continue;

                NSError *err = nil;
                NSData *data = [NSJSONSerialization dataWithJSONObject:json
                                                               options:0
                                                                 error:&err];
                if (data == nil || err != nil) continue;

                NSString *s = [[NSString alloc] initWithData:data
                                                    encoding:NSUTF8StringEncoding];
                if (s == nil) continue;

                if (gCrashCallback != NULL) {
                    const char *utf8 = [s UTF8String];
                    if (utf8 != NULL) gCrashCallback(utf8);
                }
            }
        }
    } @catch (NSException *e) {
        NSLog(@"[NoctuaCrashReporter] exception forwarding diagnostic: %@", e);
    }
}

- (NSDictionary *)serializeCrash:(MXCrashDiagnostic *)crash
                         payload:(MXDiagnosticPayload *)payload
    API_AVAILABLE(ios(14.0)) {
    NSMutableDictionary *out = [NSMutableDictionary dictionary];

    MXMetaData *meta = crash.metaData;
    if (meta != nil) {
        if (meta.applicationBuildVersion != nil)
            out[@"app_version"] = meta.applicationBuildVersion;
        if (meta.osVersion != nil)
            out[@"os_version"] = meta.osVersion;
        if ([meta respondsToSelector:@selector(deviceType)] && meta.deviceType != nil)
            out[@"device_type"] = meta.deviceType;
    }

    NSNumber *signalNum = crash.signal;
    NSNumber *exceptionType = crash.exceptionType;
    NSNumber *exceptionCode = crash.exceptionCode;
    NSString *termination = crash.terminationReason;

    if (signalNum != nil) {
        out[@"signal"] = signalNum;
        out[@"error_type"] = [self signalName:signalNum.intValue];
    } else if (exceptionType != nil) {
        out[@"error_type"] = [NSString stringWithFormat:@"MACH_EXC_%@", exceptionType];
    } else {
        out[@"error_type"] = @"UnknownCrash";
    }

    if (exceptionType != nil) out[@"exception_type"] = exceptionType;
    if (exceptionCode != nil) out[@"exception_code"] = exceptionCode;
    if (termination != nil)   out[@"message"] = termination;

    // Best-effort stack: MXCallStackTree → JSON via -jsonRepresentation (iOS 14+).
    MXCallStackTree *tree = crash.callStackTree;
    if (tree != nil) {
        NSData *stackData = [tree JSONRepresentation];
        if (stackData != nil) {
            NSString *stackStr = [[NSString alloc] initWithData:stackData
                                                       encoding:NSUTF8StringEncoding];
            if (stackStr != nil) {
                // Truncate at 8000 chars — MetricKit stacks can be huge.
                if (stackStr.length > 8000) {
                    stackStr = [stackStr substringToIndex:8000];
                }
                out[@"stack_trace"] = stackStr;
            }
        }
    }

    // Payload time window (when the crash actually occurred).
    NSString *timestampIso = nil;
    NSTimeInterval timestampSec = 0;
    if (payload.timeStampBegin != nil) {
        timestampIso = [self iso8601:payload.timeStampBegin];
        timestampSec = [payload.timeStampBegin timeIntervalSince1970];
        out[@"timestamp_utc"] = timestampIso;
    }

    // Stable ID for dedup across relaunches. MetricKit doesn't expose a UUID,
    // so build a deterministic ID from raw crash fields + payload timestamp.
    // This ID is reproducible across OS versions, unlike NSString.hash which
    // is not guaranteed to be stable.
    out[@"os_report_id"] = [NSString stringWithFormat:@"%@|%@|%@|%.0f|%@",
                            signalNum ?: @"-",
                            exceptionType ?: @"-",
                            exceptionCode ?: @"-",
                            timestampSec,
                            termination ?: @"-"];

    return out;
}

- (NSString *)iso8601:(NSDate *)date {
    static NSISO8601DateFormatter *fmt = nil;
    static dispatch_once_t once;
    dispatch_once(&once, ^{ fmt = [[NSISO8601DateFormatter alloc] init]; });
    return [fmt stringFromDate:date];
}

- (NSString *)signalName:(int)sig {
    switch (sig) {
        case 1:  return @"SIGHUP";
        case 2:  return @"SIGINT";
        case 3:  return @"SIGQUIT";
        case 4:  return @"SIGILL";
        case 5:  return @"SIGTRAP";
        case 6:  return @"SIGABRT";
        case 7:  return @"SIGBUS";      // macOS numbering
        case 8:  return @"SIGFPE";
        case 9:  return @"SIGKILL";
        case 10: return @"SIGBUS";      // iOS numbering
        case 11: return @"SIGSEGV";
        case 13: return @"SIGPIPE";
        case 15: return @"SIGTERM";
        default: return [NSString stringWithFormat:@"SIG_%d", sig];
    }
}

@end
#endif  // __IPHONE_OS_VERSION_MAX_ALLOWED >= 140000

void noctuaStartNativeCrashReporter(NoctuaNativeCrashCallback callback) {
    gCrashCallback = callback;

#if __IPHONE_OS_VERSION_MAX_ALLOWED >= 140000
    if (@available(iOS 14.0, *)) {
        dispatch_async(dispatch_get_main_queue(), ^{
            [[MXMetricManager sharedManager]
                addSubscriber:[NoctuaCrashReporterSubscriber shared]];
        });
    } else {
        NSLog(@"[NoctuaCrashReporter] iOS < 14 — native crash reporting unavailable");
    }
#else
    NSLog(@"[NoctuaCrashReporter] built against iOS SDK < 14 — native crash reporting unavailable");
#endif
}

void noctuaStopNativeCrashReporter(void) {
#if __IPHONE_OS_VERSION_MAX_ALLOWED >= 140000
    if (@available(iOS 14.0, *)) {
        [[MXMetricManager sharedManager]
            removeSubscriber:[NoctuaCrashReporterSubscriber shared]];
    }
#endif
    gCrashCallback = NULL;
}
