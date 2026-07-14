using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Holds the current FCM registration token and keeps it fresh, so that every outgoing
    /// <c>HttpRequest</c> can stamp it onto the <c>X-FCM-TOKEN</c> header without doing any
    /// work of its own.
    /// </summary>
    /// <remarks>
    /// The token is not a stable one-shot value:
    /// <list type="bullet">
    /// <item>On iOS it does not exist until the user grants notification permission — which may
    /// happen long after init, or never.</item>
    /// <item>It rotates on reinstall, app-data clear, device restore, and periodic refresh.</item>
    /// </list>
    /// So the value is cached and re-asserted from three sources: an initial retry loop after
    /// init, the native token-refresh callback (iOS), and a re-fetch on app resume (which is how
    /// Android learns about rotation — it has no <c>onNewToken</c> bridge into Unity).
    /// </remarks>
    public class FcmTokenRegistrar
    {
        private const int InitialFetchAttempts = 6;
        private const int InitialFetchRetryDelayMs = 2000;

        private readonly ILogger _log = new NoctuaLogger(typeof(FcmTokenRegistrar));
        private readonly Func<UniTask<string>> _fetchToken;
        private readonly bool _isSandbox;

        // Serializes fetches: IosPlugin stores a single static callback slot per native call, so
        // two GetFirebaseMessagingToken() calls in flight at once leave all but the last hanging.
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        private volatile string _token = string.Empty;

        /// <summary>
        /// The most recent non-empty token seen, or an empty string if none has arrived yet.
        /// Safe to read from any thread; never blocks.
        /// </summary>
        public string Current => _token;

        /// <param name="fetchToken">Fetches a token from the native plugin. Injected so tests can
        /// drive the registrar without a device.</param>
        /// <param name="isSandbox">When true, the acquired token is logged for QA to copy into
        /// backend push tests. Never enabled in production — a token in release logs is scrapeable.</param>
        public FcmTokenRegistrar(Func<UniTask<string>> fetchToken, bool isSandbox)
        {
            _fetchToken = fetchToken ?? throw new ArgumentNullException(nameof(fetchToken));
            _isSandbox = isSandbox;
        }

        /// <summary>
        /// Records a token pushed to us by the native refresh callback.
        /// </summary>
        public void Accept(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                // An empty value means "not available yet", not "revoked" — keep the good one.
                return;
            }

            if (string.Equals(token, _token, StringComparison.Ordinal))
            {
                return;
            }

            var isFirst = string.IsNullOrEmpty(_token);
            _token = token;

            _log.Info(isFirst ? "FCM token acquired" : "FCM token rotated");

            if (_isSandbox)
            {
                _log.Info($"[sandbox] FCM token: {token}");
            }
        }

        /// <summary>
        /// Fetches the current token from the native plugin once and records it. Concurrent calls
        /// are serialized; a failure or an empty result leaves any previously-held token intact.
        /// </summary>
        public async UniTask RefreshAsync()
        {
            await _refreshLock.WaitAsync();

            try
            {
                Accept(await _fetchToken());
            }
            catch (Exception e)
            {
                _log.Warning($"FCM token fetch failed: {e.Message}");
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        /// <summary>
        /// Called after init. The iOS APNs ↔ FCM handshake usually completes within a few seconds
        /// when permission was granted on a previous run; on Android the token is normally ready
        /// immediately. Caps at ~12 s so a permanently-unavailable token never becomes a
        /// long-lived background task.
        /// </summary>
        public async UniTaskVoid StartInitialFetch()
        {
            for (var attempt = 1; attempt <= InitialFetchAttempts; attempt++)
            {
                await RefreshAsync();

                if (!string.IsNullOrEmpty(_token))
                {
                    return;
                }

                if (attempt < InitialFetchAttempts)
                {
                    await UniTask.Delay(InitialFetchRetryDelayMs);
                }
            }

            _log.Warning("FCM token still unavailable after retries — check notification permission " +
                         "grant, APNs entitlement, or Firebase Messaging library link. Requests will " +
                         "omit X-FCM-TOKEN until a token arrives.");
        }

        /// <summary>
        /// Re-checks the token when the app returns to the foreground. This is how Android picks up
        /// a rotated token: <c>AndroidPlugin.SetFirebaseMessagingTokenRefreshHandler</c> is a no-op,
        /// so there is no push-based rotation signal on that platform.
        /// </summary>
        public void OnApplicationResume()
        {
            RefreshAsync().Forget();
        }
    }
}
