using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.noctuagames.sdk
{
    internal class SocialAuthenticationService
    {
        private readonly NoctuaAuthenticationService _authService;
        private readonly GlobalConfig _config;
        private readonly ILogger _log = new NoctuaLogger();
        
        internal SocialAuthenticationService(NoctuaAuthenticationService authService, GlobalConfig config)
        {
            _authService = authService;
            _config = config;
        }
        
        public async UniTask<UserBundle> SocialLoginAsync(string provider)
        {
            if(IsVNLegalPurposeEnabled())
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Social Login is Disabled");
            }

            if (_authService == null)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "AuthService is not set");
            }
            
            var callbackDataMap = await GetSocialAuthParamsAsync(provider);

            var socialLoginRequest = new SocialLoginRequest
            {
                Code = callbackDataMap["code"],
                State = callbackDataMap["state"],
                RedirectUri = callbackDataMap["redirect_uri"]
            };

            return await _authService.SocialLoginAsync(provider, socialLoginRequest);
        }
        
        public async UniTask<PlayerToken> GetSocialLoginTokenAsync(string provider)
        {
            if(IsVNLegalPurposeEnabled())
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Social Login is Disabled");
            }

            if (_authService == null)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "AuthService is not set");
            }
            
            var callbackDataMap = await GetSocialAuthParamsAsync(provider);

            var socialLoginRequest = new SocialLoginRequest
            {
                Code = callbackDataMap["code"],
                State = callbackDataMap["state"],
                RedirectUri = callbackDataMap["redirect_uri"],
                NoBindGuest = true
            };

            return await _authService.GetSocialLoginTokenAsync(provider, socialLoginRequest);
        }

        public async UniTask<Credential> SocialLinkAsync(string provider)
        {
            if (_authService == null)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "AuthService is not set");
            }

            var callbackDataMap = await GetSocialAuthParamsAsync(provider);
            
            var socialLinkRequest = new SocialLinkRequest
            {
                Code = callbackDataMap["code"],
                State = callbackDataMap["state"],
                RedirectUri = callbackDataMap["redirect_uri"]
            };

            return await _authService.SocialLinkAsync(provider, socialLinkRequest);
        }

        private async UniTask<Dictionary<string, string>> GetSocialAuthParamsAsync(string provider)
        {
#if (UNITY_STANDALONE || UNITY_EDITOR) && !UNITY_WEBGL

            // Start HTTP server to listen to the callback with random port
            // open the browser with the redirect URL

            var oauthRedirectListener = new OauthRedirectListener();

            var redirectUri = $"http://localhost:{oauthRedirectListener.Port}";

            var socialLoginUrl = await _authService.GetSocialAuthRedirectURLAsync(provider, redirectUri);

            _log.Debug($"Open URL with system browser: {socialLoginUrl}");

            Application.OpenURL(socialLoginUrl);
            
            var callbackData = await oauthRedirectListener.ListenAsync();
            var callbackDataMap = ParseQueryString(callbackData);
            
            callbackDataMap["redirect_uri"] = redirectUri;

#elif UNITY_IOS || UNITY_ANDROID
            
            _log.Debug("Initializing WebView");
            
            var gameObject = new GameObject("SocialLoginWebView");
            var uniWebView = gameObject.AddComponent<UniWebView>();
            
            uniWebView.OnOrientationChanged += (view, orietation) =>
            {
                uniWebView.Frame = new Rect(0, 0, Screen.width, Screen.height);
            };

            if (Application.platform == RuntimePlatform.Android)
            {
                if (provider == "facebook")
                {
                    uniWebView.SetUserAgent("Mozilla/5.0 (Linux; Android 4.4.4; One Build/KTU84L.H4) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/33.0.0.0 Mobile Safari/537.36 [FB_IAB/FB4A;FBAV/28.0.0.20.16;]");
                } else {
                    uniWebView.SetUserAgent("Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.0.0 Mobile Safari/537.3");
                }
            }
            else if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                uniWebView.SetUserAgent("Mozilla/5.0 (iPhone; CPU iPhone OS 14_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.0 Mobile/15E148 Safari/604.1");
            }
            
            var tcs = new UniTaskCompletionSource<string>();

            void OnSocialLoginWebviewClosed(UniWebView webView, string windowId)
            {
                tcs.TrySetException(new NoctuaException(NoctuaErrorCode.Authentication, $"{provider} login canceled"));
            }

            bool OnSocialLoginShouldClose(UniWebView webView)
            {
                if (webView.Url == null || !webView.Url.StartsWith($"{_config.Noctua.BaseUrl}/auth/{provider}/code?")) 
                {
                    _log.Debug("WebView closed by user before login completed");
                    var providerName = System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(provider);
                    tcs.TrySetException(new NoctuaException(NoctuaErrorCode.Authentication, $"{providerName} login canceled"));
                }
                else 
                {
                    tcs.TrySetResult(webView.Url);
                }

                return true;
            }
            
            void OnSocialLoginWebviewStarted(UniWebView webView, string url)
            {
                _log.Debug("URL started to load: " + url);

                if (url.StartsWith($"{_config.Noctua.BaseUrl}/auth/{provider}/code?")) 
                {
                    tcs.TrySetResult(url);
                }
                else if (url.Contains("error") && provider == "google") 
                { 
                    // "error" string does not apply for Facebook
                    var providerName = System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(provider);
                    tcs.TrySetException(new NoctuaException(NoctuaErrorCode.Authentication, $"{providerName} login failed"));
                }
            }

            void OnSocialLoginWebviewFinished(UniWebView webView, int statusCode, string url)
            {
                _log.Debug("URL finished to load: " + url);
                
                if (url.StartsWith($"{_config.Noctua.BaseUrl}/auth/{provider}/code?")) 
                {
                    tcs.TrySetResult(url);
                }
                else if (url.Contains("error") && provider == "google") 
                { 
                    // "error" string does not apply for Facebook
                    var providerName = System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(provider);
                    tcs.TrySetException(new NoctuaException(NoctuaErrorCode.Authentication, $"{providerName} login failed"));
                }
            }        

            uniWebView.OnPageFinished += OnSocialLoginWebviewFinished;
            uniWebView.OnPageStarted += OnSocialLoginWebviewStarted;
            uniWebView.OnMultipleWindowClosed += OnSocialLoginWebviewClosed;
            uniWebView.OnShouldClose += OnSocialLoginShouldClose;

            uniWebView.SetBackButtonEnabled(true);
            uniWebView.EmbeddedToolbar.SetBackgroundColor(new Color(31/255f, 35/255f, 39/255f));
            uniWebView.EmbeddedToolbar.Show();
            uniWebView.EmbeddedToolbar.SetDoneButtonText("Close");
            uniWebView.EmbeddedToolbar.SetButtonTextColor(Color.white);
            uniWebView.EmbeddedToolbar.SetPosition(UniWebViewToolbarPosition.Top);
            uniWebView.Frame = new Rect(0, 0, Screen.width, Screen.height);

            var socialLoginUrl = await _authService.GetSocialAuthRedirectURLAsync(provider);
            var socialLoginUrlQueries = ParseQueryString(socialLoginUrl);

            if (!socialLoginUrlQueries.ContainsKey("redirect_uri"))
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Redirect URI is not found in the social login URL");
            }

            _log.Debug("Showing WebView");
            uniWebView.Show();
            uniWebView.Load(socialLoginUrl);
            string callbackData = null;

            try 
            {
                callbackData = await tcs.Task;
            } 
            finally 
            {
                uniWebView.Hide();
                uniWebView.OnPageFinished -= OnSocialLoginWebviewFinished;
                uniWebView.OnPageStarted -= OnSocialLoginWebviewStarted;
                uniWebView.OnMultipleWindowClosed -= OnSocialLoginWebviewClosed;
                uniWebView.OnShouldClose -= OnSocialLoginShouldClose;

                Object.Destroy(gameObject);
            }

            var callbackDataMap = ParseQueryString(callbackData);
            
            callbackDataMap["redirect_uri"] = socialLoginUrlQueries["redirect_uri"];
