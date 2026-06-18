using System;
using System.Collections.Generic;

namespace com.noctuagames.sdk
{
    public partial class Noctua
    {
        /// <summary>
        /// Register a debug action that appears in the Noctua Inspector
        /// "Inject" tab (sandbox builds only). The game owns
        /// <paramref name="handler"/> — the Inspector renders a form from
        /// <paramref name="parameters"/> and, on Execute, invokes the handler
        /// with the entered values coerced to each parameter's declared type
        /// (<see cref="InspectorParamType.Int"/> → <c>long</c>,
        /// <see cref="InspectorParamType.Float"/> → <c>double</c>,
        /// <see cref="InspectorParamType.Bool"/> → <c>bool</c>, otherwise
        /// <c>string</c>). Use it to inject game-owned state for QA — set a
        /// level, grant an item, add currency, etc.
        ///
        /// <para>Safe to call unconditionally and at any time: the registry
        /// lives independently of the overlay, so in production (no sandbox)
        /// the action is simply never rendered. Registering the same
        /// <paramref name="label"/> again replaces the previous action.</para>
        /// </summary>
        public static void RegisterInspectorAction(
            string label,
            IReadOnlyList<InspectorActionParam> parameters,
            Action<IReadOnlyDictionary<string, object>> handler)
        {
            InspectorActionRegistry.Register(new InspectorAction(label, parameters, handler));
        }

        /// <summary>
        /// Convenience overload for a parameter-less action: renders just the
        /// Execute button. See
        /// <see cref="RegisterInspectorAction(string, IReadOnlyList{InspectorActionParam}, Action{IReadOnlyDictionary{string, object}})"/>.
        /// </summary>
        public static void RegisterInspectorAction(string label, Action handler)
        {
            InspectorActionRegistry.Register(
                new InspectorAction(label, null, _ => handler?.Invoke()));
        }

        /// <summary>Remove a previously registered Inspector debug action by label.</summary>
        public static void UnregisterInspectorAction(string label)
        {
            InspectorActionRegistry.Unregister(label);
        }
    }
}
