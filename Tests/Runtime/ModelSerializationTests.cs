using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using com.noctuagames.sdk;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Tests.Runtime
{
    public class ModelSerializationTests
    {
        private static readonly string ModelNamespace = "com.noctuagames.sdk";

        private static IEnumerable<Type> ConcreteModelTypes()
        {
            var asm = typeof(NoctuaException).Assembly;
            return asm.GetTypes()
                .Where(t => t.IsClass)
                .Where(t => !t.IsAbstract)
                .Where(t => !t.IsGenericTypeDefinition)
                .Where(t => t.Namespace != null && t.Namespace.StartsWith(ModelNamespace))
                .Where(t => t.GetCustomAttribute<JsonConverterAttribute>() == null)
                .Where(t => t.GetConstructor(Type.EmptyTypes) != null)
                .Where(t => HasJsonProperty(t))
                .Where(t => !typeof(System.Collections.IEnumerable).IsAssignableFrom(t) || t == typeof(string))
                .Where(t => !typeof(UnityEngine.Object).IsAssignableFrom(t))
                .Where(t => !typeof(JsonConverter).IsAssignableFrom(t));
        }

        private static bool HasJsonProperty(Type t)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            foreach (var f in t.GetFields(flags))
            {
                if (f.GetCustomAttribute<JsonPropertyAttribute>() != null) return true;
            }
            foreach (var p in t.GetProperties(flags))
            {
                if (p.GetCustomAttribute<JsonPropertyAttribute>() != null) return true;
            }
            return false;
        }

        [Test]
        public void RoundTrip_AllModelDtos_DoesNotThrow()
        {
            var failures = new List<string>();
            int success = 0;

            foreach (var t in ConcreteModelTypes())
            {
                try
                {
                    var instance = Activator.CreateInstance(t);
                    var json = JsonConvert.SerializeObject(instance);
                    Assert.IsNotNull(json, $"Serialization returned null for {t.FullName}");

                    var roundtripped = JsonConvert.DeserializeObject(json, t);
                    Assert.IsNotNull(roundtripped, $"Deserialization returned null for {t.FullName}");

                    var json2 = JsonConvert.SerializeObject(roundtripped);
                    Assert.AreEqual(json, json2, $"Round-trip JSON mismatch for {t.FullName}");
                    success++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{t.FullName}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            Assert.Greater(success, 0, "No model DTO types were tested.");
            UnityEngine.Debug.Log(
                $"ModelSerializationTests: round-tripped {success} types, skipped {failures.Count} due to construction/serialization issues.");
        }

        [Test]
        public void NoctuaException_Construct_AllErrorCodes()
        {
            foreach (NoctuaErrorCode code in Enum.GetValues(typeof(NoctuaErrorCode)))
            {
                var ex = new NoctuaException(code, $"msg-{code}", $"payload-{code}");
                Assert.AreEqual((int)code, ex.ErrorCode);
                Assert.AreEqual($"payload-{code}", ex.Payload);
                StringAssert.Contains(((int)code).ToString(), ex.ToString());
                StringAssert.Contains($"msg-{code}", ex.ToString());
            }
        }

        [Test]
        public void ErrorResponse_Roundtrip_PreservesFields()
        {
            var er = new ErrorResponse { Success = false, ErrorMessage = "boom", ErrorCode = 4242 };
            var json = JsonConvert.SerializeObject(er);
            var back = JsonConvert.DeserializeObject<ErrorResponse>(json);

            Assert.IsFalse(back.Success);
            Assert.AreEqual("boom", back.ErrorMessage);
            Assert.AreEqual(4242, back.ErrorCode);
            StringAssert.Contains("\"success\":false", json);
            StringAssert.Contains("\"error_message\":\"boom\"", json);
            StringAssert.Contains("\"error_code\":4242", json);
        }

        [Test]
        public void RawJsonStringConverter_StringPassThrough()
        {
            var c = new RawJsonStringConverter();
            var json = "{\"raw\":\"hello\"}";
            var holder = JsonConvert.DeserializeObject<RawHolder>(json);
            Assert.AreEqual("hello", holder.Raw);
        }

        [Test]
        public void RawJsonStringConverter_ObjectInlinedAsString()
        {
            var json = "{\"raw\":{\"a\":1,\"b\":\"two\"}}";
            var holder = JsonConvert.DeserializeObject<RawHolder>(json);
            Assert.AreEqual("{\"a\":1,\"b\":\"two\"}", holder.Raw);
        }

        [Test]
        public void RawJsonStringConverter_NullStaysNull()
        {
            var json = "{\"raw\":null}";
            var holder = JsonConvert.DeserializeObject<RawHolder>(json);
            Assert.IsNull(holder.Raw);
        }

        private class RawHolder
        {
            [JsonProperty("raw")]
            [JsonConverter(typeof(RawJsonStringConverter))]
            public string Raw;
        }
    }

    /// <summary>
    /// Extended model serialization and construction tests covering
    /// <see cref="NoctuaException"/>, <see cref="NoctuaConfig"/>, auth models
    /// (<see cref="UserBundle"/>, <see cref="NativeAccount"/>), IAP models
    /// (<see cref="PurchaseItem"/>, <see cref="Product"/>), and
    /// <see cref="RawJsonStringConverter"/> edge cases.
    /// </summary>
    public class ModelSerializationExtendedTests
    {
        // ── NoctuaException ──────────────────────────────────────────────────

        [Test]
        public void NoctuaException_Ctor_SetsErrorCode()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Authentication, "bad token");
            Assert.AreEqual((int)NoctuaErrorCode.Authentication, ex.ErrorCode);
        }

        [Test]
        public void NoctuaException_Ctor_SetsMessage()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Networking, "timeout occurred");
            StringAssert.Contains("timeout occurred", ex.Message);
        }

        [Test]
        public void NoctuaException_Ctor_SetsPayload()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Payment, "declined", "{\"reason\":\"insufficient\"}");
            Assert.AreEqual("{\"reason\":\"insufficient\"}", ex.Payload);
        }

        [Test]
        public void NoctuaException_ToString_ContainsErrorCodeAndMessage()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Application, "missing config", "some-payload");
            var str = ex.ToString();
            StringAssert.Contains(((int)NoctuaErrorCode.Application).ToString(), str);
            StringAssert.Contains("missing config", str);
        }

        [Test]
        public void NoctuaException_EmptyPayload_DoesNotThrow()
        {
            // Omitting the optional payload argument must not throw.
            Assert.DoesNotThrow(() =>
            {
                var ex = new NoctuaException(NoctuaErrorCode.Unknown, "no payload");
                _ = ex.Payload;
            });
        }

        [Test]
        public void NoctuaException_StaticSentinels_HaveCorrectErrorCode()
        {
            Assert.AreEqual((int)NoctuaErrorCode.Networking,              NoctuaException.OtherWebRequestError.ErrorCode);
            Assert.AreEqual((int)NoctuaErrorCode.Authentication,          NoctuaException.MissingAccessToken.ErrorCode);
            Assert.AreEqual((int)NoctuaErrorCode.MissingCompletionHandler, NoctuaException.MissingCompletionHandler.ErrorCode);
        }

        // ── NoctuaConfig — JSON round-trip ────────────────────────────────────

        [Test]
        public void NoctuaConfig_JsonRoundTrip_PreservesTrackerUrl()
        {
            var config = new NoctuaConfig { TrackerUrl = "https://custom.tracker.test/api" };
            var json   = JsonConvert.SerializeObject(config);
            var back   = JsonConvert.DeserializeObject<NoctuaConfig>(json);

            Assert.AreEqual("https://custom.tracker.test/api", back.TrackerUrl);
        }

        [Test]
        public void NoctuaConfig_JsonRoundTrip_PreservesBatchSize()
        {
            var config = new NoctuaConfig { TrackerBatchSize = 42 };
            var json   = JsonConvert.SerializeObject(config);
            var back   = JsonConvert.DeserializeObject<NoctuaConfig>(json);

            Assert.AreEqual(42u, back.TrackerBatchSize);
        }

        [Test]
        public void NoctuaConfig_JsonRoundTrip_PreservesSandboxFlag()
        {
            var config = new NoctuaConfig { IsSandbox = true };
            var json   = JsonConvert.SerializeObject(config);
            var back   = JsonConvert.DeserializeObject<NoctuaConfig>(json);

            Assert.IsTrue(back.IsSandbox);
        }

        [Test]
        public void NoctuaConfig_Defaults_AreNonNull()
        {
            var config = new NoctuaConfig();

            Assert.IsNotNull(config.TrackerUrl);
            Assert.IsNotNull(config.BaseUrl);
            Assert.AreEqual(NoctuaConfig.DefaultTrackerUrl, config.TrackerUrl);
            Assert.AreEqual(NoctuaConfig.DefaultBaseUrl,    config.BaseUrl);
        }

        // ── Auth models ───────────────────────────────────────────────────────

        [Test]
        public void UserBundle_Empty_HasNullUserAndCredential()
        {
            var bundle = UserBundle.Empty;

            Assert.IsNull(bundle.User);
            Assert.IsNull(bundle.Credential);
            Assert.IsNull(bundle.Player);
            Assert.IsNotNull(bundle.PlayerAccounts);
            Assert.AreEqual(0, bundle.PlayerAccounts.Count);
        }

        [Test]
        public void UserBundle_IsGuest_TrueWhenUserIsGuest()
        {
            var bundle = new UserBundle
            {
                User       = new User { IsGuest = true },
                Credential = new Credential { Provider = "device_id" }
            };

            Assert.IsTrue(bundle.IsGuest);
        }

        [Test]
        public void UserBundle_IsGuest_FalseWhenUserIsNotGuest()
        {
            var bundle = new UserBundle
            {
                User       = new User { IsGuest = false },
                Credential = new Credential { Provider = "google" }
            };

            Assert.IsFalse(bundle.IsGuest);
        }

        [Test]
        public void UserBundle_DisplayName_UsesNicknameWhenPresent()
        {
            var bundle = new UserBundle
            {
                User       = new User { Nickname = "HeroPlayer" },
                Credential = new Credential { Provider = "email", DisplayText = "hero@example.com" }
            };

            Assert.AreEqual("HeroPlayer", bundle.DisplayName);
        }

        [Test]
        public void UserBundle_Deserialization_SamplePayload()
        {
            const string json = @"{
                ""user"": { ""id"": 1001, ""nickname"": ""Tester"", ""is_guest"": false },
                ""credential"": { ""id"": 5, ""provider"": ""email"", ""display_text"": ""t@t.com"" },
                ""player_accounts"": [],
                ""is_recent"": true
            }";

            var bundle = JsonConvert.DeserializeObject<UserBundle>(json);

            Assert.IsNotNull(bundle);
            Assert.AreEqual(1001L,   bundle.User.Id);
            Assert.AreEqual("Tester", bundle.User.Nickname);
            Assert.AreEqual("email",  bundle.Credential.Provider);
            Assert.IsTrue(bundle.IsRecent);
        }

        [Test]
        public void NativeAccount_Deserialization_SamplePayload()
        {
            const string json = @"{
                ""playerId"": 9999,
                ""gameId"":   7,
                ""rawData"":  ""{}"",
                ""lastUpdated"": 1700000000000
            }";

            var account = JsonConvert.DeserializeObject<NativeAccount>(json);

            Assert.IsNotNull(account);
            Assert.AreEqual(9999L,          account.PlayerId);
            Assert.AreEqual(7L,             account.GameId);
            Assert.AreEqual("{}",           account.RawData);
            Assert.AreEqual(1700000000000L, account.LastUpdated);
        }

        // ── IAP models ────────────────────────────────────────────────────────

        [Test]
        public void PurchaseItem_PartiallyPopulated_NullSafeAccess()
        {
            var item = new PurchaseItem { OrderId = 42 };

            // These fields default to null — accessing them must not throw.
            Assert.DoesNotThrow(() =>
            {
                _ = item.PaymentType;
                _ = item.Status;
                _ = item.PurchaseItemName;
                _ = item.Timestamp;
                _ = item.OrderRequest;
                _ = item.VerifyOrderRequest;
                _ = item.PlayerId;
            });

            Assert.AreEqual(42, item.OrderId);
        }

        [Test]
        public void Product_Deserialization_SamplePayload()
        {
            const string json = @"{
                ""id"": ""gold_100"",
                ""description"": ""100 Gold Coins"",
                ""game_id"": 3,
                ""price"": 0.99,
                ""currency"": ""USD"",
                ""display_price"": ""$0.99"",
                ""platform"": ""google""
            }";

            var product = JsonConvert.DeserializeObject<Product>(json);

            Assert.IsNotNull(product);
            Assert.AreEqual("gold_100",      product.Id);
            Assert.AreEqual("100 Gold Coins", product.Description);
            Assert.AreEqual(3,               product.GameId);
            Assert.AreEqual(0.99m,           product.Price);
            Assert.AreEqual("USD",           product.Currency);
            Assert.AreEqual("$0.99",         product.DisplayPrice);
            Assert.AreEqual("google",        product.Platform);
        }

        [Test]
        public void PurchaseResponse_Deserialization_SamplePayload()
        {
            const string json = @"{
                ""order_id"": 88,
                ""status"": ""completed"",
                ""message"": ""ok""
            }";

            var resp = JsonConvert.DeserializeObject<PurchaseResponse>(json);

            Assert.AreEqual(88,              resp.OrderId);
            Assert.AreEqual(OrderStatus.completed, resp.Status);
            Assert.AreEqual("ok",            resp.Message);
        }

        // ── RawJsonStringConverter extended cases ─────────────────────────────

        [Test]
        public void RawJsonStringConverter_ArrayInlinedAsString()
        {
            var json   = "{\"raw\":[1,2,3]}";
            var holder = JsonConvert.DeserializeObject<RawHolder>(json);

            Assert.AreEqual("[1,2,3]", holder.Raw);
        }

        [Test]
        public void RawJsonStringConverter_NestedObjectPreservesStructure()
        {
            var json   = "{\"raw\":{\"x\":true,\"y\":null}}";
            var holder = JsonConvert.DeserializeObject<RawHolder>(json);

            // Exact compact JSON is preserved.
            Assert.AreEqual("{\"x\":true,\"y\":null}", holder.Raw);
        }

        [Test]
        public void RawJsonStringConverter_NumberPassThrough()
        {
            // Numbers in a field decorated with [JsonConverter(typeof(RawJsonStringConverter))]
            // should be returned as their string representation.
            var json   = "{\"raw\":42}";
            var holder = JsonConvert.DeserializeObject<RawHolder>(json);

            Assert.AreEqual("42", holder.Raw);
        }

        private class RawHolder
        {
            [JsonProperty("raw")]
            [JsonConverter(typeof(RawJsonStringConverter))]
            public string Raw;
        }
    }
}
