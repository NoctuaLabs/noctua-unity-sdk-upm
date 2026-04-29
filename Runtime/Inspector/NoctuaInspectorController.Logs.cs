using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.Inspector
{
    /// <summary>
    /// "Logs" tab — verbose log viewer with level/source/text filters.
    /// Pulls entries from <see cref="LogInspectorLedger"/> (already drained
    /// on the main thread by <c>Update</c>'s <c>Pump</c> call) and renders
    /// the most recent <see cref="LogTabRowBudget"/> rows.
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
        private string _logSourceFilter = "All"; // All | Unity | iOS | Android | Firebase | Adjust | Facebook | Noctua
        private string _logTextFilter = "";
        private bool _logPaused;

        private void RenderLogs(ref int ok, ref int failing, ref int inflight)
        {
            // Header / control strip
            _listContainer.Add(BuildLogControlStrip());

            if (_logLedger == null)
            {
                _listContainer.Add(MakeMutedLabel("Logs ledger not initialized — sandboxEnabled may be false."));
                return;
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
            if (_logSourceFilter != "All" && !string.Equals(e.Source, _logSourceFilter, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.IsNullOrEmpty(_logTextFilter))
            {
                if (e.Message?.IndexOf(_logTextFilter, StringComparison.OrdinalIgnoreCase) < 0 &&
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

            // Source chips
            foreach (var s in new[] { "All", "Unity", "iOS", "Android", "Firebase", "Adjust", "Facebook", "Noctua" })
            {
                var chip = new Label(s);
                bool active = _logSourceFilter == s;
                StyleChipText(chip, accent: active ? AccentTracker : Bg2);
                chip.style.color = active ? Color.white : TextMid;
                chip.RegisterCallback<ClickEvent>(_ =>
                {
                    _logSourceFilter = s;
                    _dirty = true;
                });
                bar.Add(chip);
            }

            // Text filter
            var textField = new TextField { value = _logTextFilter };
            textField.style.minWidth = 120;
            textField.style.marginLeft = 4; textField.style.marginRight = 4;
            textField.RegisterValueChangedCallback(evt =>
            {
                _logTextFilter = evt.newValue ?? "";
                _dirty = true;
            });
            bar.Add(textField);

            // Pause / Clear / Native toggle
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

            return bar;
        }

        private VisualElement BuildLogRow(LogEntry e)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.paddingLeft = 12; row.style.paddingRight = 12;
            row.style.paddingTop = 4; row.style.paddingBottom = 4;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = Stroke;

            // 8-char timestamp HH:mm:ss
            var ts = new Label(e.TimestampUtc.ToLocalTime().ToString("HH:mm:ss"));
            ts.style.color = TextLo; ts.style.fontSize = 10;
            ts.style.minWidth = 64; ts.style.flexShrink = 0;
            row.Add(ts);

            var lvl = new Label(LevelGlyph(e.Level));
            lvl.style.color = LevelColor(e.Level);
            lvl.style.minWidth = 18; lvl.style.flexShrink = 0;
            lvl.style.fontSize = 10;
            lvl.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(lvl);

            var src = new Label(e.Source);
            src.style.color = TextMid; src.style.fontSize = 10;
            src.style.minWidth = 60; src.style.flexShrink = 0;
            row.Add(src);

            if (!string.IsNullOrEmpty(e.Tag))
            {
                var tag = new Label(e.Tag);
                tag.style.color = TextMid; tag.style.fontSize = 10;
                tag.style.minWidth = 60; tag.style.flexShrink = 0;
                row.Add(tag);
            }

            var msg = new Label(e.Message);
            msg.style.color = TextHi; msg.style.fontSize = 11;
            msg.style.flexGrow = 1; msg.style.flexShrink = 1;
            msg.style.whiteSpace = WhiteSpace.Normal;
            row.Add(msg);

            return row;
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
