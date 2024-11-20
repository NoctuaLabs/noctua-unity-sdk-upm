using System;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    public enum NoctuaErrorCode
    {
        Unknown = 3000,
        Networking = 3001,
        Application = 3002,
        Authentication = 3003,
        ActiveCurrencyFailure = 3004,
        MissingCompletionHandler = 3005,
        Payment = 3006,
        AccountStorage = 3007,
        UserBanned = 2202
    }
    
    public class NoctuaException : Exception
    {
        public int ErrorCode { get; private set; }

        public NoctuaException(NoctuaErrorCode errorCode, string message) : base(message)
        {
            ErrorCode = (int)errorCode;
        }
        
        public NoctuaException(ErrorResponse errorResponse) : base(errorResponse.ErrorMessage)
        {
            ErrorCode = errorResponse.ErrorCode;
        }

        public NoctuaException(NoctuaErrorCode errorCode, string message, Exception innerException) : base(message, innerException)
        {
            ErrorCode = (int)errorCode;
        }

        public override string ToString()
        {
            return $"Error Code: {ErrorCode}, Message: {Message}";
        }

        /* These act as:
     *  - Error code documentation
     *  - Reusable templating
     */
        public static readonly NoctuaException OtherWebRequestError = new(NoctuaErrorCode.Networking, "Other web request error");
        public static readonly NoctuaException RequestConnectionError = new(NoctuaErrorCode.Networking, "Request connection error");
        public static readonly NoctuaException RequestDataProcessingError = new(NoctuaErrorCode.Networking, "Request data processing error");
        public static readonly NoctuaException RequestProtocolError = new(NoctuaErrorCode.Networking, "Request protocol error");
        public static readonly NoctuaException RequestUnreplacedParam = new(NoctuaErrorCode.Application, "Request unreplaced param");
        public static readonly NoctuaException MissingAccessToken = new(NoctuaErrorCode.Authentication, "Missing access token");
        public static readonly NoctuaException ActiveCurrencyFailure = new(NoctuaErrorCode.ActiveCurrencyFailure, "Failed to get active currency");
        public static readonly NoctuaException MissingCompletionHandler = new(NoctuaErrorCode.MissingCompletionHandler, "Missing task completion handler");
    }

    [Preserve]
    public class ErrorResponse
    {
        [JsonProperty("success")]
        public bool Success;

        [JsonProperty("error_message")]
        public string ErrorMessage;

        [JsonProperty("error_code")]
        public int ErrorCode;
    }
}