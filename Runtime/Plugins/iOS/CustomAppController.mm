// Noctua iOS AppController subclass — wires FirebaseCore + FirebaseMessaging
// and bridges the APNs device token into FIRMessaging so FCM registration
// tokens mint correctly under FirebaseAppDelegateProxyEnabled = NO.
//
// ─── Opt-out for games that ship their own AppController ────────────────
// Games that already subclass UnityAppController (for example to integrate
// Unity Mobile Notifications, third-party SDKs, or bespoke launch logic)
// can skip this file entirely by defining the following preprocessor macro
// in their Unity iOS build settings (Player → Other → Scripting Define
// Symbols, or Xcode Build Settings → Preprocessor Macros):
//
//     NOCTUA_DISABLE_CUSTOM_APP_CONTROLLER=1
//
// When the macro is set, this translation unit compiles to a no-op and
// Unity's IMPL_APP_CONTROLLER_SUBCLASS picks whatever subclass the game
// supplies. No link errors, no duplicate symbols.
//
// ─── Firebase / APNs handshake ──────────────────────────────────────────
// FirebaseAppDelegateProxyEnabled is set to NO in Noctua's shipped
// GoogleService-Info wiring, so the APNs device token must be forwarded
// manually to [FIRMessaging messaging].APNSToken. Previously the SDK set
// the FIRMessaging delegate BEFORE calling [super didFinishLaunching...]
// and called registerForRemoteNotifications synchronously (before the
// user granted permission) — both are races. This file corrects both.

// ─── C-bridge symbols are ALWAYS compiled ──────────────────────────────
// IosPlugin.cs P/Invokes the three noctuaSet*Callback entry points below
// regardless of whether the game opts out. If we guarded them with
// NOCTUA_DISABLE_CUSTOM_APP_CONTROLLER the opt-out path would strip the
// symbols and cause a linker / dyld failure the moment Unity tries to
// resolve them. The setters are harmless when the opt-out is active:
// the callback pointers remain NULL, no ObjC delegate code runs, and
// any buffered payload simply never arrives (as expected — the game
// has taken responsibility for its own push wiring).
#import <Foundation/Foundation.h>

typedef void (*NoctuaPushStringCallback)(const char* json);
static NoctuaPushStringCallback _noctuaOnRemoteNotificationReceived = NULL;
static NoctuaPushStringCallback _noctuaOnNotificationTapped         = NULL;
static NoctuaPushStringCallback _noctuaOnFcmTokenRefresh            = NULL;

static NSString *_bufferedTappedJson   = nil;
static NSString *_bufferedReceivedJson = nil;
static NSString *_bufferedFcmToken     = nil;

// This file is Objective-C++ (.mm) — without `extern "C"` the three
// setters below get C++ name-mangled, so IL2CPP's `DllImport("__Internal")`
// P/Invokes resolve to "undefined symbol _noctuaSet…Callback" at link time.
// Keep this wrapper whenever you add a new C-bridge symbol here.
extern "C" {

void noctuaSetRemoteNotificationCallback(NoctuaPushStringCallback cb) {
    _noctuaOnRemoteNotificationReceived = cb;
    if (cb != NULL && _bufferedReceivedJson != nil) {
        cb([_bufferedReceivedJson UTF8String]);
        _bufferedReceivedJson = nil;
    }
}
void noctuaSetNotificationTappedCallback(NoctuaPushStringCallback cb) {
    _noctuaOnNotificationTapped = cb;
    if (cb != NULL && _bufferedTappedJson != nil) {
        cb([_bufferedTappedJson UTF8String]);
        _bufferedTappedJson = nil;
    }
}
void noctuaSetFcmTokenRefreshCallback(NoctuaPushStringCallback cb) {
    _noctuaOnFcmTokenRefresh = cb;
    if (cb != NULL && _bufferedFcmToken != nil) {
        cb([_bufferedFcmToken UTF8String]);
        _bufferedFcmToken = nil;
    }
}

} // extern "C"

