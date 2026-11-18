using System;
using System.IO;
using UnityEngine;

namespace com.noctuagames.sdk.Inspector
{
    /// <summary>
    /// Locates the current Firebase project id at runtime so the Inspector's
    /// "Open in DebugView" button can link directly to
    /// <c>console.firebase.google.com/project/&lt;id&gt;/analytics/debugview</c>.
    ///
    /// On iOS we read <c>GoogleService-Info.plist</c> (copied into the app
    /// bundle by the Unity Firebase integration or by
    /// <c>BuildPostProcessor</c>); on Android we read
    /// <c>google-services.json</c>. Both files are plain-text and tiny;
    /// we parse them with a cheap regex so the Inspector doesn't pull in
    /// extra deps. Returns <c>null</c> when the project id can't be found.
    /// </summary>
    public static class FirebaseProjectLookup
    {
        private static readonly ILogger _log = new NoctuaLogger(typeof(FirebaseProjectLookup));

        private static string _cachedId;
        private static bool _resolved;

        public static string GetProjectId()
        {
            if (_resolved) return _cachedId;
            _resolved = true;

            try
            {
                _cachedId = ReadFromStreamingAssets() ?? ReadFromDataPath();
            }
            catch (Exception e)
            {
                _log.Warning($"FirebaseProjectLookup failed: {e.Message}");
            }
            return _cachedId;
        }

        private static string ReadFromStreamingAssets()
        {
            // Prefer the canonical sources shipped into StreamingAssets.
            try
            {
                var iosPlist = Path.Combine(Application.streamingAssetsPath, "GoogleService-Info.plist");
                if (File.Exists(iosPlist))
                {
                    var id = ExtractPlistValue(File.ReadAllText(iosPlist), "PROJECT_ID");
                    if (!string.IsNullOrEmpty(id)) return id;
                }
                var androidJson = Path.Combine(Application.streamingAssetsPath, "google-services.json");
                if (File.Exists(androidJson))
                {
                    var id = ExtractJsonField(File.ReadAllText(androidJson), "project_id");
                    if (!string.IsNullOrEmpty(id)) return id;
                }
            }
            catch { /* fallthrough */ }
            return null;
        }

        private static string ReadFromDataPath()
        {
            // Fallback: iOS bundle root on device / editor install.
            var plistInDataPath = Path.Combine(Application.dataPath, "GoogleService-Info.plist");
            if (File.Exists(plistInDataPath))
            {
                return ExtractPlistValue(File.ReadAllText(plistInDataPath), "PROJECT_ID");
            }
            return null;
        }

        // Naive plist `<key>PROJECT_ID</key><string>X</string>` extractor.
        private static string ExtractPlistValue(string content, string key)
        {
            if (string.IsNullOrEmpty(content)) return null;
            var anchor = "<key>" + key + "</key>";
            var idx = content.IndexOf(anchor, StringComparison.Ordinal);
            if (idx < 0) return null;
            var openTag = content.IndexOf("<string>", idx, StringComparison.Ordinal);
            if (openTag < 0) return null;
            var closeTag = content.IndexOf("</string>", openTag, StringComparison.Ordinal);
            if (closeTag < 0) return null;
            var start = openTag + "<string>".Length;
            return content.Substring(start, closeTag - start);
        }

        private static string ExtractJsonField(string content, string field)
        {
            if (string.IsNullOrEmpty(content)) return null;
            var anchor = "\"" + field + "\"";
            var idx = content.IndexOf(anchor, StringComparison.Ordinal);
            if (idx < 0) return null;
            var colon = content.IndexOf(':', idx);
            if (colon < 0) return null;
            var quoteStart = content.IndexOf('"', colon);
            if (quoteStart < 0) return null;
            var quoteEnd = content.IndexOf('"', quoteStart + 1);
            if (quoteEnd < 0) return null;
            return content.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
        }
    }
}
