#if UNITY_IOS
using System.Linq;
using com.noctuagames.sdk.Editor.Build;
using NUnit.Framework;
using UnityEditor.iOS.Xcode;

namespace com.noctuagames.sdk.Editor.Tests
{
    public class IosBackgroundModesTest
    {
        private static string[] Modes(PlistDocument plist) =>
            plist.root[IosBackgroundModes.Key].AsArray().values.Select(v => v.AsString()).ToArray();

        [Test]
        public void EnsureRemoteNotification_NoBackgroundModes_AddsIt()
        {
            var plist = new PlistDocument();

            Assert.IsTrue(IosBackgroundModes.EnsureRemoteNotification(plist.root));

            CollectionAssert.AreEqual(new[] { "remote-notification" }, Modes(plist));
        }

        [Test]
        public void EnsureRemoteNotification_KeepsExistingModes()
        {
            var plist = new PlistDocument();
            var modes = plist.root.CreateArray(IosBackgroundModes.Key);
            modes.AddString("audio");
            modes.AddString("fetch");

            Assert.IsTrue(IosBackgroundModes.EnsureRemoteNotification(plist.root));

            CollectionAssert.AreEqual(new[] { "audio", "fetch", "remote-notification" }, Modes(plist));
        }

        [Test]
        public void EnsureRemoteNotification_AlreadyPresent_DoesNotDuplicate()
        {
            var plist = new PlistDocument();
            plist.root.CreateArray(IosBackgroundModes.Key).AddString("remote-notification");

            Assert.IsFalse(IosBackgroundModes.EnsureRemoteNotification(plist.root));

            CollectionAssert.AreEqual(new[] { "remote-notification" }, Modes(plist));
        }

        [Test]
        public void EnsureRemoteNotification_SurvivesPlistRoundTrip()
        {
            var plist = new PlistDocument();
            IosBackgroundModes.EnsureRemoteNotification(plist.root);

            var reloaded = new PlistDocument();
            reloaded.ReadFromString(plist.WriteToString());

            Assert.IsFalse(IosBackgroundModes.EnsureRemoteNotification(reloaded.root));
            CollectionAssert.AreEqual(new[] { "remote-notification" }, Modes(reloaded));
        }

        [Test]
        public void EnsureRemoteNotification_NonArrayValue_IsReplacedWithArray()
        {
            var plist = new PlistDocument();
            plist.root.SetString(IosBackgroundModes.Key, "remote-notification");

            Assert.IsTrue(IosBackgroundModes.EnsureRemoteNotification(plist.root));

            CollectionAssert.AreEqual(new[] { "remote-notification" }, Modes(plist));
        }
    }
}
#endif
