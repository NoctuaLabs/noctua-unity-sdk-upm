# Changelog

All notable changes to this project will be documented in this file.

## [0.53.1] - 2025-10-22

### 💼 Other

- Bump version to v0.53.1-icm

## [0.53.0] - 2025-10-21

### 🚀 Features

- Add xml documentation noctua public class
- Add xml documentation noctua authentication class
- Add xml documentation noctua iap class
- Add xml documentation noctua platform class
- Add xml documentation noctua event class
- Allow game to retrieve purchase orderID via event and exception.

### 🐛 Bug Fixes

- Catch exceptions within event's geoIP.

### 💼 Other

- Bump version to v0.53.0.

## [0.52.0] - 2025-10-15

### 🚀 Features

- Enhance handler OnPurchaseDone by QueryPurchaseAsync
- Enhance init while offline

## [0.51.0] - 2025-10-06

### 🚀 Features

- Add exception guards for Firebase ID native calls
- Prevent NullReferenceException when EventSystem is missing in scene

### 💼 Other

- Load country in tracker.
- Determine country from geoIP first before call cloudflare API.

## [0.50.0] - 2025-09-18

### 🚀 Features

- Enhance default country code

## [0.49.0] - 2025-09-18

### 🚀 Features

- Update sdk native wrapper android version 0.13.0 to 0.14.0, ios version 0.17.0 to 0.18.0
- Add get firebase analytics session id and installation id
- Add firebase session id and installation id on internal tracker
- Update noctua native wrapper iOS 0.18.0 to 0.19.0
- Add get firebase installation id and session id
- Enhance additional internal event tracker on init and authentication sdk
- Add experiment manager

## [0.48.0] - 2025-09-04

### 🚀 Features

- Enhance payment status handling

## [0.47.0] - 2025-08-28

### 🚀 Features

- Update native wrapper sdk ios and android
- Add check product if purchased ios
- Completion delegate

## [0.46.4] - 2025-08-14

### 🐛 Bug Fixes

- Update native wrapper sdk

## [0.46.3] - 2025-08-13

### 🐛 Bug Fixes

- Update edm with new version
- Change built in event firebase ad revenue into ad impression

## [0.46.2] - 2025-08-01

### 🐛 Bug Fixes

- Add custom event with revenue

## [0.46.1] - 2025-08-01

### 🐛 Bug Fixes

- Rollback spm to cocoapods

## [0.46.0] - 2025-07-31

### 🚀 Features

- Add track event custom with revenue
- Add check google product purchased

## [0.45.0] - 2025-07-22

### 🚀 Features

- Add internal event tracker game_platform_type on init sdk

### 🐛 Bug Fixes

- Update asset ad placeholder and make it random show ad placeholder per ad type
- Add condition if iaa enabled equal false dont add the symbol

### 🚜 Refactor

- Change GetStoreName to GetPlatformType

## [0.44.1] - 2025-07-14

### 🐛 Bug Fixes

- Move Xcode.Extensions import inside UNITY_IOS preprocessor block

## [0.44.0] - 2025-07-14

### 🚀 Features

- Improve VN legal purpose, do not allow user to exit KYC.
- Add init callback

### 🐛 Bug Fixes

- Update aps-environment to production
- Add more null check for lean noctuagg.json config.
- Add more null check.

### 🚜 Refactor

- Change to use Swift Package Manager and remove Cocoapods

## [0.43.1] - 2025-06-26

### 🐛 Bug Fixes

- Remove space line

## [0.43.0] - 2025-06-26

### 🚀 Features

- Noctua menu for unity editor

### 🐛 Bug Fixes

- Issues collection was modified; enumeration operation may not execute on event sender
- Improve init iaa
- Version package not defined, noctua menu
- Install and remove iaa sdk not working properly

## [0.42.0] - 2025-06-16

### 🚀 Features

- Built in event tracker iaa admob
- Built in event tracker iaa applovin max

### 🐛 Bug Fixes

- Built in iaa event tracker not track properly admob
- Vn full kyc when not enabled
- Email register vn translation wording
- Vn full kyc vn flow
- Translation error not found on email register vn dialog
- Validation form not working properly in email register vn dialog

## [0.41.0] - 2025-06-05

### 🚀 Features

- Implement remote feature flags. Refactor the configuration convention.
- Implement multilevel remote feature flags for VN (phone number verification).
- Update native SDK that bring new configuration convention.
- Improve offline first behaviour. Disable IAP on Unity SDK level if asked by config.
- Add built in login event tracker
- Add loop check internet connection
- Add tracker for internal IAA admob
- Thread-safe event queue for Admob events to ensure they are processed on the main thread
- Add event ads tracker applovin
- Add disbaled adjust offline mode by remote config
- Add admob ad preload manager
- Add preloading admob for ad format interstitial and rewarded
- Add noctua ad placeholder
- Add admob checker when using openupm-cli
- Add Ad Placeholder for Banner and Rewarded Ad
- Admob check init adapter
- Implement VN OEG phone number verification.
- Email register and phone verification for VN

### 🐛 Bug Fixes

