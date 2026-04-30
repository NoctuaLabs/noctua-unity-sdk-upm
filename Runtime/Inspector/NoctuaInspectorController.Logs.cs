using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.Inspector
{
    /// <summary>
    /// "Logs" tab — verbose log viewer with level/source/text filters,
    /// regex support, copy-row, and export-to-file.
    ///
    /// Performance budget: rendering all 5,000 entries every frame is
    /// untenable; the controller only re-renders on dirty (new entry
    /// admitted) and we cap visible rows at <see cref="LogTabRowBudget"/>.
    /// Future work: row virtualization via <c>ListView</c> when the entry
    /// count justifies it.
    /// </summary>
    public partial class NoctuaInspectorController
    {
        private const int LogTabRowBudget = 300;

        // UI control state — kept here so this partial owns the Logs tab.
        private LogLevel _logLevelFloor = LogLevel.Verbose;
        // Multi-select sources. Empty => "All". Stored as set for cheap membership.
        private readonly HashSet<string> _logSourceFilter = new();
        private string _logTextFilter = "";
        private Regex _logTextFilterRegex;       // non-null when `re:` prefix is set
        private bool _logPaused;
        // Toast banner for "Copied to clipboard" / "Exported to: …" feedback.
        // Lives as a child of _listContainer so it reuses the controller's
        // dirty-flag re-render path without polling on Update.
        private Label _logToastEl;

        // The chips below the level — must match BuildLogControlStrip's iteration.
        private static readonly string[] LogSourceChips =
            { "Unity", "iOS", "Android", "Firebase", "Adjust", "Facebook", "Noctua" };

        private void RenderLogs(ref int ok, ref int failing, ref int inflight)
        {
            // Header / control strip
            _listContainer.Add(BuildLogControlStrip());

            if (_logLedger == null)
            {
                _listContainer.Add(MakeMutedLabel("Logs ledger not initialized — sandboxEnabled may be false."));
                return;
            }

            // Toast banner (if active). Re-attached on every render so it
            // lives above the log rows. Auto-hides via UI Toolkit's
            // VisualElement.schedule API — see ShowToast.
            if (_logToastEl != null && _logToastEl.style.display.value != DisplayStyle.None)
            {
                _listContainer.Add(_logToastEl);
            }

            var snapshot = _logLedger.Snapshot();
            int total = snapshot.Count;
            int admitted = 0;

            // Render newest-first — same orientation as Trackers / HTTP tabs.
            for (int i = snapshot.Count - 1; i >= 0; i--)
            {
                var e = snapshot[i];
                if (!PassesLogFilter(e)) continue;
                _listContainer.Add(BuildLogRow(e));
                admitted++;
                if (admitted >= LogTabRowBudget) break;

                if (e.Level == LogLevel.Error || e.Level == LogLevel.Warning) failing++;
                else ok++;
            }

            if (admitted == 0)
            {
                _listContainer.Add(MakeMutedLabel(
                    total == 0
                        ? "No log entries captured yet."
                        : $"All {total} entries filtered out — adjust the level / source / text filters above."));
            }
        }

        private bool PassesLogFilter(LogEntry e)
        {
            if (e.Level < _logLevelFloor) return false;
            // Empty set means "All" — pass everything.
            if (_logSourceFilter.Count > 0 && !_logSourceFilter.Contains(e.Source))
                return false;
            if (_logTextFilterRegex != null)
            {
                if (!_logTextFilterRegex.IsMatch(e.Message ?? "") &&
                    !_logTextFilterRegex.IsMatch(e.Tag ?? ""))
                    return false;
            }
            else if (!string.IsNullOrEmpty(_logTextFilter))
            {
                if ((e.Message?.IndexOf(_logTextFilter, StringComparison.OrdinalIgnoreCase) ?? -1) < 0 &&
                    (e.Tag?.IndexOf(_logTextFilter, StringComparison.OrdinalIgnoreCase) ?? -1) < 0)
                    return false;
            }
            return true;
        }

        private VisualElement BuildLogControlStrip()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.flexWrap = Wrap.Wrap;
            bar.style.flexShrink = 0;
            bar.style.paddingLeft = 8; bar.style.paddingRight = 8;
            bar.style.paddingTop = 6; bar.style.paddingBottom = 6;
            bar.style.backgroundColor = Bg1;

            // Level chips — multi-state: tap to advance through V/D/I/W/E.
            var levelLabel = new Label($"Level: {_logLevelFloor}+");
            StyleChipText(levelLabel, accent: AccentHttp);
            levelLabel.RegisterCallback<ClickEvent>(_ =>
            {
                _logLevelFloor = NextLevel(_logLevelFloor);
                _dirty = true;
            });
            bar.Add(levelLabel);

            // Multi-select source chips. Empty set = "All". Tapping "All" clears
            // the set; tapping a specific chip toggles its membership.
            var allChip = new Label("All");
            bool allActive = _logSourceFilter.Count == 0;
            StyleChipText(allChip, accent: allActive ? AccentTracker : Bg2);
            allChip.style.color = allActive ? Color.white : TextMid;
            allChip.RegisterCallback<ClickEvent>(_ =>
            {
                _logSourceFilter.Clear();
                _dirty = true;
            });
            bar.Add(allChip);

            foreach (var s in LogSourceChips)
            {
                var chip = new Label(s);
                bool active = _logSourceFilter.Contains(s);
                StyleChipText(chip, accent: active ? AccentTracker : Bg2);
                chip.style.color = active ? Color.white : TextMid;
                chip.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!_logSourceFilter.Add(s)) _logSourceFilter.Remove(s);
                    _dirty = true;
                });
                bar.Add(chip);
            }

            // Text filter — supports `re:<pattern>` for regex; plain substring otherwise.
            var textField = new TextField { value = _logTextFilter };
            textField.style.minWidth = 140;
            textField.style.marginLeft = 4; textField.style.marginRight = 4;
            textField.tooltip = "Plain substring; prefix `re:` for regex (e.g. `re:^GET .* 5\\d\\d`)";
            textField.RegisterValueChangedCallback(evt =>
            {
                _logTextFilter = evt.newValue ?? "";
                CompileTextFilter();
                _dirty = true;
            });
            bar.Add(textField);

            // Pause / Clear / Native toggle / Export / Copy-all
            bar.Add(MakeButton(_logPaused ? "▶ Resume" : "⏸ Pause", () =>
            {
                _logPaused = !_logPaused;
                if (_logPaused) LogInspectorHooks.UnregisterObserver(_logLedger);
                else LogInspectorHooks.RegisterObserver(_logLedger);
                _dirty = true;
            }));
            bar.Add(MakeButton("Clear", () => { _logLedger?.Clear(); _dirty = true; }));

            if (_logLedger != null)
            {
                bar.Add(MakeButton(
                    _logLedger.NativeStreamEnabled ? "Native: ON" : "Native: off",
                    () =>
                    {
                        _logLedger.NativeStreamEnabled = !_logLedger.NativeStreamEnabled;
                        try { NativeLogStreamToggle?.Invoke(_logLedger.NativeStreamEnabled); }
                        catch { /* swallow */ }
                        _dirty = true;
                    }));
            }

            // Export filtered view to a timestamped .txt under persistentDataPath.
            // Game devs / QA hand the file off via Files-app share / `adb pull`.
            bar.Add(MakeButton("Export", ExportFilteredLogs));

            // Copy-all: dumps the filtered view to the system clipboard.
            // For a single row, devs tap the row itself (BuildLogRow registers a
            // ClickEvent that copies that row alone).
            bar.Add(MakeButton("Copy", CopyFilteredLogsToClipboard));

            return bar;
        }

        private void CompileTextFilter()
        {
            const string prefix = "re:";
            if (_logTextFilter != null &&
                _logTextFilter.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var pattern = _logTextFilter.Substring(prefix.Length);
                try
                {
                    _logTextFilterRegex = string.IsNullOrEmpty(pattern)
                        ? null
                        : new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                }
                catch (ArgumentException)
                {
                    // Malformed pattern — fall back to substring against the full
                    // string (including the `re:` prefix) so the user sees nothing
                    // matches and knows the regex didn't compile.
                    _logTextFilterRegex = null;
                }
            }
            else
            {
                _logTextFilterRegex = null;
            }
        }

        private VisualElement BuildLogRow(LogEntry e)
        {
            // Two-line row layout — header line (timestamp / level / source /
            // tag, all single-line) + body line (full-width wrapped message).
            //
            // Why not the previous one-row layout? UI Toolkit's flex-row
            // height measurement does NOT propagate a wrapping Label's
            // measured height back to the row container when the Label has
            // flexGrow=1 + whiteSpace=Normal. The row stays at single-line
            // height and the wrapped message paints into the next row's
            // space — that's the "tertumpuk" (stacked) bug. Putting the
            // message on its own line under a fixed-height header avoids
            // the broken measurement entirely.
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Column;
            row.style.flexShrink = 0;
            row.style.paddingLeft = 16; row.style.paddingRight = 16;
            row.style.paddingTop = 8; row.style.paddingBottom = 8;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = Stroke;

            // Tap-to-copy single row. Cheap: single string allocation per copy.
            row.RegisterCallback<ClickEvent>(_ =>
            {
                GUIUtility.systemCopyBuffer = FormatLogLine(e);
                ShowToast($"Copied row at {e.TimestampUtc.ToLocalTime():HH:mm:ss}");
            });

            // ----- header line (single-line metadata) -----
            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.flexShrink = 0;
            head.style.alignItems = Align.Center;
            head.style.marginBottom = 4;

            // 8-char timestamp HH:mm:ss. fontSize 12 = readable on phone DPI;
            // 10pt was below iOS HIG's 11pt minimum and unreadable in sun.
            var ts = new Label(e.TimestampUtc.ToLocalTime().ToString("HH:mm:ss"));
            ts.style.color = TextLo; ts.style.fontSize = 12;
            ts.style.flexShrink = 0;
            ts.style.marginRight = 10;
            head.Add(ts);

            var lvl = new Label(LevelGlyph(e.Level));
            lvl.style.color = LevelColor(e.Level);
            lvl.style.flexShrink = 0;
            lvl.style.fontSize = 12;
            lvl.style.unityFontStyleAndWeight = FontStyle.Bold;
            lvl.style.marginRight = 10;
            head.Add(lvl);

            var src = new Label(e.Source);
            src.style.color = TextMid; src.style.fontSize = 12;
            src.style.flexShrink = 0;
            src.style.marginRight = 10;
            head.Add(src);

            if (!string.IsNullOrEmpty(e.Tag))
            {
                var tag = new Label("/" + e.Tag);
                tag.style.color = TextMid; tag.style.fontSize = 12;
                tag.style.flexShrink = 1;
                tag.style.overflow = Overflow.Hidden;
                tag.style.textOverflow = TextOverflow.Ellipsis;
                head.Add(tag);
            }

            row.Add(head);

            // ----- body line (full-width wrapped message) -----
            var msg = new Label(e.Message);
            msg.style.color = TextHi; msg.style.fontSize = 13;
            msg.style.whiteSpace = WhiteSpace.Normal;
            // No flex-grow — the message takes the row's full width naturally
            // because the parent (row) is flex-direction column and Labels
            // span the cross-axis. flex-shrink=0 + width measurement is now
            // straightforward because there's no row-axis fight with siblings.
            msg.style.flexShrink = 0;
            row.Add(msg);

            return row;
        }

        private void CopyFilteredLogsToClipboard()
        {
            if (_logLedger == null) return;
            var sb = new StringBuilder(64 * 1024);
            int n = AppendFilteredLogs(sb);
            GUIUtility.systemCopyBuffer = sb.ToString();
            ShowToast(n > 0 ? $"Copied {n} rows to clipboard" : "No rows match the filter");
        }

        private void ExportFilteredLogs()
        {
            if (_logLedger == null) return;
            try
            {
                var sb = new StringBuilder(64 * 1024);
                int n = AppendFilteredLogs(sb);
                var ts = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                var path = Path.Combine(Application.persistentDataPath, $"noctua-logs-{ts}.txt");
                File.WriteAllText(path, sb.ToString());
                ShowToast(n > 0 ? $"Exported {n} rows → {path}" : $"Exported empty file → {path}");
            }
            catch (Exception ex)
            {
                ShowToast("Export failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Walks the filtered set newest-first and appends formatted lines.
        /// Returns the number of rows appended.
        /// </summary>
        private int AppendFilteredLogs(StringBuilder sb)
        {
            var snapshot = _logLedger.Snapshot();
            int n = 0;
            for (int i = snapshot.Count - 1; i >= 0; i--)
            {
                var e = snapshot[i];
                if (!PassesLogFilter(e)) continue;
                sb.AppendLine(FormatLogLine(e));
                n++;
            }
            return n;
        }

        private static string FormatLogLine(LogEntry e)
        {
            // Same shape as logcat threadtime — tooling-friendly.
            return $"{e.TimestampUtc:yyyy-MM-dd HH:mm:ss.fff} {LevelGlyph(e.Level)} {e.Source}/{e.Tag}: {e.Message}";
        }

        private void ShowToast(string text, float seconds = 2.5f)
        {
            // Lazy-create the toast Label on first use so it can host its own
            // schedule.Execute timer (UI Toolkit's idiomatic delayed callback).
            if (_logToastEl == null)
            {
                _logToastEl = new Label();
                _logToastEl.style.backgroundColor = Bg2;
                _logToastEl.style.color = TextHi;
                _logToastEl.style.paddingLeft = 12; _logToastEl.style.paddingRight = 12;
                _logToastEl.style.paddingTop = 6; _logToastEl.style.paddingBottom = 6;
                _logToastEl.style.marginLeft = 8; _logToastEl.style.marginRight = 8;
                _logToastEl.style.marginTop = 4; _logToastEl.style.marginBottom = 4;
                _logToastEl.style.borderTopLeftRadius = 4; _logToastEl.style.borderTopRightRadius = 4;
                _logToastEl.style.borderBottomLeftRadius = 4; _logToastEl.style.borderBottomRightRadius = 4;
                _logToastEl.style.fontSize = 11;
                _logToastEl.style.whiteSpace = WhiteSpace.Normal;
            }
            _logToastEl.text = text;
            _logToastEl.style.display = DisplayStyle.Flex;
            _dirty = true;

            // UI Toolkit's schedule API — single delayed callback on the
            // panel's update loop. Avoids burning a coroutine or MonoBehaviour
            // Update tick just to fade a toast.
            _logToastEl.schedule.Execute(() =>
            {
                if (_logToastEl == null) return;
                _logToastEl.style.display = DisplayStyle.None;
                _dirty = true;
            }).StartingIn((long)(seconds * 1000));
        }

        private static LogLevel NextLevel(LogLevel current) => current switch
        {
            LogLevel.Verbose => LogLevel.Debug,
            LogLevel.Debug   => LogLevel.Info,
            LogLevel.Info    => LogLevel.Warning,
            LogLevel.Warning => LogLevel.Error,
            LogLevel.Error   => LogLevel.Verbose,
            _                => LogLevel.Verbose,
        };

        private static string LevelGlyph(LogLevel l) => l switch
        {
            LogLevel.Verbose => "V",
            LogLevel.Debug   => "D",
            LogLevel.Info    => "I",
            LogLevel.Warning => "W",
            LogLevel.Error   => "E",
            _                => "?",
        };

        private Color LevelColor(LogLevel l) => l switch
        {
            LogLevel.Error   => Err,
            LogLevel.Warning => Warn,
            LogLevel.Info    => TextHi,
            LogLevel.Debug   => TextMid,
            LogLevel.Verbose => TextLo,
            _                => TextMid,
        };

        private void StyleChipText(Label chip, Color accent)
        {
            chip.style.paddingLeft = 10; chip.style.paddingRight = 10;
            chip.style.paddingTop = 4; chip.style.paddingBottom = 4;
            chip.style.marginRight = 4; chip.style.marginBottom = 4;
            chip.style.borderTopLeftRadius = 12; chip.style.borderTopRightRadius = 12;
            chip.style.borderBottomLeftRadius = 12; chip.style.borderBottomRightRadius = 12;
            chip.style.fontSize = 11;
            chip.style.backgroundColor = accent;
            chip.style.color = Color.white;
        }

        private VisualElement MakeMutedLabel(string text)
        {
            var l = new Label(text);
            l.style.color = TextMid;
            l.style.paddingLeft = 12; l.style.paddingRight = 12;
            l.style.paddingTop = 12; l.style.paddingBottom = 12;
            l.style.fontSize = 11;
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }
    }
}
