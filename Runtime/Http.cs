using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Unity.Collections;
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


    public class HttpError : Exception
    {
        public long StatusCode { get; }

        public HttpError(long statusCode, string message) : base(message)
        {
            StatusCode = statusCode;
        }
    }


    internal class NetworkError : Exception
    {
        public NetworkError(string message) : base(message)
        {
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

        internal HttpRequest(HttpMethod method, string url)
        {
            _request.method = method.ToString().ToUpper();
            _request.url = url;
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

        [Preserve]
        private class DataWrapper<T>
        {
            [JsonProperty("data")]
            public T Data;
        }

        public async UniTask<string> SendRaw()
        {
            _request.downloadHandler = new DownloadHandlerBuffer();
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
            
            _log.Debug(
                $"=> {_request.method} {_request.url}\n"                         +
                $"Content-Type: {_request.GetRequestHeader("Content-Type")}\n"   +
                $"{auth}\n" +
                $"X-CLIENT-ID: {_request.GetRequestHeader("X-CLIENT-ID")}\n"     +
                $"X-BUNDLE-ID: {_request.GetRequestHeader("X-BUNDLE-ID")}\n\n"   +
                $"{Encoding.UTF8.GetString(_request.uploadHandler?.data ?? Array.Empty<byte>())}"
            );

            try
            {
                await _request.SendWebRequest();
                response = _request.downloadHandler.text;
            }
            catch (Exception)
            {
                switch (_request.result)
                {
                    case UnityWebRequest.Result.ConnectionError:     throw NoctuaException.RequestConnectionError;
                    case UnityWebRequest.Result.DataProcessingError: throw NoctuaException.RequestDataProcessingError;
                }

                response = _request.downloadHandler.text;
            }
            finally
            {
                _request.downloadHandler.Dispose();
                _request.uploadHandler?.Dispose();
            }

            _log.Debug(
                $"<= {_request.responseCode} {(HttpStatusCode)_request.responseCode} {_request.method} {_request.url}\n\n" +
                $"{response}"
            );

            if (_request.responseCode >= 500)
            {
                throw new NoctuaException(
                    NoctuaErrorCode.Networking,
                    $"Server error {_request.responseCode}: {response}"
                );
            }

            if (_request.responseCode >= 400)
            {
                ErrorResponse errorResponse;

                try
                {
                    errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(response, _jsonSettings);
                }
                catch (Exception)
                {
                    throw new NoctuaException(
                        NoctuaErrorCode.Application,
                        $"Unknown HTTP error {_request.responseCode}: {response}"
                    );
                }

                throw new NoctuaException((NoctuaErrorCode)errorResponse.ErrorCode, errorResponse.ErrorMessage);
            }

            try
            {
                return JsonConvert.DeserializeObject<DataWrapper<T>>(response, _jsonSettings).Data;
            }
            catch (Exception e)
            {
                _log.Info(e.Message);

                throw new NoctuaException(
                    NoctuaErrorCode.Application,
                    $"Failed to parse response as {typeof(T).Name}: {response}. Error: {e.Message}"
                );
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