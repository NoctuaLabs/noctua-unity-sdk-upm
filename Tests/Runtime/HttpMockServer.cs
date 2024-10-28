using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Tests.Runtime
{
    public class RequestData
    {
        public string Method;
        public string Path;
        public NameValueCollection Headers;
        public string Body;
    }

    public class HttpMockServer : IDisposable
    {
        public readonly ConcurrentQueue<RequestData> Requests = new();

        private readonly HttpListener _listener;
        private readonly string _basePath;
        private readonly Dictionary<string, Func<HttpListenerRequest, string>> _handlers;

        public HttpMockServer(string prefix)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _basePath = new Uri(prefix).AbsolutePath;
            _handlers = new Dictionary<string, Func<HttpListenerRequest, string>>();
        }

        public void AddHandler(string path, Func<HttpListenerRequest, string> handler)
        {
            _handlers[$"{_basePath}{path[1..]}"] = handler;
        }

        public void RemoveHandler(string path)
        {
            _handlers.Remove($"{_basePath}{path[1..]}");
        }

        public void Start()
        {
            if (_listener.IsListening) throw new InvalidOperationException("Server is already running.");

            _listener.Start();
            _ = Task.Run(HandleIncomingConnections);

            Debug.Log("HttpMockServer started.");
        }

        private async Task HandleIncomingConnections()
        {
            while (_listener.IsListening)
            {
                var context = await _listener.GetContextAsync();
                var request = context.Request;
                using var response = context.Response;

                // Find the handler for the requested path
                if (_handlers.TryGetValue(request.Url.AbsolutePath, out var handler))
                {
                    var responseString = handler(request);

                    try
                    {
                        using var reader = new StreamReader(request.InputStream);
                        var requestString = await reader.ReadToEndAsync();

                        Requests.Enqueue(
                            new RequestData
                            {
                                Method = request.HttpMethod,
                                Path = request.Url.AbsolutePath,
                                Headers = request.Headers,
                                Body = requestString,
                            }
                        );

                        var buffer = Encoding.UTF8.GetBytes(responseString);
                        response.ContentLength64 = buffer.Length;
                        
                        response.StatusCode = (int)HttpStatusCode.OK;

                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                        
                        response.OutputStream.Close();
                    }
                    catch (Exception ex)
                    {
                        response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    }
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                }
            }
        }

        // Stops the mock server
        public void Stop()
        {
            if (!_listener.IsListening) throw new InvalidOperationException("Server is not running.");

            _listener.Stop();
            _listener.Close();

            Debug.Log("HttpMockServer stopped.");
        }

        public void Dispose()
        {
            Stop();
            _listener.Close();
        }
    }
}