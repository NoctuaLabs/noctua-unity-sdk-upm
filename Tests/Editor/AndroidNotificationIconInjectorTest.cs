using System.IO;
using System.Linq;
using System.Xml.Linq;
using com.noctuagames.sdk.Editor.Build;
using NUnit.Framework;
using UnityEngine;

namespace com.noctuagames.sdk.Editor.Tests
{
    public class AndroidNotificationIconInjectorTest
    {
        private static readonly XNamespace AndroidNs = "http://schemas.android.com/apk/res/android";
        private static readonly XNamespace ToolsNs = "http://schemas.android.com/tools";

        private const string Manifest =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\">\n" +
            "  <application android:icon=\"@mipmap/app_icon\">\n" +
            "    <meta-data android:name=\"other\" android:value=\"keep\" />\n" +
            "  </application>\n" +
            "</manifest>";

        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "noctua-icon-test-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }

        private static XElement[] IconMetaData(XDocument doc) =>
            doc.Root!.Element("application")!.Elements("meta-data")
                .Where(e => (string)e.Attribute(AndroidNs + "name") == AndroidNotificationIconInjector.MetaDataName)
                .ToArray();

        private static byte[] Png(Color32 fill)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            texture.SetPixels32(Enumerable.Repeat(fill, 16).ToArray());
            var bytes = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return bytes;
        }

        [Test]
        public void SetMetaData_AddsIconWithToolsReplace()
        {
            var doc = XDocument.Parse(Manifest);

            Assert.IsTrue(AndroidNotificationIconInjector.SetNotificationIconMetaData(doc, "my_icon"));

            var meta = IconMetaData(doc).Single();
            Assert.AreEqual("@drawable/my_icon", (string)meta.Attribute(AndroidNs + "resource"));
            Assert.AreEqual("android:resource", (string)meta.Attribute(ToolsNs + "replace"));
            Assert.IsNotNull(doc.Root!.Attribute(XNamespace.Xmlns + "tools"), "tools namespace must be declared");
        }

        [Test]
        public void SetMetaData_ReplacesExistingEntryAndKeepsOthers()
        {
            var doc = XDocument.Parse(Manifest);
            AndroidNotificationIconInjector.SetNotificationIconMetaData(doc, "old_icon");

            AndroidNotificationIconInjector.SetNotificationIconMetaData(doc, "new_icon");

            Assert.AreEqual("@drawable/new_icon", (string)IconMetaData(doc).Single().Attribute(AndroidNs + "resource"));
            Assert.AreEqual(2, doc.Root!.Element("application")!.Elements("meta-data").Count());
        }