- Adjust new config for sanbdox.
- Improve loop internet checker
- Enhance AuthenticateAsync to return empty data
- Remove double check offline status
- Improve init IAA SDK and analytics SDK
- Change variable config to _config
- Improve track ad custom event admob
- Improve IAA event queue
- PreloadManager class error not found when admob sdk is not installed
- Loop for override the client RemoteFeatureFlags if any is not working properly
- Adjust close ad placeholder for applovin max and add show ad placeholder for rewarded interstitial
- Class not found because admob not installed
- Adjust event queue name.
- Safely handle missing or null RemoteFeatureFlags for VN legal purpose
- Init not completed when remote config data is null
- Ui closed earlier when email verification got error
- Ui issues email register vn dialog

### 🚜 Refactor

- AdFormat class into AdFormatNoctua to avoid confused class from Admob preloading configuration
- Change ad placeholder use uitoolkit
- Remove vn flow from email register dialog
- Adjust and add translation for email registration flow
- Translation for date of issues

### ⚙️ Miscellaneous Tasks

- Remove Script folder as we already have separated repository for config generation.

## [0.40.1] - 2025-05-27

### 🐛 Bug Fixes

- Error 'EventSender.Flush()' returns voiid, a return keyword must not be followed by an object expression

## [0.40.0] - 2025-05-27

### 🚀 Features

- Add limit to event queue length.

### 🐛 Bug Fixes

- Header value contains invalid characters
- Object reference not set to an instance of an object when show start game error dialog
- Adjust event queue name.
- Change cycle delay to 5 seconds

### 💼 Other

- Improve log when the event queue is full.

## [0.39.7] - 2025-05-05

### 🐛 Bug Fixes

- Decrease event sender interval from 5 minutes to 1 minute.

## [0.39.6] - 2025-05-02

### 🐛 Bug Fixes

- Swap incorrect offline mode mapping.

## [0.39.5] - 2025-04-30

### 🐛 Bug Fixes

- Handle internet checker crashes when app is quitting
- Handle event flush crashes when app is quitting

### ⚙️ Miscellaneous Tasks

- Update ios native sdk to 0.13.5 (support for disabling IAP).
- Update noctua sdk ios to 0.14.0

## [0.39.4] - 2025-04-29

### 🐛 Bug Fixes

- Skip event flush on IOS as it could cause unexpected crash while sending the event.

## [0.39.3] - 2025-04-28

### 🐛 Bug Fixes

- Add checker when the game is quitting or not playing to avoid crashes
- Handle exeption when check internet connection
- Send offline event only on non-offline event.

## [0.39.2] - 2025-04-23

### 🐛 Bug Fixes

- Handle all status error code above 408 (including 522) properly.

## [0.39.1] - 2025-04-21

### 🐛 Bug Fixes

- Sdk version not found
- Do not track purchase_verify_order_failed if verify triggered by retry, worker, or loop.
- Move some log.Info to log.Debug.

## [0.39.0] - 2025-04-21

### 🚀 Features

- Add bridging against native onOnline and onOffline methods.

### 🐛 Bug Fixes

- Keep init nativePlugin outside the IAA case.
- Add more debug log around nativePlugin initialization.
- Hook up internet check on event sender loop. Increase granularity and decrease batch size.
- Add note to catch 500 error to suppress init error dialog in onfline-first mode.
- Simplify offline checker, fix misleading function name.
- Send offline event only if it is previously online.
- Handle 500 error in offline-first.
- Catch IsOfflineAsync result with ContinueWith.

## [0.38.0] - 2025-04-16

### 🚀 Features

- Add reference for AppLovin MAX SDK
- Add IAA Preprocessor for check mediation SDK is exists and generate the symbols #if UNITY_ADMOB and #if UNITY_APPLOVIN
- Add MediationManager for unified mediation SDK handling
- Add Admob Manager to handling function from ad format functions
- Add AppLovin Manager to handling function from ad format functions
- Add IAdNetwork interface for mediation abstraction
- Add InterstitialAdmob class for managing AdMob interstitial ads
- Add BannerAdmob class for managing AdMob Banner ads
- Add RewardedAdmob class for managing AdMob Rewarded ads
- Add InterstitialAppLovin class for managing AppLovin interstitial ads
- Add RewardedAppLovin class for managing AppLovin Rewarded ads
- Make MediationManager a public class for external access
- Make set ad unit id used function by self
- Add banner applovin
- Add isIAAEnabled to handle init analytics SDK after init IAA SDK success
- Add data class to handle mediation response
- Add event handler admob
- Add event handler for applovin
- Add mediation debugger, creative debugger and enhance some functions
- Rewarded interstitial admob
- Add ad revenue tracker admob and applovin
- Add ad revenue internal tracker
- Add internal tracker
- Add NoctuaEvents to registered player prefs keys.

### 🐛 Bug Fixes

- Rename IAAResponse to IAA
- Remove unecessary code
- Remove whitespace
- Function not found when IAA SDK not installed
- Update comment for IAA init flow
- Update comment for IAA flow
- Refactor log error offline mode as info
- Missing responseInfo class admob
- Change convert value micros
- Preprocessor ios symbols
- Ad revenue tracker crashes
- Ios init ianalytics flow code is not working
- Change condition when iaaEnabled is false
- Keep native plugin init one time
- Improve EventSender.
- Backup internal tracker events to PlayerPrefs. Remove RetryAsync since there is already main loop that handle retries. Reenqueue if failed.
- Try to parse to IConvertible first before parse to object to reduce computation cost.
- Guard flush with try catch inside the async await.
- Remove merge conflict marker.

