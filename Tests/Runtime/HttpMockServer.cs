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
        // ConcurrentDictionary (not Dictionary<>) because AddHandler/RemoveHandler
        // are called from the test thread while HandleIncomingConnections reads
        // _handlers from a background Task.Run thread. A non-thread-safe Dictionary<>
        // can corrupt its internal bucket chains under concurrent read/write,
        // causing TryGetValue to spin forever — which makes UnityWebRequest hang
        // waiting for a response that will never come, freezing the editor.
        private readonly ConcurrentDictionary<string, Func<HttpListenerRequest, string>> _handlers;

        public HttpMockServer(string prefix)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _basePath = new Uri(prefix).AbsolutePath;
            _handlers = new ConcurrentDictionary<string, Func<HttpListenerRequest, string>>();
        }

        public void AddHandler(string path, Func<HttpListenerRequest, string> handler)
        {
            _handlers[$"{_basePath}{path[1..]}"] = handler;
        }

        public void RemoveHandler(string path)
        {
            _handlers.TryRemove($"{_basePath}{path[1..]}", out _);
        }

        public void Start()
        {
            if (_listener.IsListening) throw new InvalidOperationException("Server is already running.");

            const int maxRetries = 30;
            Exception lastEx = null;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    _listener.Start();
                    _ = Task.Run(HandleIncomingConnections);
                    Debug.Log("HttpMockServer started.");
                    return;
                }
                catch (Exception ex) when (ex is HttpListenerException || ex is System.Net.Sockets.SocketException)
                {
                    lastEx = ex;
                    System.Threading.Thread.Sleep(500);
                }
            }

            throw new InvalidOperationException($"HttpMockServer: port still in use after {maxRetries} retries.", lastEx);
        }

        private async Task HandleIncomingConnections()
        {
            while (_listener.IsListening)
            {
                // Outer try/catch around the entire iteration so that ANY uncaught
                // exception (handler lambda throwing, listener stopped mid-await,
                // stream write failure, etc.) does not terminate the background
                // task. If this task dies, all subsequent test HTTP requests hang
                // forever waiting for a response that will never come — which is
                // exactly the "Unity not responding" symptom these tests showed.
                try
                {
                    var context = await _listener.GetContextAsync();
                    var request = context.Request;
                    using var response = context.Response;

                    // Find the handler for the requested path
                    if (_handlers.TryGetValue(request.Url.AbsolutePath, out var handler))
                    {
                        string responseString;
                        try { responseString = handler(request); }
                        catch (Exception handlerEx)
                        {
                            Debug.LogWarning($"HttpMockServer: handler threw — returning 500. {handlerEx}");
                            response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            response.ContentLength64 = 0;
                            response.OutputStream.Close();
                            continue;
                        }

                        try
                        {
                            using var reader = new StreamReader(request.InputStream);
                            var requestString = await reader.ReadToEndAsync();

                            Requests.Enqueue(
                                new RequestData
                                {
                                    Method = request.HttpMethod,
                                    Path = request.Url.PathAndQuery,
                                    Headers = request.Headers,
                                    Body = requestString,
                                }
                            );

                            if (responseString == null)
                            {
                                // Null return: convention for "send HTTP 500 with empty body".
                                // Explicitly close the OutputStream so the full HTTP response
                                // (status line + headers + empty body) is flushed before the
                                // connection is released. Without this, HttpListenerResponse.Dispose()
                                // may close the socket without a proper HTTP terminator, causing
                                // UnityWebRequest to throw rather than return result=ProtocolError.
                                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                                response.ContentLength64 = 0;
                                response.OutputStream.Close();
                            }
                            else
                            {
                                var buffer = Encoding.UTF8.GetBytes(responseString);
                                response.ContentLength64 = buffer.Length;
                                response.StatusCode = (int)HttpStatusCode.OK;
                                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                                response.OutputStream.Close();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"HttpMockServer: response IO failed — returning 500. {ex}");
                            response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        }
                    }
                    else
                    {
                        // No handler matched — explicitly close the response so the
                        // 404 status line + headers are flushed and the client sees
                        // a proper HTTP termination (not a hung connection).
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        response.ContentLength64 = 0;
                        response.OutputStream.Close();
                    }
                }
                catch (HttpListenerException)
                {
                    // Listener was stopped (Stop()/Dispose() raced this loop).
                    // Exit the while normally on next IsListening check.
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception loopEx)
                {
                    // Anything else: log and KEEP the loop alive. Killing the
                    // loop is the worst failure mode because the next test will
                    // hang forever waiting for a response.
                    Debug.LogError($"HttpMockServer: unexpected exception in listener loop — staying alive. {loopEx}");
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
            if (_listener.IsListening)
                _listener.Stop();

            _listener.Close();
            Debug.Log("HttpMockServer stopped.");
        }

        /// <summary>
        /// Returns an OS-assigned free port. Uses TcpListener with port 0 so the kernel picks
        /// a port that is guaranteed free at the moment of the call.
        /// </summary>
        public static int FindFreePort()
        {
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}