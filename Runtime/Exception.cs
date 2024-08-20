using System;
using Newtonsoft.Json;

/*


- 3000, Other web request error
- 3001: Web request connection error
- 3002: Web reqest data processing error
- 3003: Web request protocol error

*/

public class NoctuaException : Exception
{
    public int ErrorCode { get; private set; }

    public NoctuaException(int errorCode, string message) 
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public NoctuaException(int errorCode, string message, Exception innerException) 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public override string ToString()
    {
        return $"Error Code: {ErrorCode}, Message: {Message}";
    }

    /* These act as:
     *  - Error code documentation
     *  - Reusable templating
     */
    public static readonly NoctuaException OtherWebRequestError = new NoctuaException(3000, "Other web request error");
    public static readonly NoctuaException RequestConnectionError = new NoctuaException(3001, "Request connection error");
    public static readonly NoctuaException RequestDataProcessingError = new NoctuaException(3002, "Request data processing error");
    public static readonly NoctuaException RequestProtocolError = new NoctuaException(3003, "Request protocol error");
    public static readonly NoctuaException RequestUnreplacedParam = new NoctuaException(3004, "Request unreplaced param");
}

public class ErrorResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("error")]
    public string Error { get; set; }

    [JsonProperty("error_code")]
    public int ErrorCode { get; set; }
}