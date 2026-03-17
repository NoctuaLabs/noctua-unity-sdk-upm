using System;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

namespace com.noctuagames.sdk
{
    public class NoctuaAppManager
    {
        private readonly ILogger _log = new NoctuaLogger(typeof(NoctuaAppManager));
        private readonly INativePlugin _nativePlugin;

        internal NoctuaAppManager(INativePlugin nativePlugin)
        {
            _nativePlugin = nativePlugin;
        }

        /// <summary>
        /// Requests the native in-app review dialog.
        /// The OS decides whether to actually show the prompt (rate-limited).
        /// </summary>
        public UniTask RequestInAppReview()
        {
            var tcs = new UniTaskCompletionSource();

            _nativePlugin.RequestInAppReview(success =>
            {
                tcs.TrySetResult();
            });

            return tcs.Task;
        }

        /// <summary>
        /// Checks if an app update is available (Android only).
        /// Returns empty info with IsUpdateAvailable=false on unsupported platforms.
        /// </summary>
        public UniTask<AppUpdateInfo> CheckForUpdate()
        {
            var tcs = new UniTaskCompletionSource<AppUpdateInfo>();

            _nativePlugin.CheckForUpdate(json =>
            {
                try
                {
                    var info = JsonConvert.DeserializeObject<AppUpdateInfo>(json) ?? new AppUpdateInfo();
                    tcs.TrySetResult(info);
                }
                catch (Exception e)
                {
                    _log.Warning("Failed to parse update info: " + e.Message);
                    tcs.TrySetResult(new AppUpdateInfo());
                }
            });

            return tcs.Task;
        }

        /// <summary>
        /// Starts an immediate (blocking) app update flow (Android only).
        /// Returns NotAvailable on unsupported platforms.
        /// </summary>
        public UniTask<AppUpdateResult> StartImmediateUpdate()
        {
            var tcs = new UniTaskCompletionSource<AppUpdateResult>();

            _nativePlugin.StartImmediateUpdate(resultCode =>
            {
                tcs.TrySetResult((AppUpdateResult)resultCode);
            });

            return tcs.Task;
        }

        /// <summary>
        /// Starts a flexible (background download) app update (Android only).
        /// Returns NotAvailable on unsupported platforms.
        /// </summary>
        /// <param name="onProgress">Optional callback with download progress (0.0 to 1.0).</param>
        public UniTask<AppUpdateResult> StartFlexibleUpdate(Action<float> onProgress = null)
        {
            var tcs = new UniTaskCompletionSource<AppUpdateResult>();

            _nativePlugin.StartFlexibleUpdate(
                progress => onProgress?.Invoke(progress),
                resultCode => tcs.TrySetResult((AppUpdateResult)resultCode)
            );

            return tcs.Task;
        }

        /// <summary>
        /// Completes a previously downloaded flexible update by installing it.
        /// The app will restart after this call.
        /// </summary>
        public void CompleteUpdate()
        {
            _nativePlugin.CompleteUpdate();
        }
    }
}