static NSString* NoctuaPayloadToJson(NSDictionary *userInfo) {
    if (userInfo == nil) return @"{}";
    NSError *err = nil;
    NSData *data = [NSJSONSerialization dataWithJSONObject:userInfo options:0 error:&err];
    if (err != nil || data == nil) {
        NSLog(@"[Noctua][CustomAppController] Failed to serialize push payload to JSON: %@", err);
        return @"{}";
    }
    return [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
}

#ifndef NOCTUA_DISABLE_CUSTOM_APP_CONTROLLER

#import "UnityAppController.h"
#import "UserNotifications/UserNotifications.h"
#import "FirebaseCore.h"
#import "FirebaseMessaging.h"
#import <objc/runtime.h>

// Parent class selection:
// - Default: UnityAppController (universally available — safe baseline).
// - When com.unity.mobile.notifications is installed, Noctua's Unity Editor
//   BuildPreprocessor auto-injects NOCTUA_USE_LOCAL_NOTIFICATION_PARENT=1 so
//   CustomAppController inherits from LocalNotificationAppController instead.
//   That keeps both Noctua's FCM wiring AND Unity's local-notification
//   delivery running via standard ObjC super dispatch — no sibling
//   IMPL_APP_CONTROLLER_SUBCLASS conflict.
#if NOCTUA_USE_LOCAL_NOTIFICATION_PARENT
@interface LocalNotificationAppController : UnityAppController
@end
#define NOCTUA_APP_CONTROLLER_PARENT LocalNotificationAppController
#else
#define NOCTUA_APP_CONTROLLER_PARENT UnityAppController
#endif

@interface CustomAppController : NOCTUA_APP_CONTROLLER_PARENT <UNUserNotificationCenterDelegate, FIRMessagingDelegate>
@end

// C function pointers set from Unity (IosPlugin.cs) so ObjC delegate callbacks can
// forward to managed code without linking against Unity internals. Payloads are
// marshalled as JSON strings because NSDictionary → Unity marshalling via P/Invoke
// is fragile; Unity deserialises with Newtonsoft.Json on the managed side.
@implementation CustomAppController

NSString *const kGCMMessageIDKey = @"gcm.message_id";

- (BOOL)application:(UIApplication *)application didFinishLaunchingWithOptions:(NSDictionary<UIApplicationLaunchOptionsKey,id> *)launchOptions {
    NSLog(@"[Noctua][CustomAppController] didFinishLaunchingWithOptions: START");

    // Configure Firebase first so FIRMessaging is available when the APNs handshake completes.
    if (![FIRApp defaultApp]) {
        NSLog(@"[Noctua][CustomAppController] Calling [FIRApp configure]");
        [FIRApp configure];
    } else {
        NSLog(@"[Noctua][CustomAppController] FIRApp already configured, skipping");
    }

    // Call super BEFORE wiring push/messaging delegates so Unity's bootstrap does
    // not overwrite our delegate assignments.
    BOOL result = [super application:application didFinishLaunchingWithOptions:launchOptions];

    [FIRMessaging messaging].delegate = self;
    [UNUserNotificationCenter currentNotificationCenter].delegate = self;

    NSLog(@"[Noctua][CustomAppController] FIRMessaging delegate set to: %@", [FIRMessaging messaging].delegate);

    UNAuthorizationOptions authOptions =
        UNAuthorizationOptionAlert | UNAuthorizationOptionSound | UNAuthorizationOptionBadge;

    NSLog(@"[Noctua][CustomAppController] Requesting notification authorization");

    // Only call registerForRemoteNotifications AFTER the user grants permission,
    // on the main queue since UIKit APIs must run there.
    [[UNUserNotificationCenter currentNotificationCenter] requestAuthorizationWithOptions:authOptions
                                                                        completionHandler:^(BOOL granted, NSError * _Nullable error) {
        NSLog(@"[Noctua][CustomAppController] requestAuthorization — granted=%d, error=%@", granted, error);
        if (error) {
            NSLog(@"[Noctua][CustomAppController] Authorization error: %@", error);
            return;
        }
        if (granted) {
            NSLog(@"[Noctua][CustomAppController] Permission granted — registerForRemoteNotifications");
            dispatch_async(dispatch_get_main_queue(), ^{
                [application registerForRemoteNotifications];
            });
        } else {
            NSLog(@"[Noctua][CustomAppController] User denied notification permission");
        }
    }];

    // Do NOT call [FIRMessaging tokenWithCompletion:] here. With
    // FirebaseAppDelegateProxyEnabled = NO the APNs token is not yet attached, so
    // FCM cannot mint a valid registration token. Rely on the delegate callback
    // messaging:didReceiveRegistrationToken: below — it fires automatically once
    // the APNs + FCM handshake completes.

    NSLog(@"[Noctua][CustomAppController] didFinishLaunchingWithOptions: END, result=%d", result);
    return result;
}

#pragma mark - APNs ↔ FIRMessaging bridge

// Required when FirebaseAppDelegateProxyEnabled = NO. Without this,
// FIRMessaging never receives the APNs token and no FCM registration token is
// produced — the root cause of "push not working" reports on this project.
- (void)application:(UIApplication *)application
    didRegisterForRemoteNotificationsWithDeviceToken:(NSData *)deviceToken {
    NSLog(@"[Noctua][CustomAppController] APNs token received (%lu bytes) — forwarding to FIRMessaging",
          (unsigned long)deviceToken.length);
    [FIRMessaging messaging].APNSToken = deviceToken;
    NSLog(@"[Noctua][CustomAppController] FIRMessaging.APNSToken set successfully");

    if ([UnityAppController instancesRespondToSelector:_cmd]) {
        [super application:application didRegisterForRemoteNotificationsWithDeviceToken:deviceToken];
    }
}

- (void)application:(UIApplication *)application
    didFailToRegisterForRemoteNotificationsWithError:(NSError *)error {
    NSLog(@"[Noctua][CustomAppController] APNs registration FAILED: %@", error);

    if ([UnityAppController instancesRespondToSelector:_cmd]) {
        [super application:application didFailToRegisterForRemoteNotificationsWithError:error];
    }
}

#pragma mark - FIRMessaging callbacks

// Fires once FCM has minted a registration token. Do NOT fetch the token inside
// didFinishLaunching — this callback is the correct place.
- (void)messaging:(FIRMessaging *)messaging didReceiveRegistrationToken:(NSString *)fcmToken {
    NSLog(@"[Noctua][CustomAppController] FCM registration token received: %@", fcmToken);

    NSDictionary *dataDict = fcmToken ? @{@"token": fcmToken} : @{};
    [[NSNotificationCenter defaultCenter] postNotificationName:@"FCMToken" object:nil userInfo:dataDict];

    // Forward to managed side so game code can react to token rotation (e.g. re-register
    // with backend push service). Buffer when Unity has not yet registered the handler —
    // the first subscribe call flushes the pending token.
    NSString *safeToken = fcmToken != nil ? fcmToken : @"";
    if (_noctuaOnFcmTokenRefresh != NULL) {
        _noctuaOnFcmTokenRefresh([safeToken UTF8String]);
    } else {
        _bufferedFcmToken = safeToken;
    }
}

#pragma mark - UNUserNotificationCenter callbacks

// Foreground notification presentation.
- (void)userNotificationCenter:(UNUserNotificationCenter *)center
       willPresentNotification:(UNNotification *)notification
         withCompletionHandler:(void (^)(UNNotificationPresentationOptions))completionHandler {
    NSDictionary *userInfo = notification.request.content.userInfo;
    [[FIRMessaging messaging] appDidReceiveMessage:userInfo];
    NSLog(@"[Noctua][CustomAppController] Foreground notification: %@", userInfo);

    // Forward the raw APS + custom payload to managed code so game can react
    // (show in-game banner, update unread counter, etc.). Delivered BEFORE the
    // system banner is presented — game code should not block the completion
    // handler call below. Buffer when Unity is not yet ready.
    NSString *foregroundJson = NoctuaPayloadToJson(userInfo);
    if (_noctuaOnRemoteNotificationReceived != NULL) {
        _noctuaOnRemoteNotificationReceived([foregroundJson UTF8String]);
    } else {
        _bufferedReceivedJson = foregroundJson;
    }

    completionHandler(UNNotificationPresentationOptionBadge |
                      UNNotificationPresentationOptionSound |
                      UNNotificationPresentationOptionList  |
                      UNNotificationPresentationOptionBanner);
}

// User tapped a notification.
- (void)userNotificationCenter:(UNUserNotificationCenter *)center
    didReceiveNotificationResponse:(UNNotificationResponse *)response
             withCompletionHandler:(void(^)(void))completionHandler {
    NSDictionary *userInfo = response.notification.request.content.userInfo;
    if (userInfo[kGCMMessageIDKey]) {
        NSLog(@"[Noctua][CustomAppController] Notification tapped — Message ID: %@", userInfo[kGCMMessageIDKey]);
    }
    [[FIRMessaging messaging] appDidReceiveMessage:userInfo];
    NSLog(@"[Noctua][CustomAppController] Notification response userInfo: %@", userInfo);

    // Primary hook for deep-link handling: game reads a custom field (e.g.
    // "deeplink" or "noctua_deeplink") from the forwarded payload and routes
    // to the appropriate in-game scene. Buffered when Unity is not yet ready so
    // cold-start taps (user tapped to launch the app, arriving during
    // didFinishLaunching before Noctua.InitAsync completes) are not lost.
    NSString *tappedJson = NoctuaPayloadToJson(userInfo);
    if (_noctuaOnNotificationTapped != NULL) {
        _noctuaOnNotificationTapped([tappedJson UTF8String]);
    } else {
        _bufferedTappedJson = tappedJson;
    }

    completionHandler();
}

#pragma mark - Legacy remote-notification callback

- (void)application:(UIApplication *)application
    didReceiveRemoteNotification:(NSDictionary *)userInfo
            fetchCompletionHandler:(void (^)(UIBackgroundFetchResult))completionHandler {
    [[FIRMessaging messaging] appDidReceiveMessage:userInfo];
    NSLog(@"[Noctua][CustomAppController] Remote notification received: %@", userInfo);

    NSString *silentJson = NoctuaPayloadToJson(userInfo);
    if (_noctuaOnRemoteNotificationReceived != NULL) {
        _noctuaOnRemoteNotificationReceived([silentJson UTF8String]);
    } else {
        _bufferedReceivedJson = silentJson;
    }

    completionHandler(UIBackgroundFetchResultNewData);
}

@end

// Unity selects the deepest declared subclass of UnityAppController automatically.
// If a game also ships its own subclass that inherits from CustomAppController
// (or a deeper ancestor), the game's subclass wins and all logic above still
// runs via normal Objective-C super dispatch. If a game ships a SIBLING
// subclass (also direct child of UnityAppController), selection is
// implementation-defined; see the runtime sibling detector below — it logs a
// clear warning so the issue isn't silent.
IMPL_APP_CONTROLLER_SUBCLASS(CustomAppController)

#pragma mark - Sibling-subclass conflict detector

// Runs at image-load time (before main). Walks the ObjC class list and counts
// every direct or indirect subclass of UnityAppController. If more than one
// LEAF subclass is registered (i.e. multiple candidates that no one else
// inherits from) we have a sibling IMPL_APP_CONTROLLER_SUBCLASS conflict —
// Unity's selection becomes link-order dependent, one controller's logic
// silently loses. Log a loud warning so the game dev notices and fixes it by
// chaining the controllers (MyAppController : CustomAppController) or setting
// NOCTUA_DISABLE_CUSTOM_APP_CONTROLLER=1.
__attribute__((constructor))
static void NoctuaDetectAppControllerConflicts(void) {
    Class unityAppController = NSClassFromString(@"UnityAppController");
    if (!unityAppController) return;

    int numClasses = objc_getClassList(NULL, 0);
    if (numClasses <= 0) return;

    Class *classes = (Class *)malloc(sizeof(Class) * (size_t)numClasses);
    numClasses = objc_getClassList(classes, numClasses);

    NSMutableArray<NSString *> *leafCandidates = [NSMutableArray array];

    // First pass: collect every subclass of UnityAppController.
    NSMutableSet<Class> *subclasses = [NSMutableSet set];
    for (int i = 0; i < numClasses; i++) {
        Class cls = classes[i];
        for (Class parent = class_getSuperclass(cls); parent; parent = class_getSuperclass(parent)) {
            if (parent == unityAppController) { [subclasses addObject:cls]; break; }
        }
    }

    // Second pass: keep only leaves (no other class in the set inherits from it).
    for (Class cls in subclasses) {
        BOOL isLeaf = YES;
        for (Class other in subclasses) {
            if (other == cls) continue;
            if (class_getSuperclass(other) == cls) { isLeaf = NO; break; }
        }
        if (isLeaf) [leafCandidates addObject:NSStringFromClass(cls)];
    }

    free(classes);

    if (leafCandidates.count > 1) {
        NSLog(@"╔══════════════════════════════════════════════════════════════════════╗");
        NSLog(@"║ [Noctua][CustomAppController] SIBLING SUBCLASS CONFLICT DETECTED    ║");
        NSLog(@"║ Multiple UnityAppController subclasses are competing for selection: ║");
        for (NSString *name in leafCandidates) {
            NSLog(@"║   • %@", name);
        }
        NSLog(@"║ Unity picks one at link order — one controller's logic will NOT    ║");
        NSLog(@"║ run (push notifications or local notifications may silently break).║");
        NSLog(@"║ Resolution options:                                                 ║");
        NSLog(@"║  1. Chain subclasses: MyAppController : CustomAppController         ║");
        NSLog(@"║  2. Define NOCTUA_DISABLE_CUSTOM_APP_CONTROLLER=1 and implement     ║");
        NSLog(@"║     APNs + FIRMessaging wiring inside your own controller.         ║");
        NSLog(@"║ See docs/troubleshoot/app-controller-conflict for details.          ║");
        NSLog(@"╚══════════════════════════════════════════════════════════════════════╝");
    } else if (leafCandidates.count == 1) {
        NSLog(@"[Noctua][CustomAppController] App controller selection healthy — active leaf: %@", leafCandidates.firstObject);
    }
}

#endif // NOCTUA_DISABLE_CUSTOM_APP_CONTROLLER