## [0.37.3] - 2025-04-09

### 🐛 Bug Fixes

- Print serialized-parsed unpaired orders instead of the JSON one.

## [0.37.2] - 2025-04-09

### 🐛 Bug Fixes

- Check null after JsonConvert.Deserialize.

## [0.37.1] - 2025-04-08

### 🐛 Bug Fixes

- Add more observability on the log around unpaired order creation.
- Add more observability on the log around unpaired order creation.

## [0.37.0] - 2025-03-24

### 🚀 Features

- Add ShowSocialMedia() feature.

## [0.36.1] - 2025-03-24

### 🐛 Bug Fixes

- Limit the generated unused port range from 61000 to 61010 so we can register these determined range to Google SSO setup.

## [0.36.0] - 2025-03-11

### 🚀 Features

- Add OnPurchaseDone event to let the game know if a purchase is completed.

### 🐛 Bug Fixes

- Error message not expected

## [0.35.1] - 2025-02-25

### 🐛 Bug Fixes

- Corrected a typo on VerifyOrderTrigger.payment_flow.

## [0.35.0] - 2025-02-24

### 🚀 Features

- Logout confirm dialog
- Logout left icon
- Localization for logout confirmation dialog
- Add Apple SSO
- Add text setting asset and font Noto Sans Thai
- Add text setting for config default font to panel setting
- Implement QueryPurchases to handle pending purchase. Split log to 800-char chunks.
- Pair unpaired purchase to unpaired order for both QueryPurchasesAsync and OnPurchaseUpdate. Add purchase history list.
- Get store/platform name
- Add request header X-PLATFORM into build config
- Add request header X-PLATFORM, X-OS-AGENT, X-OS for all http request
- Add request params exhange token, login as guest
- Add is auth with sdk checker to hide welcome notification
- Add default variable _offlineMode
- Add information about who trigger the verify order API call.

### 🐛 Bug Fixes

- Save button not disabled on starting edit profile
- Update user with the latest data.
- Increase choose account heigh
- Remove welcome notification border
- Eye naming
- Increase choose account heigh
- Datepicker not closed when back into user center in ios
- Number keyboard on verification input field
- Remove noctua text logo when potrait
- Resize icon account delete and change height dialog - account deletion confirmation dialog
- Improve dropdown field code
- Player label not in center position
- Color delete button
- Add localization on error message
- Strange grey selection in account selection
- Eye in show password design is reverse
- Remove error message when resend verification code on verification code window
- Add more padding left for player label - bind conflict dialog
- Keyboard blocking text input - edit profile
- Update credential to last used
- Missing name label - user center
- Remove Apple SSO if not on iOS
- Remove purchase timeout. Store pending purchase early. Add more valuable logs.
- Add payment update listener and pair it with unpaired order IDs. Adjust network related error message for start game error dialog.
- Add NoctuaUnpairedOrders to SDK's player prefs keys.
- Allow payment override from backend only if it's started as primary payment.
- Adding new item to pending purchase is now having old receipt data preserved.
- Revert bug fix that cause payment loop. Add notes instead.
- Fix missing user properties in event tracking.
- Conflict
- Conflict noctua file
- Conflict translation
- Update url test ping
- Add }
- Logic code check internet connection
- Rename isAuthSDK to welcomeToastDisabled
- Handle Online then Offline to show retry dialog
- Handle retry platform when Online then Offline to show retry dialog

### 💼 Other

- Add comment for closeDatepicker android

### 🚜 Refactor

- Improve code close datepicker and open datepicker
- Clean IAP Service code for handle offline mode
- Clean web content code for handle offline mode

### 🧪 Testing

- Add test for locale / language

## [0.34.1] - 2025-01-24

### 🐛 Bug Fixes

- Revert hardcoded deviceId.

## [0.34.0] - 2025-01-24

### 🚀 Features

- Hide native payment button for direct APK distribution.

### 🐛 Bug Fixes

- Reinit if the Playstore billing get disconnected at purchase.
- Add translation for IAPNotReady error.

## [0.33.1] - 2025-01-14

### 🐛 Bug Fixes

- Increase timeout. Show error message toast for Platform Content.

## [0.33.0] - 2025-01-14

### 🚀 Features

- Logout confirm dialog
- Logout left icon
- Localization for logout confirmation dialog
- Add Apple SSO
- Implement remote config to enable or disable SSO.
- Implement remote config for user center SSO linking.
- Add HTTP timeout at 10 seconds. Add X-DEVICE-ID in the header.
- Add APIs to help gamedev maintains SDK playerPrefs.

### 🐛 Bug Fixes

