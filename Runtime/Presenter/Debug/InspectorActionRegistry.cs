using System;
using System.Collections.Generic;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Declared type of a single <see cref="InspectorActionParam"/> input.
    /// The Inspector "Inject" tab renders an appropriate editor per type and
    /// coerces the entered text to the matching CLR type before handing it to
    /// the action handler:
    /// <list type="bullet">
    ///   <item><see cref="String"/> → <c>string</c></item>
    ///   <item><see cref="Int"/> → <c>long</c></item>
    ///   <item><see cref="Float"/> → <c>double</c></item>
    ///   <item><see cref="Bool"/> → <c>bool</c></item>
    /// </list>
    /// </summary>
    public enum InspectorParamType { String, Int, Float, Bool }

    /// <summary>
    /// One input field of a debug action registered via
    /// <see cref="Noctua.RegisterInspectorAction"/>. Immutable — construct a
    /// new instance rather than mutating an existing one.
    /// </summary>
    public sealed class InspectorActionParam
    {
        /// <summary>Key the parsed value is stored under in the handler payload.</summary>
        public string Key { get; }

        /// <summary>How the Inspector renders/parses this field.</summary>
        public InspectorParamType Type { get; }

        /// <summary>Pre-filled value shown when the form first renders. May be null.</summary>
        public string DefaultValue { get; }

        public InspectorActionParam(string key, InspectorParamType type = InspectorParamType.String, string defaultValue = null)
        {
            Key = key;
            Type = type;
            DefaultValue = defaultValue;
        }
    }

    /// <summary>
    /// A game-registered debug action surfaced in the Inspector "Inject" tab.
    /// The game owns the <see cref="Handler"/> — the SDK only renders the form
    /// and invokes the handler with the entered, type-coerced values.
    /// Immutable.
    /// </summary>
    public sealed class InspectorAction
    {
        /// <summary>Unique, human-readable label. Also the Execute button text.</summary>
        public string Label { get; }

        /// <summary>Input fields rendered for this action (may be empty).</summary>
        public IReadOnlyList<InspectorActionParam> Params { get; }

        /// <summary>Invoked on Execute with a key→value map of the entered fields.</summary>
        public Action<IReadOnlyDictionary<string, object>> Handler { get; }

        public InspectorAction(
            string label,
            IReadOnlyList<InspectorActionParam> @params,
            Action<IReadOnlyDictionary<string, object>> handler)
        {
            Label = label;
            Params = @params ?? Array.Empty<InspectorActionParam>();
            Handler = handler;
        }
    }

    /// <summary>
    /// Static registry of game-defined debug actions, modelled on
    /// <see cref="TrackerObserverRegistry"/>. Lives independently of the
    /// sandbox-only Inspector overlay so games may register unconditionally —
    /// in production (no overlay) the entries are simply never read.
    ///
    /// <para>Registration is keyed by <see cref="InspectorAction.Label"/>:
    /// re-registering the same label replaces the previous action, so calling
    /// it from a re-entered scene does not accumulate duplicates.</para>
    /// </summary>
    public static class InspectorActionRegistry
    {
        private static readonly List<InspectorAction> _actions = new();
        private static readonly object _lock = new();

        /// <summary>Raised whenever the action set changes (register / unregister / clear).</summary>
        public static event Action Changed;

        /// <summary>Snapshot of the currently registered actions.</summary>
        public static IReadOnlyList<InspectorAction> Actions
        {
            get { lock (_lock) return _actions.ToArray(); }
        }

        /// <summary>
        /// Register (or replace, by label) a debug action. Null actions and
        /// actions with a null/empty label are ignored.
        /// </summary>
        public static void Register(InspectorAction action)
        {
            if (action == null || string.IsNullOrEmpty(action.Label)) return;
            lock (_lock)
            {
                _actions.RemoveAll(a => a.Label == action.Label);
                _actions.Add(action);
            }
            Changed?.Invoke();
        }

        /// <summary>Remove the action registered under <paramref name="label"/>, if any.</summary>
        public static void Unregister(string label)
        {
            if (string.IsNullOrEmpty(label)) return;
            bool removed;
            lock (_lock)
            {
                removed = _actions.RemoveAll(a => a.Label == label) > 0;
            }
            if (removed) Changed?.Invoke();
        }

        /// <summary>Remove every registered action.</summary>
        public static void Clear()
        {
            bool had;
            lock (_lock)
            {
                had = _actions.Count > 0;
                _actions.Clear();
            }
            if (had) Changed?.Invoke();
        }
    }
}
