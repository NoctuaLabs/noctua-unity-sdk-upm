#import "UnityAppController.h"
#import "UserNotifications/UserNotifications.h"
#import "FirebaseCore.h"
#import "FirebaseMessaging.h"

@interface CustomAppController : UnityAppController <UNUserNotificationCenterDelegate, FIRMessagingDelegate>

@end

@implementation CustomAppController

NSString *const kGCMMessageIDKey = @"gcm.message_id";

- (BOOL)application:(UIApplication *)application didFinishLaunchingWithOptions:(NSDictionary<UIApplicationLaunchOptionsKey,id> *)launchOptions {
    
    [super application:application didFinishLaunchingWithOptions:launchOptions];

    // Check the existing instance before initialize the new one.
    if(![FIRApp defaultApp]){
    	[FIRApp configure];
    }
    
    // Set FIRMessaging delegate
    [FIRMessaging messaging].delegate = self;

    // Request notification authorization
    [UNUserNotificationCenter currentNotificationCenter].delegate = self;
    UNAuthorizationOptions authOptions = UNAuthorizationOptionAlert |
        UNAuthorizationOptionSound | UNAuthorizationOptionBadge;
    
    [[UNUserNotificationCenter currentNotificationCenter] requestAuthorizationWithOptions:authOptions
                                                                         completionHandler:^(BOOL granted, NSError * _Nullable error) {
        if (error) {
            NSLog(@"Error requesting authorization: %@", error);
        }
    }];
    
    // Register for remote notifications
    [application registerForRemoteNotifications];
    
    // Get Firebase Cloud Messaging (FCM) registration token
    [[FIRMessaging messaging] tokenWithCompletion:^(NSString *token, NSError *error) {
        if (error != nil) {
            NSLog(@"Error getting FCM registration token: %@", error);
        } else {
            NSLog(@"FCM registration token: %@", token);
        }
    }];
    
    return YES;
}

- (void)messaging:(FIRMessaging *)messaging didReceiveRegistrationToken:(NSString *)fcmToken {
    NSLog(@"FCM registration token: %@", fcmToken);
    
    // Notify about received token
    NSDictionary *dataDict = @{@"token": fcmToken};
    [[NSNotificationCenter defaultCenter] postNotificationName:@"FCMToken" object:nil userInfo:dataDict];
    
    // Optionally send token to application server
}

// Handle notifications while app is in the foreground
- (void)userNotificationCenter:(UNUserNotificationCenter *)center
       willPresentNotification:(UNNotification *)notification
         withCompletionHandler:(void (^)(UNNotificationPresentationOptions))completionHandler {
    
    NSDictionary *userInfo = notification.request.content.userInfo;
    
    // Let FIRMessaging handle the message
    [[FIRMessaging messaging] appDidReceiveMessage:userInfo];
    
    // Log full message
    NSLog(@"%@", userInfo);
    
    // Customize notification presentation options
    completionHandler(UNNotificationPresentationOptionBadge |
                       UNNotificationPresentationOptionSound |
                      UNNotificationPresentationOptionList | UNNotificationPresentationOptionBanner);
}

// Handle notification response when user taps on notification
- (void)userNotificationCenter:(UNUserNotificationCenter *)center
didReceiveNotificationResponse:(UNNotificationResponse *)response
         withCompletionHandler:(void(^)(void))completionHandler {
    
    NSDictionary *userInfo = response.notification.request.content.userInfo;
    
    // Log message ID (if available)
    if (userInfo[kGCMMessageIDKey]) {
        NSLog(@"Message ID: %@", userInfo[kGCMMessageIDKey]);
    }
    
    // Let FIRMessaging handle the message
    [[FIRMessaging messaging] appDidReceiveMessage:userInfo];
    
    // Log full message
    NSLog(@"%@", userInfo);
    
    completionHandler();
}

// Handle remote notifications (background or foreground)
- (void)application:(UIApplication *)application didReceiveRemoteNotification:(NSDictionary *)userInfo
    fetchCompletionHandler:(void (^)(UIBackgroundFetchResult))completionHandler {
    
    // Let FIRMessaging handle the message
    [[FIRMessaging messaging] appDidReceiveMessage:userInfo];
    
    // Log full message
    NSLog(@"%@", userInfo);
    
    // Handle background fetch result
    completionHandler(UIBackgroundFetchResultNewData);
}

@end

// Register the subclass
IMPL_APP_CONTROLLER_SUBCLASS(CustomAppController)