- Show error if no token when getting web content details
- Save button not disabled on starting edit profile
- Update user with the latest data.
- Show mobile input on user center edit profile nicknameTF
- Increase choose account heigh
- Remove welcome notification border
- Eye naming
- Show error if no token when getting web content details
- Increase choose account heigh
- Remove welcome notification border
- Conflict
- Datepicker not closed when back into user center in ios
- Number keyboard on verification input field
- Remove noctua text logo when potrait
- Resize icon account delete and change height dialog - account deletion confirmation dialog
- Improve dropdown field code
- Player label not in center position
- Color delete button
- Add localization on error message
- Strange grey selection in account selection
- Eye in show password design is reverse
- Remove error message when resend verification code on verification code window
- Add more padding left for player label - bind conflict dialog
- Keyboard blocking text input - edit profile
- Update credential to last used
- Work around for fullscreen web content on iOS not aligned
- Restore readonly property of _credentials.

### 💼 Other

- Add comment for closeDatepicker android

### 🚜 Refactor

- Improve code close datepicker and open datepicker

### 🧪 Testing

- Add test for locale / language

## [0.32.0] - 2025-01-09

### 🚀 Features

- Allow user to pay with native payment in custom payment complete dialog.

### 🐛 Bug Fixes

- Button reset password override error label
- Adjust style dialog footer
- Fix missing profile picture in edit mode.
- Revert deleted line.
- Show mobile input on user center edit profile nicknameTF
- Non guest email linking not working
- Fix minor styling issues.
- Birthdate value not set - edit profile
- Adjust clean code

## [0.31.0] - 2025-01-08

### 🚀 Features

- Allow payment type override from backend.

### 🐛 Bug Fixes

- Update code email register dialog
- Update code email reset password dialog
- Update code email confirm reset password dialog
- Update code email confirm reset password dialog
- Code format
- Force scaling at startup.
- Back to user center after email linking
- Change user nickname to user id in bind conflict
- Use exit button with new style
- Use AuthService instead of removed Model proxy method
- Register and login back to login options
- Prevent copy player when scrolling
- Dropdown error label - edit profile
- Filter pending purchases by player id
- Logo profile teralu keatas saat guest diarahkan ke prompt buat switch account
- In edit profile, the loading screen is not the standard UI theme
- Failed to switch accounts if user logged in to other games
- Resolved an issue where the date picker could appear multiple times in the iOS registration flow VN.
- Resolved an issue where the date picker could appear multiple times in the iOS edit profile.
- Instead cancel, create new player if user doesn't want to bind guest
- Webview scale should be floating point
- Reversed match panel settings parameter
- Button not consistent in switch account confirm dialog
- Button not consistent in account deletion confirm dialog
- Change position confirm button - switch account confirm dialog
- Button position not consistent in bind confirmation dialog
- Button position not consistent in bind conflict dialog
- Translate pending purchase dialog to id and vi
- Add PlayerID as parameter for retry pending purchase item. Use JWT parser as backup.
- Add Player ID as VerifyOrderImplAsync param.
- Remove GetPlayerIdFromJwt.
- Bring back disappearing verification code
- Style pending purchase to match UI design
- Close button position
- Change margin close button failed payment dialog
- Add retry to event sender
- Usercenter design not matches

## [0.30.1] - 2024-12-27

### 🐛 Bug Fixes

- Check for existing instance before call Firebase::configure.

## [0.30.0] - 2024-12-27

### 🚀 Features

- Add pending purhase menu translation vn

### 🐛 Bug Fixes

- Auto scaling problem
- Delete old code
- Wrong URL detection cause SSO to fail
- Webview scale broken due to scaling change
- Translation bind or add other account
- Change button style
- Scale should adjust with auto rotate
- Check for existing instance before call Firebase::configure.

### ⚙️ Miscellaneous Tasks

- No verbose log for HTTP requests with passwords

## [0.29.0] - 2024-12-22

### 🚀 Features

- Add custom app controller
- Automation add capability push notification

### 🐛 Bug Fixes

- Update native sdk for ios

## [0.28.0] - 2024-12-20

### 🚀 Features

- Add copublisher logo on register widget. Improve register user experience.
- Pop up success message linked account
- Localization wording account linked

### 🐛 Bug Fixes

- Add null check for account container in email login widget.
- Change picture button is gone after success update image
- Change picture button not center
- Spinner edit profile
- Update design exit pada account selection dialog
- Remove player username from user display name prioritization list.
- Date picker ios
- Improve UX on Vietnam registration.
- Button not active when change date picker and remove not necessary code
- Close the entire user center after profile is successfully updated.
- Remove duplicate lines.
- Reload the entire user center presenter to avoid unexpected bug.

## [0.27.1] - 2024-12-20

### 🐛 Bug Fixes

- Use scrollable page instead of pagination for pending purchase widget.
- Different color panel pop up in bind confirmation dialog and connect conflict dialog
- More option menu still showing after closing and reopen the user center panel
- Design dropdown panel
- Save button turn blue on disable
- Login and register screen looped or stacked
- Use currentActivity instead of applicationContext to ask notification permission
- Check null on EditProfile saveButton enable and apply translations to dropdown fields

## [0.27.0] - 2024-12-19

### 🚀 Features

- Add Accept-Language in HTTP header to help error message translation in backend side.
- Show switch account menu in user center for guest account.
- Add copy localization languages
- Localization for text input validation message - edit profile

### 🐛 Bug Fixes

