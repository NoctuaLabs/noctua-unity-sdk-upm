using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.Inspector
{
    /// <summary>
    /// "Inject" tab — renders the game-registered debug actions from
    /// <see cref="InspectorActionRegistry"/> (populated via
    /// <see cref="Noctua.RegisterInspectorAction"/>) as forms. Injecting a
    /// level / game item mutates state the SDK does not own, so the game
    /// supplies the handler; this tab only collects the typed inputs and
    /// invokes that handler. Empty when no game has registered anything.
    /// </summary>
    public partial class NoctuaInspectorController
    {
        private readonly ILogger _log = new NoctuaLogger(typeof(NoctuaInspectorController));

        // Per-action current field values, keyed [actionLabel][paramKey].
        // Persisted across the per-tab rebuild so typed input survives a
        // RenderList() (the form is re-created from these on every render).
        private readonly Dictionary<string, Dictionary<string, string>> _injectValues = new();

        // Marks the Inject tab dirty when the registry changes after the
        // overlay has spawned (subscribed in Install, removed in OnDestroy).
        private void OnInspectorActionsChanged()
        {
            if (_tab == Tab.Inject) _dirty = true;
        }

        private void RenderInject(ref int ok, ref int failing, ref int inflight)
        {
            var actions = InspectorActionRegistry.Actions;
            if (actions == null || actions.Count == 0)
            {
                _listContainer.Add(Placeholder(
                    "No debug actions registered.\n\n" +
                    "Call Noctua.RegisterInspectorAction(label, params, handler) from your game " +
                    "to inject game-owned state here (e.g. set a level, grant an item)."));
                return;
            }

            foreach (var action in actions)
            {
                _listContainer.Add(BuildInjectAction(action));
                ok++;
            }
        }

        private VisualElement BuildInjectAction(InspectorAction action)
        {
            var box = new VisualElement();
            box.style.flexShrink = 0;
            box.style.paddingLeft = 12; box.style.paddingRight = 12;
            box.style.paddingTop = 12; box.style.paddingBottom = 8;
            box.style.borderBottomWidth = 1;
            box.style.borderBottomColor = Stroke;

            var head = new Label(action.Label);
            head.style.color = TextLo; head.style.fontSize = 12;
            head.style.paddingBottom = 6;
            box.Add(head);

            if (!_injectValues.TryGetValue(action.Label, out var values))
            {
                values = new Dictionary<string, string>();
                _injectValues[action.Label] = values;
            }

            foreach (var p in action.Params)
            {
                if (p == null || string.IsNullOrEmpty(p.Key)) continue;

                // Seed the persisted value from the declared default the
                // first time this field is rendered.
                if (!values.ContainsKey(p.Key))
                    values[p.Key] = p.DefaultValue ?? "";

                box.Add(BuildInjectField(p, values));
            }

            var execute = MakeButton("Execute", () => OnExecuteAction(action, values));
            execute.style.color = AccentTracker;
            execute.style.marginTop = 8;
            execute.style.paddingTop = 8; execute.style.paddingBottom = 8;
            box.Add(execute);

            return box;
        }

        private VisualElement BuildInjectField(InspectorActionParam p, Dictionary<string, string> values)
        {
            var row = new VisualElement();
            row.style.flexShrink = 0;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 4; row.style.paddingBottom = 4;

            var label = new Label($"{p.Key} ({p.Type})");
            label.style.color = TextMid; label.style.fontSize = 12;
            label.style.minWidth = 130; label.style.flexShrink = 0;
            row.Add(label);

            if (p.Type == InspectorParamType.Bool)
            {
                var toggle = new Toggle
                {
                    value = string.Equals(values[p.Key], "true", StringComparison.OrdinalIgnoreCase)
                };
                toggle.style.flexGrow = 0;
                toggle.RegisterValueChangedCallback(evt =>
                    values[p.Key] = evt.newValue ? "true" : "false");
                row.Add(toggle);
            }
            else
            {
                var field = new TextField { value = values[p.Key] };
                StyleSearchField(field);
                field.style.flexGrow = 1;
                field.RegisterValueChangedCallback(evt => values[p.Key] = evt.newValue ?? "");
                row.Add(field);
            }

            return row;
        }

        private void OnExecuteAction(InspectorAction action, Dictionary<string, string> values)
        {
            if (action?.Handler == null)
            {
                ShowToast($"'{action?.Label}' has no handler");
                return;
            }

            var payload = new Dictionary<string, object>();
            foreach (var p in action.Params)
            {
                if (p == null || string.IsNullOrEmpty(p.Key)) continue;
                var raw = values.TryGetValue(p.Key, out var v) ? (v ?? "") : "";

                switch (p.Type)
                {
                    case InspectorParamType.Int:
                        if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                        {
                            ShowToast($"'{p.Key}' must be an integer");
                            return;
                        }
                        payload[p.Key] = l;
                        break;
                    case InspectorParamType.Float:
                        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                        {
                            ShowToast($"'{p.Key}' must be a number");
                            return;
                        }
                        payload[p.Key] = d;
                        break;
                    case InspectorParamType.Bool:
                        payload[p.Key] = string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
                        break;
                    default:
                        payload[p.Key] = raw;
                        break;
                }
            }

            var payloadStr = payload.Count == 0
                ? "(no params)"
                : string.Join(", ", payload.Select(kv => $"{kv.Key}={kv.Value}"));
            _log.Info($"[inspector] inject action '{action.Label}' executed — {payloadStr}");

            try
            {
                action.Handler.Invoke(payload);
                ShowToast($"Executed '{action.Label}'");
            }
            catch (Exception e)
            {
                _log.Error($"[inspector] inject action '{action.Label}' failed: {e}");
                ShowToast($"'{action.Label}' failed: {e.Message}");
            }
        }
    }
}
