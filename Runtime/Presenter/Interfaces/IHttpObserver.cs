using System;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Observer invoked by <see cref="HttpRequest"/> as each HTTP exchange
    /// progresses through <see cref="HttpExchangeState"/> transitions.
    /// Used by the Noctua Inspector to render a live HTTP timeline.
    ///
    /// Observers are fan-out targets on a static registry
    /// (<c>HttpInspectorHooks.RegisterObserver</c>) so the Infrastructure
    /// layer stays free of Presenter/View dependencies.
    /// </summary>
    public interface IHttpObserver
    {
        void OnRequestStart(HttpExchange exchange);
        void OnStateChange(Guid exchangeId, HttpExchangeState state);
        void OnRequestEnd(HttpExchange exchange);
    }
}