- Back button is hard to click
- Add retry on failed init due to connection error
- Make sure pending purchases get removed from persistent storage after it get verified.
- Close the loading spinner widget if there is exception in User Center initialization.
- Add order ID validation for VerifyOrderImplAsync().
- Typo
- Add ID label in user ID
- Cutted input field
- Add ID label in user ID
- Cutted input field
- Account selection dialog close button move to right
- User center image resolution
- Align back button with tittle header
- Update reset password wording
- Show error message if payment failed
- Revamp pending purchases widget and add more functionalities (CS, Retry, Copy) and payment details.
- Update error message for Pending Purchase retry attempt.
- Find more website can clickable
- Remove blue login button if the state is still in link account process
- Edit profile ui
- User center isssues
- Apply localization immediately on language change
- Account selection dialog close button move to right
- User center image resolution
- Align back button with tittle header
- Update reset password wording
- Translate text field title except in user update dropdown field
- New scrollbar design
- Change wording connect account when guest user
- Tnc text size and position
- Text to center of button and bottom
- Copy icon change to copy button
- Revamp ui edit profile
- Append reason for customer service URL. Add loading spinner for PlatformContent API.
- Profile edit profile image streched
- Add start game error dialog if sdk init failed
- Error dialogue to overflow
- Add null check on register by email verification.
- Add null check on register by email verification.
- Error dialogue to overflow
- Overflow label text in the new design
- Nickname not update realtime after success edit
- Nickname field empty then save, change picture button is gone
- Use relative path instead of absolute path
- Adjust margin bottom save and remove not necessary code
- Localization text and key
- Typo key
- Update android native sdk to 0.9.0

### ⚙️ Miscellaneous Tasks

- Add SDK version to header.

## [0.26.0] - 2024-12-16

### 🚀 Features

- Registration wizard for Vietnam region.
- Add locale information in HTTP request header.
- Add translation for Retry and Custom Payment Complete dialog.
- Prepare retry pending purchases container.
- Add Pending Purchase widget for both guest and authenticated user.

### 🐛 Bug Fixes

- Add sandbox flag to events
- Add 18 years min age for VN
- Edit Keyboard type to match the value
- Logo switch account yg gepeng
- ConnectConflictDialog Cancel button color to blue
- Region vn not translated
- Datepicker open twice, hard to click, disabled focusable - edit profile
- Improve custom payment cancelation logic.
- Put back user pref for language determination.
- Update Pending Purchases widget title according to the total of the purchases.
- Reset password doesn't automatically login
- Rename SDK first_open to sdk_first_open to differentiate with custom tracker event.

### ⚙️ Miscellaneous Tasks

- Downgrade locale log to Debug.

## [0.25.2] - 2024-12-12

### 🐛 Bug Fixes

- Adjust retry pending purchase mechanism to make it more persistent for upcoming failed verification.

## [0.25.1] - 2024-12-12

### 🐛 Bug Fixes

- Always unset the visibility of current dialog before calling ShowCustomerService().

## [0.25.0] - 2024-12-12

### 🚀 Features

- Enable secondary payment after Noctuastore payment get canceled.

### 🐛 Bug Fixes

- Tidy up some UIs.

## [0.24.0] - 2024-12-12

### 🚀 Features

- Default avatar
- Add action help button
- Add geo metadata in tracker event's extra payload.
- Retry dialog ui
- Retry mechanism for create order and verify order
- Show error notification - purchase
- Get noctua gold
- Add Noctua Payment implementation using native browser with improved retry pending purchase.

### 🐛 Bug Fixes

- Landscape user account design
- Remove payment options
- Add spinner after click continue button in email login
- Add spinner after click continue button in email login
- Update spinner grafic
- Failed init should disable auth completely
- Email confirm reset password panel not move to top when virtual keyboard showing
- Add noctua games to manifest at build times
- Adjust retry dialog ui into center
- Adjust retry mechanism code
- Fix some logs in retry dialog presenter
- Remove log when show retry dialog
- More robust implementation to add keychain sharing
- Filter payment type by runtime platform. Open payment URL with native browser.
- Remove currency from edit profile to prevent user playing around with currency to get cheaper goods.

### ⚙️ Miscellaneous Tasks

- Print all custom tracker event parameters for easier debugging.

## [0.23.0] - 2024-12-10

### 🚀 Features

- Improve currency accuracy by using country to currency map.

### 🐛 Bug Fixes

- Bring extra params for create order.
- Ui edit profile strached
- Icon more option streched - user center
- Change help button to position end - user center
- Change exit button position to flex end - edit profile
- Throws exception on Google Billing error
- Remove superfluous exception message
- Remove permission conflict

### ⚙️ Miscellaneous Tasks

- Add LICENSE

## [0.22.1] - 2024-12-06

### 🐛 Bug Fixes

- Match OrderStatus enum to backen types

## [0.22.0] - 2024-12-05

### 🚀 Features

- Show/Hide password in email login pop up
- Add extra params in purchase request
- Add display_price in product class
- Add extra param in UpdatePlayerAccountAsync - purchase

### 🐛 Bug Fixes

- Login options dialog ui issues
- Text button login with email
- Set default/fallback currency to USD.
- Naming convention
- Show error when playstore payment failed
- Addtional params for product purchase

## [0.21.0] - 2024-12-05

### 🚀 Features

