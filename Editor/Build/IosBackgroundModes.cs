#if UNITY_IOS
using System.Linq;
using UnityEditor.iOS.Xcode;

namespace com.noctuagames.sdk.Editor.Build
{
    /// <summary>
    /// Ensures the <c>UIBackgroundModes</c> entries the SDK's iOS code relies on.
    /// </summary>
    /// <remarks>
    /// <c>CustomAppController</c> implements
    /// <c>application:didReceiveRemoteNotification:fetchCompletionHandler:</c> so silent / data pushes
    /// reach <c>Noctua.OnRemoteNotificationReceived</c> (and are tracked as
    /// <c>push_notification_received</c>). iOS only wakes the app for them when
    /// <c>remote-notification</c> is a declared background mode.
    /// </remarks>
    public static class IosBackgroundModes
    {
        public const string Key = "UIBackgroundModes";
        public const string RemoteNotification = "remote-notification";

        /// <summary>
        /// Adds <see cref="RemoteNotification"/> to <see cref="Key"/>, keeping existing modes.
        /// Returns false when it was already present.
        /// </summary>
        public static bool EnsureRemoteNotification(PlistElementDict root)
        {
            var modes = root.values.TryGetValue(Key, out var existing) && existing is PlistElementArray array
                ? array
                : root.CreateArray(Key);

            if (modes.values.Any(mode => mode is PlistElementString text && text.value == RemoteNotification))
            {
                return false;
            }

            modes.AddString(RemoteNotification);
            return true;
        }
    }
}
#endif
