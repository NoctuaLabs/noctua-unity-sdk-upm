using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace com.noctuagames.sdk.Editor.Build
{
    /// <summary>
    /// Injects the game's push-notification small icon into the generated Android project.
    /// </summary>
    /// <remarks>
    /// Android draws notification small icons from the alpha channel only, so a full-colour
    /// launcher icon renders as a white square. The icon is chosen in this order:
    /// <list type="number">
    /// <item><see cref="SourceAssetPath"/> — a white-on-transparent silhouette the game provides.</item>
    /// <item>A silhouette derived from the game's Unity app icon (Player Settings), when it can be
    /// made cleanly: the icon's own transparency, or its logo against a solid background.</item>
    /// <item>Nothing injected — the native SDK's default bell is used.</item>
    /// </list>
    /// The chosen icon is copied into the launcher's <c>res/drawable</c> and registered as
    /// <c>com.google.firebase.messaging.default_notification_icon</c>, which covers both FCM tray
    /// notifications and the SDK's foreground notifications.
    /// </remarks>
    public static class AndroidNotificationIconInjector
    {
        public const string SourceAssetPath = "Assets/Noctua/notification_icon.png";
        public const string DrawableName = "noctua_game_notification_icon";
        public const string MetaDataName = "com.google.firebase.messaging.default_notification_icon";

        // 24dp at xxxhdpi.
        public const int SilhouetteSize = 96;

        private static readonly XNamespace AndroidNs = "http://schemas.android.com/apk/res/android";
        private static readonly XNamespace ToolsNs = "http://schemas.android.com/tools";

        // Alpha at or above this counts as opaque; a silhouette needs transparent pixels.
        private const byte OpaqueAlpha = 250;

        // Derivation limits: an icon is "shaped" when at least this share of pixels is transparent.
        private const float ShapedIconTransparentShare = 0.05f;
        // Corners must agree on the background within this per-channel difference.
        private const int BackgroundTolerance = 40;
        // The logo must stand out from the background by at least this much.
        private const int MinimumContrast = 60;
        // A clean logo covers neither almost nothing nor almost everything.
        private const float MinCoverage = 0.03f;
        private const float MaxCoverage = 0.6f;
        // Empty margin around the logo, as a share of the icon on each side.
        private const float Padding = 0.08f;

        /// <summary>Injects the icon using the game's Player Settings app icon as fallback.</summary>
        public static void Apply(string gradleRootPath, string unityProjectPath) =>
            Apply(gradleRootPath, unityProjectPath, LoadAppIconPng());

        /// <summary>
        /// Injects <see cref="SourceAssetPath"/> when present, else a silhouette derived from
        /// <paramref name="appIconPng"/>. Leaves the manifest untouched (SDK bell) when neither works.
        /// </summary>
        public static void Apply(string gradleRootPath, string unityProjectPath, byte[] appIconPng)
        {
            var iconPng = LoadProvidedIcon(unityProjectPath) ?? DeriveFromAppIcon(appIconPng);
            if (iconPng == null) return;

            var launcherMain = Path.Combine(gradleRootPath, "launcher", "src", "main");
            var drawableDir = Path.Combine(launcherMain, "res", "drawable");
            Directory.CreateDirectory(drawableDir);
            File.WriteAllBytes(Path.Combine(drawableDir, DrawableName + ".png"), iconPng);

            var manifestPath = Path.Combine(launcherMain, "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogError($"[Noctua] AndroidManifest.xml not found at {manifestPath}; notification icon not set.");
                return;
            }

            var manifest = XDocument.Load(manifestPath);
            if (!SetNotificationIconMetaData(manifest, DrawableName))
            {
                Debug.LogError("[Noctua] <application> not found in launcher AndroidManifest.xml; notification icon not set.");
                return;
            }

            manifest.Save(manifestPath);
        }

        private static byte[] LoadProvidedIcon(string unityProjectPath)
        {
            var sourcePath = Path.Combine(unityProjectPath, SourceAssetPath);
            if (!File.Exists(sourcePath)) return null;

            var pngBytes = File.ReadAllBytes(sourcePath);
            if (!HasTransparency(pngBytes))
            {
                Debug.LogWarning($"[Noctua] {SourceAssetPath} has no transparent pixels, so Android will draw it " +
                                 "as a white square. Use a white silhouette on a transparent background.");
            }

            Debug.Log($"[Noctua] Push notification icon set from {SourceAssetPath}.");
            return pngBytes;
        }

        private static byte[] DeriveFromAppIcon(byte[] appIconPng)
        {
            var silhouette = appIconPng == null ? null : CreateSilhouettePng(appIconPng);
            if (silhouette == null)
            {
                Debug.LogWarning("[Noctua] Could not derive a clean push notification icon from the app icon; " +
                                 "using the SDK's default icon. Add a white-on-transparent logo at " +
                                 $"{SourceAssetPath} to use your own.");
                return null;
            }

            Debug.Log("[Noctua] Push notification icon derived from the app icon. " +
                      $"Add {SourceAssetPath} to control it exactly.");
            return silhouette;
        }

        /// <summary>
        /// Raw bytes of the largest Android (else default) Player Settings icon, or null when the
        /// project has none or it is not a PNG/JPG file.
        /// </summary>
        private static byte[] LoadAppIconPng()
        {
            var icons = PlayerSettings.GetIcons(NamedBuildTarget.Android, IconKind.Application);
            if (icons == null || icons.All(icon => icon == null))
            {
                icons = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            }

            var texture = icons?.Where(icon => icon != null).OrderByDescending(icon => icon.width).FirstOrDefault();
            var assetPath = texture == null ? null : AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath)) return null;

            return File.ReadAllBytes(assetPath);
        }

        /// <summary>
        /// Makes a <see cref="SilhouetteSize"/>-pixel white-on-transparent icon from an app icon
        /// image (PNG/JPG bytes), or returns null when no clean silhouette can be made.
        /// </summary>
        public static byte[] CreateSilhouettePng(byte[] iconBytes)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!source.LoadImage(iconBytes)) return null;

                var alpha = ExtractLogoAlpha(source.GetPixels32(), source.width, source.height);
                if (alpha == null) return null;

                var icon = FitToSquare(alpha, source.width, source.height, SilhouetteSize);
                return EncodeWhiteSilhouette(icon, SilhouetteSize);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        /// <summary>
        /// Per-pixel logo coverage (0..1): the icon's own alpha when it is shaped, else contrast
        /// against a solid background read from the corners. Null when it fails the quality gate.
        /// </summary>
        public static float[] ExtractLogoAlpha(Color32[] pixels, int width, int height)
        {
            var count = width * height;
            if (count == 0 || pixels.Length != count) return null;

            var alpha = new float[count];
            var transparent = pixels.Count(pixel => pixel.a < 128);

            if (transparent >= count * ShapedIconTransparentShare)
            {
                for (var i = 0; i < count; i++) alpha[i] = pixels[i].a / 255f;
            }
            else
            {
                Color32[] corners =
                {
                    pixels[0], pixels[width - 1], pixels[(height - 1) * width], pixels[count - 1],
                };
                var background = new Color32(
                    (byte)corners.Average(c => c.r), (byte)corners.Average(c => c.g), (byte)corners.Average(c => c.b), 255);
                if (corners.Any(corner => Difference(corner, background) > BackgroundTolerance)) return null;

                var contrast = pixels.Select(pixel => Difference(pixel, background)).ToArray();
                var maxContrast = contrast.Max();
                if (maxContrast < MinimumContrast) return null;

                // Soft edge between faint (15%) and clear (50%) contrast keeps anti-aliasing.
                float low = maxContrast * 0.15f, high = maxContrast * 0.5f;
                for (var i = 0; i < count; i++) alpha[i] = Mathf.Clamp01((contrast[i] - low) / (high - low));
            }

            var coverage = alpha.Count(a => a > 0.5f) / (float)count;
            return coverage < MinCoverage || coverage > MaxCoverage ? null : alpha;
        }

        private static int Difference(Color32 a, Color32 b) =>
            Math.Max(Math.Abs(a.r - b.r), Math.Max(Math.Abs(a.g - b.g), Math.Abs(a.b - b.b)));

        /// <summary>Crops to the logo, centres it with padding and box-scales to size×size.</summary>
        private static float[] FitToSquare(float[] alpha, int width, int height, int size)
        {
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if (alpha[y * width + x] <= 0.1f) continue;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }

            int logoWidth = maxX - minX + 1, logoHeight = maxY - minY + 1;
            var side = Math.Max(logoWidth, logoHeight) / (1f - 2f * Padding);
            var originX = minX - (side - logoWidth) / 2f;
            var originY = minY - (side - logoHeight) / 2f;
            var scale = side / size;

            var result = new float[size * size];
            for (var ty = 0; ty < size; ty++)
            for (var tx = 0; tx < size; tx++)
            {
                int x0 = (int)Math.Floor(originX + tx * scale), x1 = (int)Math.Ceiling(originX + (tx + 1) * scale);
                int y0 = (int)Math.Floor(originY + ty * scale), y1 = (int)Math.Ceiling(originY + (ty + 1) * scale);
                float sum = 0;
                var samples = 0;
                for (var sy = y0; sy < y1; sy++)
                for (var sx = x0; sx < x1; sx++)
                {
                    samples++;
                    if (sx >= 0 && sx < width && sy >= 0 && sy < height) sum += alpha[sy * width + sx];
                }

                result[ty * size + tx] = samples == 0 ? 0 : sum / samples;
            }

            return result;
        }

        private static byte[] EncodeWhiteSilhouette(float[] alpha, int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(alpha.Select(a => new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255))).ToArray());
                return texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// Points <see cref="MetaDataName"/> at <c>@drawable/{drawableName}</c>, replacing any
        /// existing entry. <c>tools:replace</c> lets it win over the native SDK's default.
        /// Returns false when the manifest has no <c>application</c> element.
        /// </summary>
        public static bool SetNotificationIconMetaData(XDocument manifest, string drawableName)
        {
            var root = manifest.Root;
            var application = root?.Element("application");
            if (application == null) return false;

            if (root.Attribute(XNamespace.Xmlns + "tools") == null)
            {
                root.Add(new XAttribute(XNamespace.Xmlns + "tools", ToolsNs.NamespaceName));
            }

            application.Elements("meta-data")
                .Where(e => (string)e.Attribute(AndroidNs + "name") == MetaDataName)
                .ToList()
                .ForEach(e => e.Remove());

            application.Add(new XElement("meta-data",
                new XAttribute(AndroidNs + "name", MetaDataName),
                new XAttribute(AndroidNs + "resource", "@drawable/" + drawableName),
                new XAttribute(ToolsNs + "replace", "android:resource")));

            return true;
        }

        /// <summary>True when the PNG has at least one non-opaque pixel.</summary>
        public static bool HasTransparency(byte[] pngBytes)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(pngBytes)) return false;
                return texture.GetPixels32().Any(pixel => pixel.a < OpaqueAlpha);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
