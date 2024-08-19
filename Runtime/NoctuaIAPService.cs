using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine.Device;
using UnityEngine;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace com.noctuagames.sdk
{
    public partial class Product
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("game_id")]
        public long GameId { get; set; }

        [JsonProperty("vat_rate")]
        public long VatRate { get; set; }

        [JsonProperty("enabled_payment_types")]
        public object EnabledPaymentTypes { get; set; }

        [JsonProperty("price")]
        public double Price { get; set; }

        [JsonProperty("price_vat")]
        public double PriceVat { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }
    }

    [JsonArray]
    public class ProductList : List<Product>
    {
    }

    public partial class ProductListResponse
    {
        [JsonProperty("data")]
        public List<Product> Data { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("error_code")]
        public int ErrorCode { get; set; }
    }


    public class NoctuaIAPService
    {
        private readonly Config _config;

        private string _accessToken;

        internal NoctuaIAPService(Config config)
        {
            _config = config;
        }

        public async Task<ProductListResponse> GetProductListAsync(string gameId, string currency, string enabledPaymentTypes)
        {

            Debug.Log("NoctuaIAPService.GetProductListAsync");
            Debug.Log(_config.BaseUrl);
            Debug.Log(_config.ClientId);
            Debug.Log(gameId);
            Debug.Log(currency);
            Debug.Log(enabledPaymentTypes);

            var url = $"{_config.BaseUrl}/api/v1/products?game_id={gameId}&currency={currency}&enabled_payment_types={enabledPaymentTypes}";
            Debug.Log(url);

            var request = new HttpRequest(HttpMethod.Get, url)
                .WithHeader("X-CLIENT-ID", _config.ClientId);

            var response = await request.Send<ProductListResponse>();

            return response;
        }

        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
        }
    }
}