- Copy user data to clipboard when selected account held down for 3 seconds
- Bind confirmation and connect conflict dialogs
- Show/Hide password in email login pop up

### 🐛 Bug Fixes

- Set platform distribution by platform OS instead of payment type.
- Sso logo stretched
- Update IOS upstream library that fix Adjust event map null check.
- Remove justify in root
- Add translation to Bind Confirmation and Connect Conflict Dialog
- Wording id 'continue with another account'

### 🚜 Refactor

- Sso logo stretched code

## [0.20.0] - 2024-12-03

### 🚀 Features

- Add sentry dll files
- Add configuration sentry
- Add Dsn sentry url to config
- Log json body http
- Update sdk native version

### 🐛 Bug Fixes

- Change from Noctua.Log.Debug to _log.debug
- Delete log http
- Set result for google billing product details and makes CreateOrder works again
- Forgotten temporary undef

### 🚜 Refactor

- Do not write if url sentry is empty
- Change _log to Debug.Log
- Change noctua logger init position

### ⚙️ Miscellaneous Tasks

- Add logging to aid debugging

## [0.19.9] - 2024-11-29

### 🐛 Bug Fixes

- Linker error for removed noctuaCloseKeyboardiOS

## [0.19.8] - 2024-11-28

### 🐛 Bug Fixes

- Update Android SDK to remove QUERY_ALL_PACKAGES permission

## [0.19.7] - 2024-11-28

### 🐛 Bug Fixes

- Determine language by this priority; user preference, region, system language.
- Update user prefs payment type with string instead of numeric enum.
- Use Active label instead of Recent for current active/recent account.
- Update wording for continue with another account button.
- Update the user language preference immediately after successfully update to backend.
- Remove duplicate HTTP log.
- Keyboard not closed after entering input

### ⚙️ Miscellaneous Tasks

- Initiate locale once, then inject it anywhere we need.

## [0.19.6] - 2024-11-27

### 🐛 Bug Fixes

- Keyboard show up at startup
- Blur if visible false

## [0.19.5] - 2024-11-27

### 🐛 Bug Fixes

- Simplify enum conversion

## [0.19.4] - 2024-11-26

### 🐛 Bug Fixes

- Use fallback if native account store is unavailable.

### 🚜 Refactor

- Change throw exeption to log warning

## [0.19.3] - 2024-11-25

### 🐛 Bug Fixes

- Handle webview url empty
- Expose iOS logs to Files app

## [0.19.2] - 2024-11-22

### 🐛 Bug Fixes

- Session not initialized in iOS
- Purchase error blocked by loading box and platform param should be included in  get product list
- Notif box text should not overflow
- Redirect some Debug.Log to files and logcat/os_log
- Remove unnecessary namespace from Editor assembly
- UI event handling breaks when changing scenes
- Clear login form after success
- Clear form register after success

## [0.19.1] - 2024-11-20

### 🐛 Bug Fixes

- Welcome toast doesn't show up at first call of AuthenticateAsync

## [0.19.0] - 2024-11-20

### 🚀 Features

- Add translation for user banned info
- Add exception error code for user banned
- General confirm dialog for user banned
- Add public method general confirm dialog
- Add handle error user banned - login with email

### 🐛 Bug Fixes

- Update translation vn
- Function authenticateAsync
- Rename key localization contact support
- Make throw exeption after user clicked button or hyperlink
- Retry saving account if failed

### 🚜 Refactor

- Method authenticateAsync
- Rename GeneralConfirmDialog to ConfirmationDialog
- Used color for hyperlink - translation for user banned
- Rename with spesific name banned confirmation dialog
- Rename method name to ShowBannedConfirmationDialog
- Changed to async and return UniTask

## [0.18.2] - 2024-11-19

### 🐛 Bug Fixes

- Overlay UI should block whole screen
- Account item should have no hover and connected account item should have no action indicator
- Align SDK version text in UserCenter

## [0.18.1] - 2024-11-18

### 🐛 Bug Fixes

- Ios log using os_log

## [0.18.0] - 2024-11-13

### 🚀 Features

- New spinner and logger to file

## [0.17.0] - 2024-11-13

### 🚀 Features

- Makes accounts available across games in iOS
- Add bridging function close keyboard ios
- Firebase crashlytics

### 🐛 Bug Fixes

- Virtual keyboard not hidden in iOS
- Validate call function close keyboard only ios
- Update noctua android sdk native to 0.6.0
- Change to follow BE payment type

## [0.16.0] - 2024-11-07

### 🚀 Features

- Dynamic custom event suffix for Android and iOS
- Add sdk version to account selection and user center

### 🐛 Bug Fixes

- Update iOS SDK to lower Facebook SDK version that doesn't have Swift float linker error
- Support ban user by exchange token for current game
- Track USD as revenue while still keeping  original currency

### 📚 Documentation

- Add manual release guide [skip ci]

## [0.15.1] - 2024-11-01

### 🐛 Bug Fixes

- Tracker can be used without calling init
- Account bound should be fired on registering email with guest account
- Add semicolon to CI
- Purchase completed also send to native tracker
- Use credential provider before deleted when sending event
- Still used noctua logo
- Ui loading progress
- Make default show exit button for behaviour vn
- Remove params isShowBackButton
- Remove SSO connect UI - user center
- Hide noctua logo welcome notification

