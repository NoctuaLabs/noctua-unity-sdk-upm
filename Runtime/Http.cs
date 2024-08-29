using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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
        private readonly UnityWebRequest _request = new();
        private readonly Newtonsoft.Json.JsonSerializerSettings _jsonSettings = new()
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
            [JsonProperty("success")]
            public bool Success { get; set; }

            [JsonProperty("data")]
            public T Data { get; set; }

            // Parameterless constructor is not strictly required but can be useful for deserialization
            public DataWrapper() { }

            // Optional constructor for convenience
            public DataWrapper(bool success, T data)
            {
                Success = success;
                Data = data;
            }
        }

        public static string ConvertSnakeCaseToCamelCase(string json)
    {
        var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
        var jsonObject = ProcessJsonElement(jsonDoc.RootElement);
        return SerializeObjectToJson(jsonObject);
    }

    private static object ProcessJsonElement(System.Text.Json.JsonElement element)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                return ProcessJsonObject(element);
            case System.Text.Json.JsonValueKind.Array:
                return ProcessJsonArray(element);
            default:
                return element.ToString();  // Handle primitive types
        }
    }

    private static Dictionary<string, object> ProcessJsonObject(System.Text.Json.JsonElement element)
    {
        var jsonObject = new Dictionary<string, object>();

        foreach (var property in element.EnumerateObject())
        {
            var camelCaseName = ConvertSnakeToCamelCase(property.Name);
            jsonObject[camelCaseName] = ProcessJsonElement(property.Value);
        }

        return jsonObject;
    }

    private static List<object> ProcessJsonArray(System.Text.Json.JsonElement arrayElement)
    {
        var jsonArray = new List<object>();

        foreach (var item in arrayElement.EnumerateArray())
        {
            jsonArray.Add(ProcessJsonElement(item));
        }

        return jsonArray;
    }

    private static string ConvertSnakeToCamelCase(string snakeCase)
    {
        if (string.IsNullOrEmpty(snakeCase))
            return snakeCase;

        // Convert snake_case to camelCase
        var camelCase = Regex.Replace(snakeCase, "_([a-z])", match => match.Groups[1].Value.ToUpper());
        return char.ToLower(camelCase[0]) + camelCase.Substring(1);
    }

    private static string SerializeObjectToJson(object obj)
    {
        return System.Text.Json.JsonSerializer.Serialize(obj, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

        public async UniTask<T> Send<T>()
        {
            Debug.Log("HttpRequest.Send");
            Debug.Log(_request.url);

            if (_request.url.Contains("{") || _request.url.Contains("}"))
            {
                Debug.Log($"There are still path parameters that are not replaced: {_request.url}");
                throw NoctuaException.RequestUnreplacedParam;
            }
            
            _request.downloadHandler = new DownloadHandlerBuffer();
            string response = null;

            try {
                Debug.Log("Http.Send() -> _request.SendWebRequest()");
                await _request.SendWebRequest();
            } catch (Exception e) {
                Debug.Log("Http.Send() -> _request.SendWebRequest() -> Exception");
                Debug.Log(_request.result.ToString());
                response = _request.downloadHandler.text;
                Debug.Log(response);
                Debug.Log(e.Message);

                if (response == null || _request.result != UnityWebRequest.Result.Success) {
                    // Try to parse the error first to get the error code
                    ErrorResponse errorResponse = null;
                    errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(response, _jsonSettings);
                    if (errorResponse != null && errorResponse.ErrorCode > 0) {
                        throw new NoctuaException(errorResponse.ErrorCode, errorResponse.Error);
                    } else { // If there is no error code in the response, throw the original error
                        switch (_request.result)
                        {
                            case UnityWebRequest.Result.ConnectionError:
                                // e.g. the device is disconnected from the internet/network
                                throw NoctuaException.RequestConnectionError;
                            case UnityWebRequest.Result.DataProcessingError:
                                throw NoctuaException.RequestDataProcessingError;
                            case UnityWebRequest.Result.ProtocolError:
                                throw NoctuaException.RequestProtocolError;
                            default:
                                throw NoctuaException.OtherWebRequestError;
                        }
                    }
                }
            }

            Debug.Log("Http.Send() -> _request.SendWebRequest() -> Success");
            response = _request.downloadHandler.text;
            Debug.Log(response);
            #if UNITY_IOS
                Debug.Log("Http.Send() -> JsonUtility.FromJson");
                /* Not work: Exception: System.PlatformNotSupportedException: Operation is not supported on this platform.
                  at System.Reflection.Emit.DynamicMethod..ctor (System.String name, System.Type returnType, System.Type[]
                var CamelCaseResponse = ConvertSnakeCaseToCamelCase(response);
                var result =  JsonUtility.FromJson<DataWrapper<T>>(CamelCaseResponse).Data;
                */

                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = new SnakeCaseNamingPolicy(),
                    WriteIndented = true,
                };
                var result = System.Text.Json.JsonSerializer.Deserialize<DataWrapper<T>>(response, options);
                Debug.Log("Http.Send() -> parsed");
                Debug.Log(result);
                return result.Data;
            #else
                Debug.Log("Http.Send() -> JsonConvert.DeserializeObject");
                var result = JsonConvert.DeserializeObject<DataWrapper<T>>(response, _jsonSettings).Data;
                Debug.Log("Http.Send() -> parsed");
                Debug.Log(result);
            #endif
        }
    }
}