using System;
using System.Collections.Generic;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Lifecycle state of a single HTTP exchange captured by the Inspector.
    /// Updated live by <see cref="HttpRequest"/> as the UnityWebRequest progresses.
    /// </summary>
    public enum HttpExchangeState
    {
        Building  = 0,  // DTO built, request not yet on the wire
        Sending   = 1,  // SendWebRequest() invoked
        Receiving = 2,  // first non-zero downloadProgress observed
        Complete  = 3,  // response returned with status < 400
        Failed    = 4,  // HTTP >= 400 or connection/data/parse error
        Aborted   = 5,  // request cancelled (reserved — not emitted today)
    }

    /// <summary>
    /// Snapshot of one HTTP exchange, surfaced to the Inspector UI.
    /// Mutable during flight; immutable contract after <see cref="HttpExchangeState.Complete"/>
    /// or <see cref="HttpExchangeState.Failed"/>.
    /// Kept as a plain record so consumers can clone / serialise for export.
    /// </summary>
    public class HttpExchange
    {
        public Guid Id { get; set; }
        public string Method { get; set; }
        public string Url { get; set; }
        public Dictionary<string, string> RequestHeaders { get; set; } = new();
        public string RequestBody { get; set; }
        public int Status { get; set; }
        public Dictionary<string, string> ResponseHeaders { get; set; } = new();
        public string ResponseBody { get; set; }
        public DateTime StartUtc { get; set; }
        public long ElapsedMs { get; set; }
        public string Error { get; set; }
        public HttpExchangeState State { get; set; }
    }
}