## [0.15.0] - 2024-10-29

### 🚀 Features

- Add events to IAP and fix retry pending purchases
- Add platform content events
- Translation vn
- Add translation vn language
- Add translation for select gender and country
- Add session tracking

### 🐛 Bug Fixes

- Text not translated - user center
- Text not translated - email register
- Object reference not set when open user center the first time (not logged yet)
- Translation loading
- Add retry to Google Billing init
- Use WebUtility instead of HttpUtility to be compatible with .NET Framework API Level

## [0.14.0] - 2024-10-25

### 🚀 Features

- Authentication builtin tracker
- Add events  to Auth UIs, add double events to some auth process
- Add localization EN json file
- Add configuration for localization
- Add localization
- Add indonesia localization file

### 🐛 Bug Fixes

- Adjsut name some widget to support localization - email login
- Update the localization text sources
- Enhance code localization data
- Update text localization
- Rename label name text
- Optimaze config localization code
- Enhance code to support localization - user center
- Translation en type widget
- Translation id type widget
- Translate label inside the button as container
- Change debug log to noctua debug log
- Add support for Unity versions with older Gradle 6
- Optional copublisher config should be ignored instead of exception
- Add elvis operators to potentially null configs

### 🧪 Testing

- Fix tests to adjust with original requirements

## [0.13.0] - 2024-10-22

### 🚀 Features

- Add cross-game account storage
- Add countries data and phone code
- Add registration extra params for behaviour vn
- Form field for behaviour whitelabel vn
- Picker id param
- Add form register for behaviour whitelabel vn
- Configuration behaviour whitelabel vn
- Remove close/back button in login with email for behaviour whitelabel vn
- Don't show notif user guest if behavior whitelabel vn is true
- Disable SSO for Behaviour whitelabel vn
- Show direct login with email when player continue with other account
- Add reusable ContainsFlag Checker
- Add event tracker generator for multiple platforms and multiple thirdparty trackers.

### 🐛 Bug Fixes

- Shim for android sdk with content provider
- Guest can't bind account
- Change flag by company name
- Update library date picker android native
- Conflict rebase
- Update bridging file date picker native ios
- Adjust code conflict
- Update player avatar
- Change req_extra to dictionary
- Add idPicker params - showDatePicker
- Adjust code to filter non guest account - account selection
- Enhance flag checking more robust
- Make token optional for fetching platform content.

### 🚜 Refactor

- Open date picker - user center

## [0.12.0] - 2024-10-16

### 🚀 Features

- Add oeg logo
- Whitelabel - user center
- Add whitelabel - login options
- Add whitelabel - account selection
- Add reusbale get co publisher logo
- Add configuration for whitelabel

### 🐛 Bug Fixes

- Tnc and privacy can clickable
- Don't destroy panelSettings when switching scene

### 🚜 Refactor

- Name logo with text - user center
- Method get co publisher logo

## [0.11.0] - 2024-10-14

### 🚀 Features

- Add copy user id to clipboard
- Reusable validate textfield

### 🐛 Bug Fixes

- Add more failsafe around SDK init.
- Style button save - edit profile
- Disable button when text input is empty
- Disable button when text input is empty
- Disable button when text input is empty
- Disable button when text input is empty
- Prevent registering twice
- Optimaze validate textfield code
- Calculating webview frame

## [0.10.0] - 2024-10-11

### 🚀 Features

- Add method detect multiple values changes

### 🐛 Bug Fixes

- Color not change message notification
- Back to user center after update profile success
- Spinner ui not correct
- Spinner ui, profile url null will not loaded, detect value changes to enable button save
- Dont destroy UI with new scene

## [0.9.1] - 2024-10-10

### ⚙️ Miscellaneous Tasks

- Split long log

## [0.9.0] - 2024-10-10

### 🚀 Features

- Add method remove white space

### 🐛 Bug Fixes

- Include token only if it's guest
- Error label not hide when on loading spinner
- Email not valid when have white space
- Email not valid when have white space
- Hide welcome notificaiton when success reset password
- Change wording Continue to Reset Password and Login
- Rollback after reset password and then login
- Hide show login with email after reset password success
- Bug ui edit profile and adjust save change profile
- Ordering ui message notification and loading progress
- Update VerifyOrderResponse struct to match with BE.
- Error label not showing when email verif code
- Apply scaling consistently between editor and real device

### 🚜 Refactor

- Remove utility method remove white space
- Use method directly to remove white space

## [0.8.0] - 2024-10-07

### 🚀 Features

- Add 3rd party NativeGallery
- Add edit profile service
- Add file uploader services
- Add get profile options service
- Date picker and refactor code
- Image picker
- Add payment by noctua website
- Spinner, error label and styling dropdown
- Add NoctuaWebContent
- Add payment type in user profile
- Noctua logo with text footer in edit profile left side
- Add date picker
- Add loading progress when iap
- Add parse query string

### 🐛 Bug Fixes

