#if UNITY_IOS
using System.Linq;
using NUnit.Framework;
using UnityEditor.iOS.Xcode;

namespace com.noctuagames.sdk.Editor.Tests
{
    public class IosBackgroundModesTest
    {
        private static string[] Modes(PlistDocument plist) =>
            plist.root["UIBackgroundModes"].AsArray().values.Select(v => v.AsString()).ToArray();

        [Test]
        public void EnsureRemoteNotification_NoBackgroundModes_AddsIt()
        {
            var plist = new PlistDocument();

            Assert.IsTrue(NoctuaIosBuildProcessor.EnsureRemoteNotificationBackgroundMode(plist.root));

            CollectionAssert.AreEqual(new[] { "remote-notification" }, Modes(plist));
        }

        [Test]
        public void EnsureRemoteNotification_KeepsExistingModes()
        {
            var plist = new PlistDocument();
            var modes = plist.root.CreateArray("UIBackgroundModes");
            modes.AddString("audio");
            modes.AddString("fetch");

            Assert.IsTrue(NoctuaIosBuildProcessor.EnsureRemoteNotificationBackgroundMode(plist.root));

            CollectionAssert.AreEqual(new[] { "audio", "fetch", "remote-notification" }, Modes(plist));
        }

        [Test]
        public void EnsureRemoteNotification_AlreadyPresent_DoesNotDuplicate()
        {
            var plist = new PlistDocument();
            plist.root.CreateArray("UIBackgroundModes").AddString("remote-notification");

            Assert.IsFalse(NoctuaIosBuildProcessor.EnsureRemoteNotificationBackgroundMode(plist.root));

            CollectionAssert.AreEqual(new[] { "remote-notification" }, Modes(plist));
        }

        [Test]
        public void EnsureRemoteNotification_SurvivesPlistRoundTrip()
        {
            var plist = new PlistDocument();
            NoctuaIosBuildProcessor.EnsureRemoteNotificationBackgroundMode(plist.root);

            var reloaded = new PlistDocument();
            reloaded.ReadFromString(plist.WriteToString());

            Assert.IsFalse(NoctuaIosBuildProcessor.EnsureRemoteNotificationBackgroundMode(reloaded.root));
            CollectionAssert.AreEqual(new[] { "remote-notification" }, Modes(reloaded));
        }

        [Test]
        public void EnsureRemoteNotification_NonArrayValue_IsReplacedWithArray()
        {
            var plist = new PlistDocument();
            plist.root.SetString("UIBackgroundModes", "remote-notification");

            Assert.IsTrue(NoctuaIosBuildProcessor.EnsureRemoteNotificationBackgroundMode(plist.root));

            CollectionAssert.AreEqual(new[] { "remote-notification" }, Modes(plist));
        }
    }
}
#endif
