using System;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Error codes used by <see cref="NoctuaException"/> to categorize SDK failures.
    /// </summary>
    public enum NoctuaErrorCode
    {
        /// <summary>An unknown or uncategorized error.</summary>
        Unknown = 3000,
        /// <summary>A network-level error (connection, timeout, DNS).</summary>
        Networking = 3001,
        /// <summary>An application-level error (invalid parameters, missing config).</summary>
        Application = 3002,
        /// <summary>An authentication error (invalid token, expired session).</summary>
        Authentication = 3003,
        /// <summary>Failed to resolve the active currency for the user.</summary>
        ActiveCurrencyFailure = 3004,
        /// <summary>A required async completion handler was not set.</summary>
        MissingCompletionHandler = 3005,
        /// <summary>A general payment processing error.</summary>
        Payment = 3006,
        /// <summary>An error reading or writing the local account storage.</summary>
        AccountStorage = 3007,
        /// <summary>The payment was canceled by the user.</summary>
        PaymentStatusCanceled = 3008,
        /// <summary>The item has already been purchased (non-consumable duplicate).</summary>
        PaymentStatusItemAlreadyOwned = 3009,
        /// <summary>The native IAP subsystem is not ready or not initialized.</summary>
        PaymentStatusIapNotReady = 3010,
        /// <summary>The user account has been banned by the server.</summary>
        UserBanned = 2202
    }
    
    /// <summary>
    /// Exception type thrown by Noctua SDK operations, carrying a numeric error code and optional payload.
    /// </summary>
    public class NoctuaException : Exception
    {
        /// <summary>Numeric error code identifying the failure category.</summary>
        public int ErrorCode { get; private set; }

        /// <summary>Optional JSON payload with additional error details from the server.</summary>
        public string Payload { get; private set; }

        /// <summary>
        /// Creates a new NoctuaException with the specified error code, message, and optional payload.
        /// </summary>
        /// <param name="errorCode">The error code category.</param>
        /// <param name="message">Human-readable error description.</param>
        /// <param name="payload">Optional additional data payload.</param>
        public NoctuaException(NoctuaErrorCode errorCode, string message, string payload = "")
            : base($"ErrorCode: {errorCode}, Message: \"{message}\"")
        {
            ErrorCode = (int)errorCode;
            Payload = payload;
        }

        /// <summary>Returns a string representation including the error code, message, and payload.</summary>
        public override string ToString()
        {
            return $"ErrorCode: {ErrorCode}, Message: {Message}, Payload: {Payload}";
        }

        /* These act as:
     *  - Error code documentation
     *  - Reusable templating
     */
        /// <summary>Unclassified web request error.</summary>
        public static readonly NoctuaException OtherWebRequestError = new(NoctuaErrorCode.Networking, "Other web request error");
        /// <summary>Network connection could not be established.</summary>
        public static readonly NoctuaException RequestConnectionError = new(NoctuaErrorCode.Networking, "Request connection error");
        /// <summary>Error while processing request or response data.</summary>
        public static readonly NoctuaException RequestDataProcessingError = new(NoctuaErrorCode.Networking, "Request data processing error");
        /// <summary>A request is already in progress.</summary>
        public static readonly NoctuaException RequestInProgress = new(NoctuaErrorCode.Networking, "Request in progress");
        /// <summary>HTTP protocol-level error.</summary>
        public static readonly NoctuaException RequestProtocolError = new(NoctuaErrorCode.Networking, "Request protocol error");
        /// <summary>A URL template parameter was not replaced before sending the request.</summary>
        public static readonly NoctuaException RequestUnreplacedParam = new(NoctuaErrorCode.Application, "Request unreplaced param");
        /// <summary>No access token is available for an authenticated request.</summary>
        public static readonly NoctuaException MissingAccessToken = new(NoctuaErrorCode.Authentication, "Missing access token");
        /// <summary>Failed to determine the active currency for the user.</summary>
        public static readonly NoctuaException ActiveCurrencyFailure = new(NoctuaErrorCode.ActiveCurrencyFailure, "Failed to get active currency");
        /// <summary>A required async task completion handler was not registered.</summary>
        public static readonly NoctuaException MissingCompletionHandler = new(NoctuaErrorCode.MissingCompletionHandler, "Missing task completion handler");
    }

    /// <summary>
    /// Standard error response body returned by the Noctua API when a request fails.
    /// </summary>
    [Preserve]
    public class ErrorResponse
    {
        /// <summary>Always false for error responses.</summary>
        [JsonProperty("success")]
        public bool Success;

        /// <summary>Human-readable error message from the server.</summary>
        [JsonProperty("error_message")]
        public string ErrorMessage;

        /// <summary>Server-defined numeric error code.</summary>
        [JsonProperty("error_code")]
        public int ErrorCode;
    }
}