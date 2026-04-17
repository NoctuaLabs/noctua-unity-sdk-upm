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
#ifndef NOCTUA_DISABLE_CUSTOM_APP_CONTROLLER

#import "UnityAppController.h"
#import "UserNotifications/UserNotifications.h"
#import "FirebaseCore.h"
#import "FirebaseMessaging.h"

@interface CustomAppController : UnityAppController <UNUserNotificationCenterDelegate, FIRMessagingDelegate>
@end

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
}

#pragma mark - UNUserNotificationCenter callbacks

// Foreground notification presentation.
- (void)userNotificationCenter:(UNUserNotificationCenter *)center
       willPresentNotification:(UNNotification *)notification
         withCompletionHandler:(void (^)(UNNotificationPresentationOptions))completionHandler {
    NSDictionary *userInfo = notification.request.content.userInfo;
    [[FIRMessaging messaging] appDidReceiveMessage:userInfo];
    NSLog(@"[Noctua][CustomAppController] Foreground notification: %@", userInfo);

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
    completionHandler();
}

#pragma mark - Legacy remote-notification callback

- (void)application:(UIApplication *)application
    didReceiveRemoteNotification:(NSDictionary *)userInfo
            fetchCompletionHandler:(void (^)(UIBackgroundFetchResult))completionHandler {
    [[FIRMessaging messaging] appDidReceiveMessage:userInfo];
    NSLog(@"[Noctua][CustomAppController] Remote notification received: %@", userInfo);
    completionHandler(UIBackgroundFetchResultNewData);
}

@end

// Unity selects the deepest declared subclass of UnityAppController automatically.
// If a game also ships its own subclass that inherits from CustomAppController
// (or a deeper ancestor), the game's subclass wins and all logic above still
// runs via normal Objective-C super dispatch. If a game ships a SIBLING
// subclass (also direct child of UnityAppController), selection is
// implementation-defined; those games should define
// NOCTUA_DISABLE_CUSTOM_APP_CONTROLLER=1 and re-implement the APNs + FIRMessaging
// wiring on their side.
IMPL_APP_CONTROLLER_SUBCLASS(CustomAppController)

#endif // NOCTUA_DISABLE_CUSTOM_APP_CONTROLLER