- More options menu shows on guest account opening user center
- Clear email reset password confirmation on start and makes error text not floating
- Rebase and resolve conflict
- Nickname input field and profile image and button change profile
- Change method post to get profile options
- Use tokens only if needed
- Bug ui edit profile when directly close
- Margin dropdown and border color
- Retry pending purchases with backoff
- Update native sdk to delete manifest entry that removes gms.permission.AD_ID
- Add validation dropdown and add default value for payment type
- Error label not hide
- Date picker default value
- Edit profile not working
- Remove log
- Set enabled payment types priority higher than user preferences.
- Update to ios-sdk-v0.2.0 to include facebook sdk as static framework
- Makes webview can be wrapped with UI and don't show again toggle
- Verif order not working
- Verif order not processed
- Get receipt data from response payment url noctua wallet

### 🚜 Refactor

- Moves Event methods under NoctuaEventService class
- Makes UI creation more reusable via UIFactory
- Take out social authentication flow from AuthenticationModel
- Use enum status and move tcs out of retry
- General notificaiton can be reusable
- Hide loading progress for temporary
- General notification message and loading progress

### 📚 Documentation

- Update readme

## [0.7.0] - 2024-09-18

### 🚀 Features

- Implement account deletion with confirmation dialog.
- Implement purchaseItem bridging against native SDK.
- Icon two hand carousel
- Code to show object in Noctua.uss
- Uss code configuration for carousel
- Add uxml carousel in user center
- Add carousel logic in user center presenter
- Wire up GetActiveCurrency for Android. Use UniTask for PurchaseItemAsync.
- Apply facebook config to android project

### 🐛 Bug Fixes

- Ios bridging init
- Truncate long PlayerName
- Update iOS SDK version to fix JSON serialization crash
- Show error on failed social link
- Change auto properties to public fields to avoid code stripping
- Remove get set to preserve deeply.
- Make Firebase tracker works from Unity
- Configure firebase Android from Unity
- Facebook player avatar
- Guest binding offer
- Guest connect button
- Remove GoogleService-Info.plist from project if Firebase disabled

### 🚜 Refactor

- Indicator style code to uss code
- Remove comparation state

## [0.6.0] - 2024-09-09

### 🚀 Features

- Add UniWebView
- Warning icon (error notification icon)
- Error notification ui
- Add public method show general notification error
- Add method show notification error user center
- Add method show notification error login options

### 🐛 Bug Fixes

- Add optional redirect_uri on desktop
- Add uniwebview android AAR and moves UniWebView inside Plugins folder
- Add facebook login support
- Handle error on social login failed
- Should throw error response from BE

### 🚜 Refactor

- Makes config load more robust

## [0.5.2] - 2024-09-07

### 🐛 Bug Fixes

- Font and layout doesn't render correctly

## [0.5.1] - 2024-09-06

### 🐛 Bug Fixes

- Unwanted selected background on listview
- Margin top dialog-title
- Check for BOM characters before skipping

## [0.5.0] - 2024-09-04

### 🚀 Features

- Click outside to close MoreOptionsMenu

### 🐛 Bug Fixes

- Use link endpoints instead of login
- Remove warning and fix MoreOptionsMenu styles
- Make icons on MoreOptionsMenu smaller
- IOS and Android runtime error

## [0.4.0] - 2024-09-02

### 🚀 Features

- Login dialog ui
- Register dialog ui
- Login dialog style
- Email verification code ui
- Login options dialog
- Add player avatar
- Add user center
- Edit profile ui
- Add cs file edit profile
- Skeleton for register and reset-password flow
- Login options dialog
- Enable social login
- Implement UpdatePlayerAccountAsync
- OnAccountChanged and OnAccountDeleted
- Social login user center
- Change user center layout based on screen orientation

### 🐛 Bug Fixes

- Change name function
- Split account list into game users and noctua users
- Fix dummy var initiation
- Reset password endpoint and request
- VerifyCode is only for registration
- Check size before slicing
- Styling, navigation, memory leak
- Rename ShowUserCenterUI() to UserCenter()
- User center get data from /api/v1/user/profile

### 💼 Other

- User center

### 🚜 Refactor

- Move UI actions to NoctuaBehavior
- Conform more closely to MVP pattern
- Delete unused bind dialog

## [0.3.0] - 2024-08-15

### 🚀 Features

- Guest login integration
- Add welcome notification

## [0.2.0] - 2024-08-08

### 🚀 Features

- Integrate ios plugin

## [0.1.2] - 2024-07-31

### 🐛 Bug Fixes

- Change AndroidJNIHelper.Box for 2021.3 compatibility

## [0.1.1] - 2024-07-25

### 🐛 Bug Fixes

- .gitlab-ci.yml rules

### 📚 Documentation

- Reformat bullets

### ⚙️ Miscellaneous Tasks

- Add trigger CI
- Add CI for release
- *(ci)* Fix invalid yaml
- *(ci)* Fix invalid yaml again
- *(ci)* Fix curl
- *(ci)* Fix skipped bump-version
- *(ci)* Generate release notes for github

## [0.1.0] - 2024-07-24

### 🚀 Features

- Basic event trackers wrapping Noctua Android SDK

### 📚 Documentation

- Add README.md for getting started with this package
- Add README.md.meta from Unity Editor
- Add README.md for installation and getting started
- Edit README.md to add required config file
- Add platform settings for EDM

<!-- generated by git-cliff -->