        [Test]
        public void SetMetaData_NoApplication_ReturnsFalse()
        {
            var doc = XDocument.Parse("<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" />");

            Assert.IsFalse(AndroidNotificationIconInjector.SetNotificationIconMetaData(doc, "my_icon"));
        }

        [Test]
        public void HasTransparency_DetectsSilhouetteVsOpaqueIcon()
        {
            Assert.IsTrue(AndroidNotificationIconInjector.HasTransparency(Png(new Color32(255, 255, 255, 0))));
            Assert.IsFalse(AndroidNotificationIconInjector.HasTransparency(Png(new Color32(30, 30, 30, 255))));
            Assert.IsFalse(AndroidNotificationIconInjector.HasTransparency(new byte[] { 1, 2, 3 }));
        }

        [Test]
        public void Apply_CopiesIconAndPatchesManifest()
        {
            var unityProject = Path.Combine(_tempDir, "unity");
            var gradleRoot = Path.Combine(_tempDir, "gradle");
            var source = Path.Combine(unityProject, AndroidNotificationIconInjector.SourceAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            File.WriteAllBytes(source, Png(new Color32(255, 255, 255, 0)));
            var manifestPath = Path.Combine(gradleRoot, "launcher", "src", "main", "AndroidManifest.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
            File.WriteAllText(manifestPath, Manifest);

            AndroidNotificationIconInjector.Apply(gradleRoot, unityProject, LogoOnDarkIcon());

            var copied = Path.Combine(gradleRoot, "launcher", "src", "main", "res", "drawable",
                AndroidNotificationIconInjector.DrawableName + ".png");
            Assert.IsTrue(File.Exists(copied));
            Assert.AreEqual(1, IconMetaData(XDocument.Load(manifestPath)).Length);
        }

        [Test]
        public void Apply_NoSourceIcon_NoAppIcon_LeavesManifestUntouched()
        {
            var manifestPath = WriteManifest(out var gradleRoot);

            AndroidNotificationIconInjector.Apply(gradleRoot, Path.Combine(_tempDir, "unity"), null);

            Assert.AreEqual(Manifest, File.ReadAllText(manifestPath));
        }

        [Test]
        public void Apply_NoSourceIcon_DerivesIconFromAppIcon()
        {
            var manifestPath = WriteManifest(out var gradleRoot);

            AndroidNotificationIconInjector.Apply(gradleRoot, Path.Combine(_tempDir, "unity"), LogoOnDarkIcon());

            Assert.IsTrue(File.Exists(DrawablePath(gradleRoot)));
            Assert.AreEqual(1, IconMetaData(XDocument.Load(manifestPath)).Length);
        }

        [Test]
        public void Apply_NoSourceIcon_UnusableAppIcon_LeavesManifestUntouched()
        {
            var manifestPath = WriteManifest(out var gradleRoot);

            AndroidNotificationIconInjector.Apply(gradleRoot, Path.Combine(_tempDir, "unity"), Png(new Color32(200, 40, 40, 255)));

            Assert.AreEqual(Manifest, File.ReadAllText(manifestPath));
            Assert.IsFalse(File.Exists(DrawablePath(gradleRoot)));
        }

        // ─── Silhouette derivation ─────────────────────────────────────────────

        [Test]
        public void CreateSilhouette_LogoOnSolidBackground_KeepsLogoDropsBackground()
        {
            var silhouette = Decode(AndroidNotificationIconInjector.CreateSilhouettePng(LogoOnDarkIcon()));

            Assert.AreEqual(AndroidNotificationIconInjector.SilhouetteSize, silhouette.width);
            Assert.AreEqual(0, silhouette.GetPixel(0, 0).a, 0.01f, "background corner must be transparent");
            Assert.Greater(silhouette.GetPixel(48, 48).a, 0.9f, "logo centre must be opaque");
            Assert.AreEqual(Color.white.r, silhouette.GetPixel(48, 48).r, 0.01f);
            Object.DestroyImmediate(silhouette);
        }

        [Test]
        public void CreateSilhouette_ShapedTransparentIcon_UsesItsAlpha()
        {
            var silhouette = Decode(AndroidNotificationIconInjector.CreateSilhouettePng(
                Icon(64, (x, y) => Inside(x, y, 24, 40) ? new Color32(220, 30, 30, 255) : new Color32(0, 0, 0, 0))));

            Assert.AreEqual(0, silhouette.GetPixel(0, 0).a, 0.01f);
            Assert.Greater(silhouette.GetPixel(48, 48).a, 0.9f);
            Object.DestroyImmediate(silhouette);
        }

        [Test]
        public void CreateSilhouette_SolidColour_ReturnsNull()
        {
            Assert.IsNull(AndroidNotificationIconInjector.CreateSilhouettePng(Png(new Color32(30, 30, 30, 255))));
        }

        [Test]
        public void CreateSilhouette_BusyFullBleedArt_ReturnsNull()
        {
            var random = new System.Random(7);
            var noise = Icon(64, (x, y) => new Color32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), 255));

            Assert.IsNull(AndroidNotificationIconInjector.CreateSilhouettePng(noise));
        }

        [Test]
        public void CreateSilhouette_ShapeFillingTheIcon_ReturnsNull()
        {
            var almostFull = Icon(64, (x, y) => Inside(x, y, 2, 62) ? new Color32(30, 120, 220, 255) : new Color32(0, 0, 0, 0));

            Assert.IsNull(AndroidNotificationIconInjector.CreateSilhouettePng(almostFull));
        }

        [Test]
        public void CreateSilhouette_InvalidBytes_ReturnsNull()
        {
            Assert.IsNull(AndroidNotificationIconInjector.CreateSilhouettePng(new byte[] { 1, 2, 3 }));
        }

        // ─── Helpers ───────────────────────────────────────────────────────────

        private string WriteManifest(out string gradleRoot)
        {
            gradleRoot = Path.Combine(_tempDir, "gradle");
            var manifestPath = Path.Combine(gradleRoot, "launcher", "src", "main", "AndroidManifest.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
            File.WriteAllText(manifestPath, Manifest);
            return manifestPath;
        }

        private static string DrawablePath(string gradleRoot) =>
            Path.Combine(gradleRoot, "launcher", "src", "main", "res", "drawable",
                AndroidNotificationIconInjector.DrawableName + ".png");

        private static bool Inside(int x, int y, int min, int max) => x >= min && x < max && y >= min && y < max;

        // Opaque icon like the sample app's: a white logo on a solid dark background.
        private static byte[] LogoOnDarkIcon() =>
            Icon(64, (x, y) => Inside(x, y, 20, 44) ? new Color32(240, 240, 240, 255) : new Color32(34, 34, 34, 255));

        private static byte[] Icon(int size, System.Func<int, int, Color32> pixel)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                pixels[y * size + x] = pixel(x, y);
            texture.SetPixels32(pixels);
            var bytes = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return bytes;
        }

        private static Texture2D Decode(byte[] png)
        {
            Assert.IsNotNull(png, "expected a silhouette");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(texture.LoadImage(png));
            return texture;
        }
    }
}