#endif

            return callbackDataMap;
        }

        private static Dictionary<string, string> ParseQueryString(string queryString)
        {
            var queryParameters = new Dictionary<string, string>();
            queryString = queryString[(queryString.IndexOf('?') + 1)..];
            queryString = queryString.Split('#')[0];

            var pairs = queryString.Split('&');
            foreach (var pair in pairs)
            {
                var splitIndex = pair.IndexOf('=');

                if (splitIndex < 1 || splitIndex == pair.Length - 1)
                {
                    continue;
                }

                var key = Uri.UnescapeDataString(pair[..splitIndex]);
                var value = Uri.UnescapeDataString(pair[(splitIndex + 1)..]);
                queryParameters[key] = value;
            }

            return queryParameters;
        }

        private bool IsVNLegalPurposeEnabled()
        {
            return _config.Noctua.RemoteFeatureFlags.ContainsKey("vnLegalPurposeEnabled") == true && _config.Noctua.RemoteFeatureFlags["vnLegalPurposeEnabled"] == true;
        }

    }
   
    internal class OauthRedirectListener
    {
        private readonly HttpListener _listener = new();
        private readonly ILogger _log = new NoctuaLogger();

        public string Path;
        public int Port;

        public OauthRedirectListener(string path = "")
        {
            Path = path;
            Port = GetRandomUnusedPort();
            _listener.Prefixes.Add($"http://localhost:{Port}/{path.Trim('/')}/");
            
            _log.Debug($"HTTP Server started on port {Port} with path {Path}");
        }
        
        public async UniTask<string> ListenAsync()
        {
            _listener.Start();

            var contextTask = _listener.GetContextAsync();
            var cts = new CancellationTokenSource();
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(180), cts.Token);
            
            var completedTask = await Task.WhenAny(contextTask, timeoutTask);
            
            if (completedTask != contextTask)
            {
                throw new TimeoutException("Timeout while waiting for the HTTP server to respond");
            }
            
            cts.Cancel();
            
            var request = contextTask.Result.Request;
            var response = contextTask.Result.Response;
            
            if (request.HttpMethod != "GET")
            {
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                response.Close();
                
                throw new ArgumentException("Only GET method is allowed");
            }

            var callbackData = request.Url.Query;

            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "text/plain";
            var buffer = System.Text.Encoding.UTF8.GetBytes("Social login completed. You can close this window now.");
            response.ContentLength64 = buffer.Length;
            
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            
            response.Close();
            
            _listener.Stop();

            return callbackData;
        }

        private int GetRandomUnusedPort()
        {
            for (int port = 61000; port <= 61010; port++)
            {
                try
                {
                    var listener = new TcpListener(IPAddress.Loopback, port);
                    listener.Start();
                    listener.Stop();
                    return port;
                }
                catch (SocketException)
                {
                    continue;
                }
            }
            throw new Exception("No available ports found in range 61000-61010 for SSO redirect uri");
        }
    }
}