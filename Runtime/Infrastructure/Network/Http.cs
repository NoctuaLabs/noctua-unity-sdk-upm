using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Net;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Provides an HTTP Authorization header value for authenticating requests.
    /// </summary>
    internal interface IHttpAuth
    {
        /// <summary>Returns the full Authorization header value (e.g. "Basic ..." or "Bearer ...").</summary>
        string Get();
    }


    /// <summary>
    /// Generates a Base64-encoded HTTP Basic Authentication header from a username and password.
    /// </summary>
    internal class BasicAuth : IHttpAuth
    {
        private readonly string _username;
        private readonly string _password;

        /// <summary>
        /// Initializes a new <see cref="BasicAuth"/> with the given credentials.
        /// </summary>
        /// <param name="username">The authentication username.</param>
        /// <param name="password">The authentication password.</param>
        public BasicAuth(string username, string password)
        {
            _username = username;
            _password = password;
        }

        /// <inheritdoc />
        public string Get()
        {
            return $"Basic {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_username}:{_password}"))}";
        }
    }


    /// <summary>
    /// Generates an HTTP Bearer token Authentication header from an access token.
    /// </summary>
    internal class BearerAuth : IHttpAuth
    {
        private readonly string _token;

        /// <summary>
        /// Initializes a new <see cref="BearerAuth"/> with the given token.
        /// </summary>
        /// <param name="token">The bearer access token.</param>
        public BearerAuth(string token)
        {
            _token = token;
        }

        /// <inheritdoc />
        public string Get()
        {
            return $"Bearer {_token}";
        }
    }


    /// <summary>
    /// Supported HTTP methods for <see cref="HttpRequest"/>.
    /// </summary>
    public enum HttpMethod
    {
        /// <summary>HTTP GET method for retrieving resources.</summary>
        Get,
        /// <summary>HTTP POST method for creating resources or submitting data.</summary>
        Post,
        /// <summary>HTTP PUT method for replacing resources.</summary>
        Put,
        /// <summary>HTTP DELETE method for removing resources.</summary>
        Delete,
        /// <summary>HTTP PATCH method for partially updating resources.</summary>
        Patch
    }

    /// <summary>
    /// Fluent builder for constructing and sending HTTP requests via <see cref="UnityWebRequest"/>.
    /// Automatically injects locale, device, and SDK version headers. Supports JSON, NDJSON,
    /// form-encoded, and raw binary request bodies. Responses are deserialized from a
    /// <c>{"data": ...}</c> JSON wrapper.
    /// </summary>
    internal class HttpRequest : IDisposable
    {
        private static ILocaleProvider _localeProvider;

        /// <summary>
        /// Sets the static locale provider used by all <see cref="HttpRequest"/> instances
        /// to populate locale-related headers (language, country, currency).
        /// </summary>
        /// <param name="provider">The locale provider to inject.</param>
        internal static void SetLocaleProvider(ILocaleProvider provider) => _localeProvider = provider;

        private readonly NoctuaLogger _log = new(typeof(HttpRequest));
        private readonly UnityWebRequest _request = new();

        private readonly JsonSerializerSettings _jsonSettings = new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            },
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented,
        };
        
        private bool _noVerboseLog;

        /// <summary>
        /// Creates a new HTTP request with the specified method and URL, and injects default
        /// headers for locale, device ID, platform, OS, and SDK version.
        /// </summary>
        /// <param name="method">The HTTP method (GET, POST, PUT, DELETE, PATCH).</param>
        /// <param name="url">The full request URL.</param>
        internal HttpRequest(HttpMethod method, string url)
        {
            _jsonSettings.Converters.Add(new StringEnumConverter());
            _request.method = method.ToString().ToUpper();
            _request.url = url;

            // Inject locale data via static provider (avoids Noctua singleton dependency)
            var lang = _localeProvider?.GetLanguage() ?? "en";
            var country = _localeProvider?.GetCountry() ?? "";
            var currency = _localeProvider?.GetCurrency() ?? "";
            _request.SetRequestHeader("Accept-Language", lang);
            _request.SetRequestHeader("X-LANGUAGE", lang);
            _request.SetRequestHeader("X-COUNTRY", country);
            _request.SetRequestHeader("X-CURRENCY", currency);
            _request.SetRequestHeader("X-DEVICE-ID", SystemInfo.deviceUniqueIdentifier);
            _request.SetRequestHeader("X-PLATFORM", Utility.GetPlatformType());
            _request.SetRequestHeader("X-OS-AGENT", SystemInfo.operatingSystem);
            _request.SetRequestHeader("X-OS", Application.platform.ToString().ToLower());
            
            _request.SetRequestHeader("X-SDK-VERSION", Assembly.GetExecutingAssembly().GetName().Version.ToString());

        }

        /// <summary>
        /// Replaces a <c>{key}</c> placeholder in the URL with the URI-escaped value.
        /// </summary>
        /// <param name="key">The placeholder name (without braces).</param>
        /// <param name="value">The value to substitute, which will be URI-escaped.</param>
        /// <returns>This <see cref="HttpRequest"/> for method chaining.</returns>
        public HttpRequest WithPathParam(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new Exception($"Path parameter key is null or empty.");
            }

            if (string.IsNullOrEmpty(value))
            {
                throw new Exception($"The path value of key={key} is null or empty.");
            }

            _request.url = _request.url.Replace("{" + key + "}", Uri.EscapeDataString(value));

            return this;
        }

        /// <summary>
        /// Adds an Authorization header to the request using the provided authentication strategy.
        /// </summary>
        /// <param name="auth">The authentication provider (e.g. <see cref="BasicAuth"/> or <see cref="BearerAuth"/>).</param>
        /// <returns>This <see cref="HttpRequest"/> for method chaining.</returns>
        public HttpRequest WithAuth(IHttpAuth auth)
        {
            _request.SetRequestHeader("Authorization", auth.Get());

            return this;
        }

        /// <summary>
        /// Adds a custom HTTP header to the request.
        /// </summary>
        /// <param name="key">The header name.</param>
        /// <param name="value">The header value.</param>
        /// <returns>This <see cref="HttpRequest"/> for method chaining.</returns>
        public HttpRequest WithHeader(string key, string value)
        {
            _request.SetRequestHeader(key, value);

            return this;
        }

        /// <summary>
        /// Sets the request body as URL-encoded form data with Content-Type <c>application/x-www-form-urlencoded</c>.
        /// </summary>
        /// <param name="body">Key-value pairs to encode as form fields.</param>
        /// <returns>This <see cref="HttpRequest"/> for method chaining.</returns>
        public HttpRequest WithFormBody(Dictionary<string, string> body)
        {
            _request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            var form = new WWWForm();

            foreach (var pair in body)
            {
                form.AddField(pair.Key, pair.Value);
            }

            _request.uploadHandler = new UploadHandlerRaw(form.data);

            return this;
        }

        /// <summary>
        /// Serializes the body as JSON with snake_case naming and sets Content-Type to <c>application/json</c>.
        /// </summary>
        /// <typeparam name="T">The type of the request body object.</typeparam>
        /// <param name="body">The object to serialize as the JSON request body.</param>
        /// <returns>This <see cref="HttpRequest"/> for method chaining.</returns>
        public HttpRequest WithJsonBody<T>(T body)
        {
            _request.SetRequestHeader("Content-Type", "application/json");
            var jsonBody = JsonConvert.SerializeObject(body, _jsonSettings);
            var rawBody = Encoding.UTF8.GetBytes(jsonBody);

            _request.uploadHandler = new UploadHandlerRaw(rawBody);

            return this;
        }

        /// <summary>
        /// Serializes a list of objects as newline-delimited JSON (NDJSON) and sets Content-Type
        /// to <c>application/x-ndjson</c>. Each object is serialized on a separate line.
        /// </summary>
        /// <typeparam name="T">The type of each item in the list.</typeparam>
        /// <param name="body">The list of objects to serialize as NDJSON.</param>
        /// <returns>This <see cref="HttpRequest"/> for method chaining.</returns>
        public HttpRequest WithNdjsonBody<T>(IList<T> body)
        {
            _request.SetRequestHeader("Content-Type", "application/x-ndjson");

            var jsonSettings =
                new JsonSerializerSettings
                {
                    ContractResolver = new DefaultContractResolver
                    {
                        NamingStrategy = new SnakeCaseNamingStrategy()
                    },
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.None,
                };

            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                var serializer = JsonSerializer.Create(jsonSettings);

                for (var i = 0; i < body.Count - 1; i++)
                {
                    serializer.Serialize(writer, body[i]);
                    writer.Write("\n");
                }
                
                var last = body.LastOrDefault();
                
                if (last != null)
                {
                    serializer.Serialize(writer, last);
                }

                writer.Flush();

                _request.uploadHandler = new UploadHandlerRaw(stream.ToArray());
            }


            return this;
        }

        /// <summary>
        /// Sets the request body as raw binary data with Content-Type <c>application/octet-stream</c>.
        /// </summary>
        /// <param name="body">The raw byte array to send as the request body.</param>
        /// <returns>This <see cref="HttpRequest"/> for method chaining.</returns>
        public HttpRequest WithRawBody(byte[] body)
        {
            _request.SetRequestHeader("Content-Type", "application/octet-stream");
            _request.uploadHandler = new UploadHandlerRaw(body);

            return this;
        }
        
        /// <summary>
        /// Suppresses verbose request/response header logging, only logging the method and URL.
        /// </summary>
        /// <returns>This <see cref="HttpRequest"/> for method chaining.</returns>
        public HttpRequest NoVerboseLog()
        {
            _noVerboseLog = true;
            
            return this;
        }

        [Preserve]
        private class DataWrapper<T>
        {
            [JsonProperty("data")]
            public T Data;
        }

        /// <summary>
        /// Sends the request and returns the raw response body as a string without JSON deserialization.
        /// </summary>
        /// <returns>The raw response body text.</returns>
        public async UniTask<string> SendRaw()
        {
            _request.downloadHandler = new DownloadHandlerBuffer();
            _request.timeout = 60;
            // Inspector network conditioner — sandbox-only fault injection.
            // No-op in production (Mode defaults to Normal, single read).
            try { await NetworkConditioner.ApplyAsync(); }
            catch (NetworkConditionerException) { throw NoctuaException.RequestConnectionError; }
            await _request.SendWebRequest();

            return _request.downloadHandler.text;
        }

        /// <summary>
        /// Sends the request and deserializes the JSON response from a <c>{"data": T}</c> wrapper.
        /// Throws <see cref="NoctuaException"/> for HTTP errors (4xx/5xx) and deserialization failures.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response <c>data</c> field into.</typeparam>
        /// <returns>The deserialized response data.</returns>
        /// <exception cref="NoctuaException">Thrown on HTTP errors, connection failures, or parse errors.</exception>
        public async UniTask<T> Send<T>()
        {
            if (_request.url.Contains("{") || _request.url.Contains("}"))
            {
                _log.Error($"There are still path parameters that are not replaced: {_request.url}");

                throw NoctuaException.RequestUnreplacedParam;
            }

            _request.downloadHandler = new DownloadHandlerBuffer();
            var response = "";

            // Inspector hook — zero-cost when no observer registered.
            HttpExchange exchange = null;
            System.Diagnostics.Stopwatch sw = null;
            if (HttpInspectorHooks.HasObservers)
            {
                exchange = BuildExchangeSnapshot();
                sw = System.Diagnostics.Stopwatch.StartNew();
                HttpInspectorHooks.FireStart(exchange);
            }

            var auth = !string.IsNullOrEmpty(_request.GetRequestHeader("Authorization")) ? "Authorization: Bearer" : "";

            if (_noVerboseLog)
            {
                _log.Debug($"=> {_request.method} {_request.url}\n");
                
            }
            else
            {
                _log.Debug(
                    $"=> {_request.method} {_request.url}\n"                           +
                    $"Content-Type: {_request.GetRequestHeader("Content-Type")}\n"     +
                    //$"Authorization: {_request.GetRequestHeader("Authorization")}\n"   + // Uncomment for debugging purpose
                    $"{auth}\n"                                                        +
                    $"Accept-Language: {_request.GetRequestHeader("X-LANGUAGE")}\n"    +
                    $"X-CLIENT-ID: {_request.GetRequestHeader("X-CLIENT-ID")}\n"       +
                    $"X-BUNDLE-ID: {_request.GetRequestHeader("X-BUNDLE-ID")}\n"       +
                    $"X-LANGUAGE: {_request.GetRequestHeader("X-LANGUAGE")}\n"         +
                    $"X-COUNTRY: {_request.GetRequestHeader("X-COUNTRY")}\n"           +
                    $"X-CURRENCY: {_request.GetRequestHeader("X-CURRENCY")}\n"         +
                    $"X-DEVICE-ID: {_request.GetRequestHeader("X-DEVICE-ID")}\n"       +
                    $"X-PLATFORM: {_request.GetRequestHeader("X-PLATFORM")}\n"         +
                    $"X-OS: {_request.GetRequestHeader("X-OS")}\n"                     +
                    $"X-OS-AGENT: {_request.GetRequestHeader("X-OS-AGENT")}\n"         +
                    $"X-SDK-VERSION: {_request.GetRequestHeader("X-SDK-VERSION")}\n\n" +
                    $"{Encoding.UTF8.GetString(_request.uploadHandler?.data ?? Array.Empty<byte>())}"
                );
            }

            try
            {
                _request.timeout = 20;
                if (exchange != null) HttpInspectorHooks.FireStateChange(exchange.Id, HttpExchangeState.Sending);
                // Inspector network conditioner — sandbox-only fault injection
                // applied between Sending state-change and the actual network
                // call so the Inspector still records the attempt.
                try { await NetworkConditioner.ApplyAsync(); }
                catch (NetworkConditionerException ncex)
                {
                    if (exchange != null) exchange.Error = ncex.Message;
                    FireEndIfObserved(exchange, sw, response, HttpExchangeState.Failed);
                    throw NoctuaException.RequestConnectionError;
                }
                await _request.SendWebRequest();
                response = _request.downloadHandler.text;
            }
            catch (Exception e)
            {
                _log.Exception(e);

                if (exchange != null) exchange.Error = e.Message;

                switch (_request.result)
                {
                    case UnityWebRequest.Result.ConnectionError:
                        FireEndIfObserved(exchange, sw, response, HttpExchangeState.Failed);
                        throw NoctuaException.RequestConnectionError;
                    case UnityWebRequest.Result.DataProcessingError:
                        FireEndIfObserved(exchange, sw, response, HttpExchangeState.Failed);
                        throw NoctuaException.RequestDataProcessingError;
                    case UnityWebRequest.Result.InProgress:
                        FireEndIfObserved(exchange, sw, response, HttpExchangeState.Failed);
                        throw NoctuaException.RequestInProgress;
                    case UnityWebRequest.Result.ProtocolError: // HTTP statuses >= 400
                    case UnityWebRequest.Result.Success: // HTTP statuses < 400
                        response = _request.downloadHandler.text;
                        break;
                }
            }
            finally
            {
                _request.downloadHandler.Dispose();
                _request.uploadHandler?.Dispose();
            }
            
            var responseCode = _request.responseCode;
            var responseCodeString = ((HttpStatusCode)responseCode).ToString();
            var url = _request.url;
            var method = _request.method;
            
            if (_noVerboseLog)
            {
                _log.Debug($"<= {responseCode} {responseCodeString} {method} {url}");
            }
            else
            {
                var responseHeaders = _request.GetResponseHeaders().Aggregate("", (a, p) => $"{a}\n{p.Key}: {p.Value}");
                _log.Debug($"<= {responseCode} {responseCodeString} {method} {url}\n{responseHeaders}\n\n{response}");
            }
            

            if ((int)_request.responseCode >= 400 && (int)_request.responseCode <= 408)
            {
                ErrorResponse errorResponse;

                try
                {
                    errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(response, _jsonSettings);
                }
                catch (Exception)
                {
                    _log.Error($"HTTP error {_request.responseCode}, response: '{response}'");
                    FireEndIfObserved(exchange, sw, response, HttpExchangeState.Failed);
                    throw new NoctuaException(
                        NoctuaErrorCode.Application,
                            $"HTTP error {_request.responseCode}: {((HttpStatusCode)_request.responseCode).ToString()}"
                        );
                }

                _log.Error($"Noctua error {errorResponse.ErrorCode}: {errorResponse.ErrorMessage}");
                FireEndIfObserved(exchange, sw, response, HttpExchangeState.Failed);
                throw new NoctuaException((NoctuaErrorCode)errorResponse.ErrorCode, errorResponse.ErrorMessage);
            }
            else if ((int)_request.responseCode > 408) // Including 5XX
            {
                // Retryable HTTP status codes are treated as networking error:
                // 408 Request Timeout
                // 425 Too Early
                // 429 Too Many Requests
                // 500 Internal Server Error
                // 502 Bad Gateway
                // 503 Service Unavailable
                // 504 Gateway Timeout
                // 522 Bad Gateway
                response = response[..Math.Min(1000, response.Length)]; // Limit the response to 1000 characters
                _log.Error($"HTTP error {_request.responseCode}, response: '{response}'");
                FireEndIfObserved(exchange, sw, response, HttpExchangeState.Failed);
                throw new NoctuaException(
                    NoctuaErrorCode.Networking,
                        $"HTTP error {_request.responseCode}: {((HttpStatusCode)_request.responseCode)}," +
                        $"Response: '{response}'"
                    );
            } else {
                try
                {
                    var data = JsonConvert.DeserializeObject<DataWrapper<T>>(response, _jsonSettings).Data;
                    FireEndIfObserved(exchange, sw, response, HttpExchangeState.Complete);
                    return data;
                }
                catch (Exception e)
                {
                    _log.Exception(e);

                    if (exchange != null) exchange.Error = e.Message;
                    FireEndIfObserved(exchange, sw, response, HttpExchangeState.Failed);
                    throw new NoctuaException(
                        NoctuaErrorCode.Application,
                        $"Failed to parse response as {typeof(T).Name}: {response}. Error: {e.Message}"
                    );
                }
            }
        }

        // ---- Inspector helpers (active only when an observer is registered) ----

        private const int InspectorBodyCapBytes = 64 * 1024;

        private HttpExchange BuildExchangeSnapshot()
        {
            var ex = new HttpExchange
            {
                Id = Guid.NewGuid(),
                Method = _request.method,
                Url = _request.url,
                StartUtc = DateTime.UtcNow,
                State = HttpExchangeState.Building,
            };

            // Redact sensitive headers — same deny-list on request + response.
            foreach (var key in new[] {
                "Content-Type", "X-CLIENT-ID", "X-BUNDLE-ID", "X-LANGUAGE", "X-COUNTRY",
                "X-CURRENCY", "X-DEVICE-ID", "X-PLATFORM", "X-OS", "X-OS-AGENT",
                "X-SDK-VERSION", "Authorization", "X-Access-Token",
            })
            {
                var val = _request.GetRequestHeader(key);
                if (!string.IsNullOrEmpty(val))
                {
                    ex.RequestHeaders[key] = IsSensitiveHeader(key) ? "••••" : val;
                }
            }

            var body = Encoding.UTF8.GetString(_request.uploadHandler?.data ?? Array.Empty<byte>());
            ex.RequestBody = TruncateForInspector(body);
            return ex;
        }

        private void FireEndIfObserved(HttpExchange exchange, System.Diagnostics.Stopwatch sw, string response, HttpExchangeState state)
        {
            if (exchange == null) return;
            exchange.Status = (int)_request.responseCode;
            exchange.ResponseBody = TruncateForInspector(response);
            try
            {
                var headers = _request.GetResponseHeaders();
                if (headers != null)
                {
                    foreach (var kv in headers)
                    {
                        exchange.ResponseHeaders[kv.Key] = IsSensitiveHeader(kv.Key) ? "••••" : kv.Value;
                    }
                }
            }
            catch { /* response not available on some error paths */ }
            exchange.ElapsedMs = sw?.ElapsedMilliseconds ?? 0;
            exchange.State = state;
            HttpInspectorHooks.FireEnd(exchange);
        }

        private static bool IsSensitiveHeader(string key)
        {
            return key.Equals("Authorization",  StringComparison.OrdinalIgnoreCase)
                || key.Equals("X-Access-Token", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Cookie",         StringComparison.OrdinalIgnoreCase)
                || key.Equals("Set-Cookie",     StringComparison.OrdinalIgnoreCase);
        }

        private static string TruncateForInspector(string body)
        {
            if (string.IsNullOrEmpty(body)) return body ?? "";
            if (body.Length <= InspectorBodyCapBytes) return body;
            return body.Substring(0, InspectorBodyCapBytes) + "…[truncated]";
        }

        ~HttpRequest()
        {
            Dispose();
        }

        /// <summary>
        /// Disposes the underlying upload and download handlers to release native resources.
        /// </summary>
        public void Dispose()
        {
            _request.uploadHandler?.Dispose();
            _request.downloadHandler?.Dispose();
        }
    }
}
