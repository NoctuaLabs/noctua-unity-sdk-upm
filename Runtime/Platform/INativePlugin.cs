using System;
using System.Collections.Generic;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Bridges analytics tracking calls to native SDK trackers (Adjust, Firebase, etc.).
    /// </summary>
    public interface INativeTracker
    {
        /// <summary>
        /// Tracks an ad revenue event in the native analytics SDK.
        /// </summary>
        /// <param name="source">The ad network source (e.g., "admob", "applovin").</param>
        /// <param name="revenue">The revenue amount.</param>
        /// <param name="currency">The ISO 4217 currency code (e.g., "USD").</param>
        /// <param name="extraPayload">Optional additional key-value pairs to include in the event.</param>
        void TrackAdRevenue(string source, double revenue, string currency, Dictionary<string, IConvertible> extraPayload = null);

        /// <summary>
        /// Tracks a purchase event in the native analytics SDK.
        /// </summary>
        /// <param name="orderId">The order or transaction identifier.</param>
        /// <param name="amount">The purchase amount.</param>
        /// <param name="currency">The ISO 4217 currency code (e.g., "USD").</param>
        /// <param name="extraPayload">Optional additional key-value pairs to include in the event.</param>
        void TrackPurchase(string orderId, double amount, string currency, Dictionary<string, IConvertible> extraPayload = null);

        /// <summary>
        /// Tracks a custom named event in the native analytics SDK.
        /// </summary>
        /// <param name="name">The event name.</param>
        /// <param name="extraPayload">Optional additional key-value pairs to include in the event.</param>
        void TrackCustomEvent(string name, Dictionary<string, IConvertible> extraPayload = null);

        /// <summary>
        /// Tracks a custom named event with revenue in the native analytics SDK.
        /// </summary>
        /// <param name="name">The event name.</param>
        /// <param name="revenue">The revenue amount associated with the event.</param>
        /// <param name="currency">The ISO 4217 currency code (e.g., "USD").</param>
        /// <param name="extraPayload">Optional additional key-value pairs to include in the event.</param>
        void TrackCustomEventWithRevenue(string name, double revenue, string currency, Dictionary<string, IConvertible> extraPayload = null);

        /// <summary>
        /// Notifies the native tracker that the device has come online.
        /// </summary>
        void OnOnline();

        /// <summary>
        /// Notifies the native tracker that the device has gone offline.
        /// </summary>
        void OnOffline();
    }

    /// <summary>
    /// Bridges in-app purchase operations to the native store (Google Play Billing / Apple StoreKit).
    /// </summary>
    public interface INativeIAP
    {
        /// <summary>
        /// Initiates a purchase flow for the specified product via the native store.
        /// </summary>
        /// <param name="productId">The store product identifier.</param>
        /// <param name="callback">Callback with success status and a message or receipt token.</param>
        void PurchaseItem(string productId, Action<bool, string> callback);

        /// <summary>
        /// Retrieves the active currency for a product from the native store.
        /// </summary>
        /// <param name="productId">The store product identifier.</param>
        /// <param name="callback">Callback with success status and the currency code.</param>
        void GetActiveCurrency(string productId, Action<bool, string> callback);

        /// <summary>
        /// Checks whether a product has been purchased by the current user.
        /// </summary>
        /// <param name="productId">The store product identifier.</param>
        /// <param name="callback">Callback indicating whether the product is owned.</param>
        void GetProductPurchasedById(string productId, Action<bool> callback);

        /// <summary>
        /// Retrieves the StoreKit 1 receipt data for a purchased product (iOS only).
        /// </summary>
        /// <param name="productId">The store product identifier.</param>
        /// <param name="callback">Callback with the receipt string.</param>
        void GetReceiptProductPurchasedStoreKit1(string productId, Action<string> callback);

        /// <summary>
        /// Retrieves detailed purchase status for a product, including acknowledgment and renewal state.
        /// </summary>
        /// <param name="productId">The store product identifier.</param>
        /// <param name="callback">Callback with the full <see cref="ProductPurchaseStatus"/> details.</param>
        void GetProductPurchaseStatusDetail(string productId, Action<ProductPurchaseStatus> callback);

        /// <summary>
        /// Completes purchase processing on the native side after server verification.
        /// On iOS, this finishes the SKPaymentTransaction via <c>finishTransaction</c>.
        /// On Android, this consumes or acknowledges the Google Play purchase.
        /// Must be called after the server returns <c>OrderStatus.completed</c>.
        /// </summary>
        /// <param name="purchaseToken">The native purchase token (transaction ID on iOS, purchase token on Android).</param>
        /// <param name="consumableType">The consumable type of the product.</param>
        /// <param name="verified">Whether the server verification succeeded.</param>
        /// <param name="callback">Optional callback with success status.</param>
        void CompletePurchaseProcessing(string purchaseToken, NoctuaConsumableType consumableType, bool verified, Action<bool> callback);
    }

    /// <summary>
    /// Bridges account persistence to native secure storage (Keychain on iOS, encrypted prefs on Android).
    /// </summary>
    public interface INativeAccountStore
    {
        /// <summary>
        /// Retrieves a single account by player ID and game ID from native storage.
        /// </summary>
        /// <param name="userId">The player (user) identifier.</param>
        /// <param name="gameId">The game identifier.</param>
        /// <returns>The matching <see cref="NativeAccount"/>, or null if not found.</returns>
        NativeAccount GetAccount(long userId, long gameId);

        /// <summary>
        /// Retrieves all stored accounts from native storage.
        /// </summary>
        /// <returns>A list of all <see cref="NativeAccount"/> entries.</returns>
        List<NativeAccount> GetAccounts();

        /// <summary>
        /// Inserts or updates an account in native storage.
        /// </summary>
        /// <param name="account">The account to store.</param>
        void PutAccount(NativeAccount account);

        /// <summary>
        /// Deletes an account from native storage.
        /// </summary>
        /// <param name="account">The account to delete.</param>
        /// <returns>The number of accounts removed.</returns>
        int DeleteAccount(NativeAccount account);
    }

    /// <summary>
    /// Bridges native date picker display and dismissal on mobile platforms.
    /// </summary>
    public interface INativeDatePicker
    {
        /// <summary>
        /// Displays the native date picker initialized to the specified date.
        /// </summary>
        /// <param name="year">The initial year to display.</param>
        /// <param name="month">The initial month (1-12) to display.</param>
        /// <param name="day">The initial day of month to display.</param>
        /// <param name="id">A unique picker identifier used to correlate the result callback.</param>
        void ShowDatePicker(int year, int month, int day, int id);

        /// <summary>
        /// Dismisses the currently displayed native date picker.
        /// </summary>
        void CloseDatePicker();
    }

    /// <summary>
    /// Firebase and Adjust analytics bridge methods.
    /// </summary>
    public interface INativeFirebase
    {
        /// <summary>
        /// Retrieves the Firebase Installation ID from the native SDK.
        /// </summary>
        /// <param name="callback">Callback with the installation ID string, or empty on failure.</param>
        void GetFirebaseInstallationID(Action<string> callback);

        /// <summary>
        /// Retrieves the Firebase Analytics session ID from the native SDK.
        /// </summary>
        /// <param name="callback">Callback with the session ID string, or empty on failure.</param>
        void GetFirebaseAnalyticsSessionID(Action<string> callback);

        /// <summary>
        /// Retrieves the current Firebase Cloud Messaging (FCM) registration token from the native SDK.
        /// The token is minted once the APNs (iOS) / FCM registration handshake completes — callers
        /// may receive an empty string if invoked too early after SDK init. Recommended pattern:
        /// retry once after a short delay, or subscribe to the per-platform token-refresh event.
        /// </summary>
        /// <param name="callback">Callback with the FCM token string, or empty on failure.</param>
        void GetFirebaseMessagingToken(Action<string> callback) { callback?.Invoke(string.Empty); }

        /// <summary>
        /// Registers a delegate that fires whenever a remote push notification arrives (foreground or background).
        /// Payload arrives as a JSON string representing the full APS + custom dictionary.
        /// Call once during SDK init; replaces any previously registered handler.
        /// </summary>
        void SetRemoteNotificationReceivedHandler(Action<string> handler) { }

        /// <summary>
        /// Registers a delegate that fires when the user taps a notification (primary deeplink hook).
        /// Payload arrives as a JSON string — game code should parse the expected custom fields
        /// (e.g. "deeplink", "route", "noctua_deeplink") and route to the appropriate screen.
        /// Call once during SDK init.
        /// </summary>
        void SetNotificationTappedHandler(Action<string> handler) { }

        /// <summary>
        /// Registers a delegate that fires when Firebase Cloud Messaging rotates the FCM
        /// registration token (reinstall, app-data clear, device restore, periodic refresh).
        /// Game code should re-register the new token with their backend push service.
        /// Call once during SDK init.
        /// </summary>
        void SetFirebaseMessagingTokenRefreshHandler(Action<string> handler) { }

        /// <summary>
        /// Fetches a string value from Firebase Remote Config via the native SDK.
        /// </summary>
        /// <param name="key">The Remote Config parameter key.</param>
        /// <param name="callback">Callback with the config string value.</param>
        void GetFirebaseRemoteConfigString(string key, Action<string> callback);

        /// <summary>
        /// Fetches a boolean value from Firebase Remote Config via the native SDK.
        /// </summary>
        /// <param name="key">The Remote Config parameter key.</param>
        /// <param name="callback">Callback with the config boolean value.</param>
        void GetFirebaseRemoteConfigBoolean(string key, Action<bool> callback);

        /// <summary>
        /// Fetches a double value from Firebase Remote Config via the native SDK.
        /// </summary>
        /// <param name="key">The Remote Config parameter key.</param>
        /// <param name="callback">Callback with the config double value.</param>
        void GetFirebaseRemoteConfigDouble(string key, Action<double> callback);

        /// <summary>
        /// Fetches a long value from Firebase Remote Config via the native SDK.
        /// </summary>
        /// <param name="key">The Remote Config parameter key.</param>
        /// <param name="callback">Callback with the config long value.</param>
        void GetFirebaseRemoteConfigLong(string key, Action<long> callback);

        /// <summary>
        /// Retrieves the Adjust attribution data as a JSON string from the native SDK.
        /// </summary>
        /// <param name="callback">Callback with the attribution JSON, or empty on failure.</param>
        void GetAdjustAttribution(Action<string> callback);
    }

    /// <summary>
    /// Native event persistence (save, retrieve, delete events).
    /// </summary>
    public interface INativeEventStorage
    {
        /// <summary>
        /// Saves a batch of events as a JSON array string to native persistent storage (legacy blob API).
        /// </summary>
        /// <param name="jsonString">JSON array string containing the serialized events.</param>
        void SaveEvents(string jsonString);

        /// <summary>
        /// Retrieves all previously saved events from native storage (legacy blob API).
        /// </summary>
        /// <param name="callback">Callback with the list of event JSON strings.</param>
        void GetEvents(Action<List<string>> callback);

        /// <summary>
        /// Deletes all saved events from native storage (legacy blob API).
        /// </summary>
        void DeleteEvents();

        /// <summary>
        /// Inserts a single event into per-row native storage for unlimited event tracking.
        /// </summary>
        /// <param name="eventJson">The serialized event JSON string.</param>
        void InsertEvent(string eventJson);

        /// <summary>
        /// Retrieves a paginated batch of events from per-row native storage.
        /// </summary>
        /// <param name="limit">Maximum number of events to return.</param>
        /// <param name="offset">Number of events to skip from the start.</param>
        /// <param name="callback">Callback with the list of <see cref="NativeEvent"/> entries.</param>
        void GetEventsBatch(int limit, int offset, Action<List<NativeEvent>> callback);

        /// <summary>
        /// Deletes events by their IDs from per-row native storage.
        /// </summary>
        /// <param name="ids">Array of event IDs to delete.</param>
        /// <param name="callback">Callback with the number of events actually deleted.</param>
        void DeleteEventsByIds(long[] ids, Action<int> callback);

        /// <summary>
        /// Returns the total number of events stored in per-row native storage.
        /// </summary>
        /// <param name="callback">Callback with the event count.</param>
        void GetEventCount(Action<int> callback);
    }

    /// <summary>
    /// Native plugin lifecycle (init, pause/resume, dispose).
    /// </summary>
    public interface INativeLifecycle
    {
        /// <summary>
        /// Initializes the native plugin with the list of active bundle identifiers.
        /// </summary>
        /// <param name="activeBundleIds">Bundle IDs of the active games/apps.</param>
        void Init(List<String> activeBundleIds);

        /// <summary>
        /// Notifies the native plugin of application pause or resume state changes.
        /// </summary>
        /// <param name="pause">True when the application is pausing, false when resuming.</param>
        void OnApplicationPause(bool pause);

        /// <summary>
        /// Disposes the native StoreKit/billing service and releases resources.
        /// Called automatically on application quit.
        /// </summary>
        void DisposeStoreKit();

        /// <summary>
        /// Returns whether the native StoreKit/billing service is initialized and ready for operations.
        /// </summary>
        /// <returns>True if the store service is ready, false otherwise.</returns>
        bool IsStoreKitReady();

        /// <summary>
        /// Registers a callback that native code invokes on platform lifecycle transitions.
        /// The callback receives "resume" when the app becomes active, "pause" when it resigns.
        /// </summary>
        /// <param name="callback">Action receiving "resume" or "pause". Pass null to unregister.</param>
        void RegisterNativeLifecycleCallback(Action<string> callback);
    }

    /// <summary>
    /// Bridges in-app review and in-app update operations to native platform APIs
    /// (Google Play Core on Android, StoreKit on iOS).
    /// </summary>
    public interface INativeAppManagement
    {
        /// <summary>
        /// Requests the native in-app review dialog.
        /// The OS decides whether to actually show the prompt (rate-limited).
        /// </summary>
        /// <param name="callback">Callback with true if the flow completed, false on failure.</param>
        void RequestInAppReview(Action<bool> callback);

        /// <summary>
        /// Checks if an app update is available (Android only).
        /// </summary>
        /// <param name="callback">Callback with a JSON string containing update availability info.</param>
        void CheckForUpdate(Action<string> callback);

        /// <summary>
        /// Starts an immediate (blocking) app update flow (Android only).
        /// </summary>
        /// <param name="callback">Callback with result code (0=Success, 1=Cancelled, 2=Failed, 3=NotAvailable).</param>
        void StartImmediateUpdate(Action<int> callback);

        /// <summary>
        /// Starts a flexible (background download) app update (Android only).
        /// </summary>
        /// <param name="onProgress">Callback with download progress (0.0 to 1.0).</param>
        /// <param name="onResult">Callback with result code when download completes or fails.</param>
        void StartFlexibleUpdate(Action<float> onProgress, Action<int> onResult);

        /// <summary>
        /// Completes a previously downloaded flexible update by installing it.
        /// The app will restart after this call.
        /// </summary>
        void CompleteUpdate();
    }

    /// <summary>
    /// Inspector-only — bridges native logcat / os_log streams to the Unity
    /// "Logs" tab. Implementations gate themselves on
    /// <c>NoctuaInspectorBus.isEnabled()</c>; the bus is only flipped on by
    /// the Unity-side controller, which itself only spawns when sandbox
    /// mode is enabled. Production builds therefore see no callbacks.
    /// </summary>
    public interface INativeLogStream
    {
        /// <summary>
        /// Toggles the native log stream. When false the native side stops
        /// reading logcat / OSLogStore and stops invoking the callback.
        /// Defaults off — the volume is high enough that we want explicit
        /// user opt-in from the Inspector "Logs" tab.
        /// </summary>
        void SetLogStreamEnabled(bool enabled);

        /// <summary>
        /// Registers a callback for each captured native log line.
        /// Parameters: (level 2..6 logcat priority, source "iOS"/"Android"/SDK name, tag, message, timestampMillisUtc).
        /// Pass null to unregister.
        /// </summary>
        void RegisterNativeLogCallback(Action<int, string, string, string, long> callback);
    }

    /// <summary>
    /// Inspector-only — exposes native device metrics that Unity cannot read
    /// directly (iOS phys_footprint, Android PSS, thermal pressure). Polled
    /// at 1Hz from <see cref="MemoryMonitor"/>.
    ///
    /// Implementations must return <see cref="DeviceMetricsSnapshot.Empty"/>
    /// for any field the platform does not expose, never throw, and never
    /// block — the call is on the Unity main thread and runs every second.
    /// </summary>
    public interface INativeDeviceMetrics
    {
        DeviceMetricsSnapshot SnapshotDeviceMetrics();
    }

    /// <summary>
    /// Inspector-only — destructive maintenance actions exposed by the
    /// "Memory" tab's Action Panel. Implementations are no-ops in
    /// production builds (the actions are only invoked through the
    /// sandbox-gated Inspector UI).
    /// </summary>
    /// <summary>
    /// Inspector-only — read-only build/config metadata exposed for the
    /// "Build" sanity panel. All getters return safe defaults (empty
    /// string / -1 / false) when the platform doesn't expose the metric.
    /// </summary>
    public interface INativeBuildInfo
    {
        /// <summary>Native SDK version string (e.g. "0.36.0").</summary>
        string GetNativeSdkVersion();

        /// <summary>
        /// Firebase project ID from `GoogleService-Info.plist` (iOS) or
        /// `FirebaseApp.options.projectId` (Android). Empty when Firebase
        /// isn't configured.
        /// </summary>
        string GetFirebaseProjectId();

        /// <summary>iOS: count of `SKAdNetworkItems` in Info.plist. -1 elsewhere.</summary>
        int GetSkAdNetworksCount();

        /// <summary>Android: count of requested permissions. -1 elsewhere.</summary>
        int GetAndroidPermissionsCount();
    }

    public interface INativeMaintenance
    {
        /// <summary>
        /// Wipes platform HTTP caches:
        ///   * iOS — <c>URLCache.shared.removeAllCachedResponses()</c> and
        ///     <c>WKWebsiteDataStore.default().removeData(...)</c> for disk cache.
        ///   * Android — <c>WebView(activity).clearCache(true)</c> and
        ///     recursive delete of <c>context.cacheDir</c> contents.
        /// Always synchronous and never throws — failures are logged on the
        /// native side and surfaced as a no-op return.
        /// </summary>
        void ClearNativeHttpCache();
    }

    /// <summary>
    /// Aggregate native plugin interface — inherits all domain-specific sub-interfaces.
    /// Platform implementations (AndroidPlugin, IosPlugin, DefaultNativePlugin) implement this.
    /// </summary>
    public interface INativePlugin : INativeTracker, INativeIAP, INativeAccountStore,
        INativeDatePicker, INativeFirebase, INativeEventStorage, INativeLifecycle,
        INativeAppManagement, INativeLogStream, INativeDeviceMetrics,
        INativeMaintenance, INativeBuildInfo
    {
    }
}
