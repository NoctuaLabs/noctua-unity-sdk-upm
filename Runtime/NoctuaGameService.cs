﻿using System;
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
    [Preserve]
    public class InitGameResponse
    {
        [JsonProperty("country")]
        public string Country;

        [JsonProperty("active_product_id")]
        public string ActiveProductId;

        [JsonProperty("remote_configs")]
        public RemoteConfigs RemoteConfigs;
    }

    [Preserve]
    public class RemoteConfigs
    {
        [JsonProperty("enabled_payment_types")]
        public List<string> EnabledPaymentTypes;
    }

    internal class NoctuaGameService
    {
        private readonly string _clientId;
        private readonly string _baseUrl;

        internal NoctuaGameService(Config config)
        {
            _clientId = config.ClientId;
            _baseUrl = config.BaseUrl;
        }

        public async UniTask<InitGameResponse> InitGameAsync()
        {
            Debug.Log("InitGame: " + Application.identifier + " " + SystemInfo.deviceUniqueIdentifier);
            if (string.IsNullOrEmpty(Application.identifier))
            {
                throw new ApplicationException($"App id for platform {Application.platform} is not set");
            }

            Debug.Log("ClientID: " + _clientId);
            Debug.Log("BundleID: " + Application.identifier);

            Debug.Log("ClientId: " + _clientId);
            var request = new HttpRequest(HttpMethod.Get, $"{_baseUrl}/games/init")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier);

            var response = await request.Send<InitGameResponse>();
            Debug.Log("InitGameResponse: " + response.Country + " " + response.ActiveProductId);
            return response;
        }

        public async UniTask<string> GetCountryIDFromCloudflareTraceAsync()
        {
            // Extract domain from baseUrl
            Uri baseUri = new Uri(_baseUrl);
            string domain = baseUri.Host;
            Debug.Log($"Domain extracted from baseUrl: {domain}");
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

            Debug.Log($"Location value: {locValue}");

            return locValue;
        }

        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
        }
    }
}