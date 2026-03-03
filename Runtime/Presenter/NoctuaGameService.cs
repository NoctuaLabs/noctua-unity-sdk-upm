using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Handles game initialization and geo-IP detection by communicating with the Noctua backend API.
    /// </summary>
    internal class NoctuaGameService
    {
        private readonly string _clientId;
        private readonly string _baseUrl;
        private readonly bool _isOfflineFirst;
        private readonly ILogger _log = new NoctuaLogger();

        internal NoctuaGameService(Config config)
        {
            _clientId = config.ClientId;
            _baseUrl = config.BaseUrl;
            _isOfflineFirst = config.IsOfflineFirst;
        }

        /// <summary>
        /// Initializes the game by calling the backend <c>/games/init</c> endpoint
        /// to get game configuration, feature flags, and payment info.
        /// </summary>
        /// <returns>The server response containing game configuration data.</returns>
        public async UniTask<InitGameResponse> InitGameAsync()
        {
            if (string.IsNullOrEmpty(Application.identifier))
            {
                throw new ApplicationException($"App id for platform {Application.platform} is not set");
            }

            _log.Debug(
                "bundleId " + Application.identifier + 
                ", deviceId " + SystemInfo.deviceUniqueIdentifier +
                ", clientId " + _clientId
            );

            var request = new HttpRequest(HttpMethod.Get, $"{_baseUrl}/games/init")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier);

            InitGameResponse response;
            response = await request.Send<InitGameResponse>();

            return response;
        }

        /// <summary>
        /// Detects the user's country code by querying the Cloudflare CDN trace endpoint.
        /// </summary>
        /// <returns>An ISO 3166-1 alpha-2 country code (e.g. "US", "ID"), or <c>null</c> if not found.</returns>
        public async UniTask<string> GetCountryIDFromCloudflareTraceAsync()
        {
            // Extract domain from baseUrl
            Uri baseUri = new Uri(_baseUrl);
            string domain = baseUri.Host;
            _log.Debug($"Domain extracted from baseUrl: {domain}");
            var request = new HttpRequest(HttpMethod.Get, $"https://{domain}/cdn-cgi/trace")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier);

            string responseText = await request.SendRaw();
            
            // Parse the response to get the 'loc' value
            string locValue = null;
            string[] lines = responseText.Split('\n');
            foreach (string line in lines)
            {
                if (line.StartsWith("loc="))
                {
                    locValue = line.Substring(4).Trim();
                    break;
                }
            }

            _log.Debug($"Location value: {locValue}");

            return locValue;
        }

        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
            public bool IsOfflineFirst;
        }
    }
}