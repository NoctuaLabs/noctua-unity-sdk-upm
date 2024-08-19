using System;
using System.Collections.Generic;
using com.noctuagames.sdk;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using UnityEngine.Networking;

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
    
    
    internal class HttpRequest
    {
        private readonly UnityWebRequest _request = new UnityWebRequest();
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
            var rawBody = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body, _jsonSettings));
            _request.uploadHandler = new UploadHandlerRaw(rawBody);
            
            return this;
        }
        
        public HttpRequest WithRawBody(byte[] body)
        {
            _request.SetRequestHeader("Content-Type", "application/octet-stream");
            _request.uploadHandler = new UploadHandlerRaw(body);
            
            return this;
        }

        private class DataWrapper<T>
        {
            [JsonProperty("data")]
            public T Data;
        }
     
        public async UniTask<T> Send<T>()
        {
            Debug.Log("HttpRequest.Send");
            if (_request.url.Contains("{") || _request.url.Contains("}"))
            {
                throw new Exception($"There are still path parameters that are not replaced: {_request.url}");
            }
            
            _request.downloadHandler = new DownloadHandlerBuffer();
            
            try
            {
                await _request.SendWebRequest();
                var response = _request.downloadHandler.text;
                Debug.Log(response);
                switch (_request.result)
                {
                    case UnityWebRequest.Result.ConnectionError:
                        response = @"{
                            ""success"": false,
                            ""error"": ""Connection error"",
                            ""error_code"": 3001
                        }";
                        return JsonConvert.DeserializeObject<T>(response, _jsonSettings);
                    case UnityWebRequest.Result.DataProcessingError:
                        response = @"{
                            ""success"": false,
                            ""error"": ""Data processing error"",
                            ""error_code"": 3002
                        }";
                        return JsonConvert.DeserializeObject<T>(response, _jsonSettings);
                    case UnityWebRequest.Result.ProtocolError:
                        response = @"{
                            ""success"": false,
                            ""error"": ""Protocol error"",
                            ""error_code"": 3003
                        }";
                        return JsonConvert.DeserializeObject<T>(response, _jsonSettings);
                    // We don't handle this
                    // case UnityWebRequest.Result.InProgress:
                    default:
                        return JsonConvert.DeserializeObject<T>(response, _jsonSettings);
                }
            }
            catch (Exception e)
            {
                // Keep parse the response and pass the error message and error code as is
                var response = _request.downloadHandler.text;
                Debug.Log(response);
                return JsonConvert.DeserializeObject<T>(response, _jsonSettings);
            }
        }
    }
}