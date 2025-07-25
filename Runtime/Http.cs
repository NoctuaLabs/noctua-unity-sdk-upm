﻿using System;
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
    internal interface IHttpAuth
    {
        string Get();
    }


    internal class BasicAuth : IHttpAuth
    {
        private readonly string _username;
        private readonly string _password;

        public BasicAuth(string username, string password)
        {
            _username = username;
            _password = password;
        }

        public string Get()
        {
            return $"Basic {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_username}:{_password}"))}";
        }
    }


    internal class BearerAuth : IHttpAuth
    {
        private readonly string _token;

        public BearerAuth(string token)
        {
            _token = token;
        }

        public string Get()
        {
            return $"Bearer {_token}";
        }
    }


    public enum HttpMethod
    {
        Get,
        Post,
        Put,
        Delete,
        Patch
    }

    internal class HttpRequest : IDisposable
    {
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

        internal HttpRequest(HttpMethod method, string url)
        {
            _jsonSettings.Converters.Add(new StringEnumConverter());
            _request.method = method.ToString().ToUpper();
            _request.url = url;

            // Inject locale data
            _request.SetRequestHeader("Accept-Language", Noctua.Platform.Locale.GetLanguage());
            _request.SetRequestHeader("X-LANGUAGE", Noctua.Platform.Locale.GetLanguage());
            _request.SetRequestHeader("X-COUNTRY", Noctua.Platform.Locale.GetCountry());
            _request.SetRequestHeader("X-CURRENCY", Noctua.Platform.Locale.GetCurrency());
            _request.SetRequestHeader("X-DEVICE-ID", SystemInfo.deviceUniqueIdentifier);
            _request.SetRequestHeader("X-PLATFORM", Utility.GetPlatformType());
            _request.SetRequestHeader("X-OS-AGENT", SystemInfo.operatingSystem);
            _request.SetRequestHeader("X-OS", Application.platform.ToString().ToLower());
            
            _request.SetRequestHeader("X-SDK-VERSION", Assembly.GetExecutingAssembly().GetName().Version.ToString());

        }

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

        public HttpRequest WithAuth(IHttpAuth auth)
        {
            _request.SetRequestHeader("Authorization", auth.Get());

            return this;
        }

        public HttpRequest WithHeader(string key, string value)
        {
            _request.SetRequestHeader(key, value);

            return this;
        }

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

        public HttpRequest WithJsonBody<T>(T body)
        {
            _request.SetRequestHeader("Content-Type", "application/json");
            var jsonBody = JsonConvert.SerializeObject(body, _jsonSettings);
            var rawBody = Encoding.UTF8.GetBytes(jsonBody);

            _request.uploadHandler = new UploadHandlerRaw(rawBody);

            return this;
        }

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

        public HttpRequest WithRawBody(byte[] body)
        {
            _request.SetRequestHeader("Content-Type", "application/octet-stream");
            _request.uploadHandler = new UploadHandlerRaw(body);

            return this;
        }
        
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

        public async UniTask<string> SendRaw()
        {
            _request.downloadHandler = new DownloadHandlerBuffer();
            _request.timeout = 60;
            await _request.SendWebRequest();

            return _request.downloadHandler.text;
        }

        public async UniTask<T> Send<T>()
        {
            if (_request.url.Contains("{") || _request.url.Contains("}"))
            {
                _log.Error($"There are still path parameters that are not replaced: {_request.url}");

                throw NoctuaException.RequestUnreplacedParam;
            }

            _request.downloadHandler = new DownloadHandlerBuffer();
            var response = "";

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
                await _request.SendWebRequest();
                response = _request.downloadHandler.text;
            }
            catch (Exception e)
            {
                _log.Exception(e);
                
                switch (_request.result)
                {
                    case UnityWebRequest.Result.ConnectionError:
                        throw NoctuaException.RequestConnectionError;
                    case UnityWebRequest.Result.DataProcessingError:
                        throw NoctuaException.RequestDataProcessingError;
                    case UnityWebRequest.Result.InProgress:
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
                    
                    throw new NoctuaException(
                        NoctuaErrorCode.Application,
                            $"HTTP error {_request.responseCode}: {((HttpStatusCode)_request.responseCode).ToString()}"
                        );
                }
                
                _log.Error($"Noctua error {errorResponse.ErrorCode}: {errorResponse.ErrorMessage}");
                
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

                throw new NoctuaException(
                    NoctuaErrorCode.Networking,
                        $"HTTP error {_request.responseCode}: {((HttpStatusCode)_request.responseCode)}," +
                        $"Response: '{response}'"
                    );
            } else {
                try
                {
                    return JsonConvert.DeserializeObject<DataWrapper<T>>(response, _jsonSettings).Data;
                }
                catch (Exception e)
                {
                    _log.Exception(e);

                    throw new NoctuaException(
                        NoctuaErrorCode.Application,
                        $"Failed to parse response as {typeof(T).Name}: {response}. Error: {e.Message}"
                    );
                }
            }
        }

        ~HttpRequest()
        {
            Dispose();
        }

        public void Dispose()
        {
            _request.uploadHandler?.Dispose();
            _request.downloadHandler?.Dispose();
        }
    }
}
