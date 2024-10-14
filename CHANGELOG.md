# Changelog

All notable changes to this project will be documented in this file.

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

### 🚜 Refactor

- Move UI actions to NoctuaBehavior
- Conform more closely to MVP pattern
- Delete unused bind dialog

### Wip

- User center

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
