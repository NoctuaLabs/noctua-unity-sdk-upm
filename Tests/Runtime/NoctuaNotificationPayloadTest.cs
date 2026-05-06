using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="NoctuaNotificationPayload"/>:
    ///   — <see cref="NoctuaNotificationPayload.FromJson"/> for null, empty, iOS APS,
    ///     Android FCM, deeplink, and malformed inputs.
    ///   — <see cref="NoctuaNotificationPayload.GetCustomString"/> for null Custom,
    ///     missing key, and found key.
    ///
    /// All methods are pure JSON parsing with no network or Unity-runtime
    /// dependencies, so plain <c>[Test]</c> attributes suffice.
    /// </summary>
    [TestFixture]
    public class NoctuaNotificationPayloadTest
    {
        // ─── FromJson — null / empty ───────────────────────────────────────────

        [Test]
        public void FromJson_Null_ReturnsValidPayload()
        {
            var payload = NoctuaNotificationPayload.FromJson(null);

            Assert.IsNotNull(payload, "FromJson(null) must return a non-null payload");
        }

        [Test]
        public void FromJson_Null_RawJsonIsEmpty()
        {
            var payload = NoctuaNotificationPayload.FromJson(null);

            // RawJson falls back to "{}" when input is null
            Assert.IsNotNull(payload.RawJson);
        }

        [Test]
        public void FromJson_Null_TitleIsNullOrEmpty()
        {
            var payload = NoctuaNotificationPayload.FromJson(null);

            Assert.IsTrue(string.IsNullOrEmpty(payload.Title),
                "Title must be null/empty for a null input");
        }

        [Test]
        public void FromJson_EmptyString_ReturnsValidPayload()
        {
            var payload = NoctuaNotificationPayload.FromJson("");

            Assert.IsNotNull(payload);
        }

        [Test]
        public void FromJson_EmptyString_TitleIsNullOrEmpty()
        {
            var payload = NoctuaNotificationPayload.FromJson("");

            Assert.IsTrue(string.IsNullOrEmpty(payload.Title));
        }

        [Test]
        public void FromJson_EmptyString_DeeplinkIsNullOrEmpty()
        {
            var payload = NoctuaNotificationPayload.FromJson("");

            Assert.IsTrue(string.IsNullOrEmpty(payload.Deeplink));
        }

        // ─── FromJson — iOS APS format ─────────────────────────────────────────

        [Test]
        public void FromJson_IosApsPayload_ParsesTitle()
        {
            const string json = @"{
                ""aps"": {
                    ""alert"": {
                        ""title"": ""Level Up!"",
                        ""body"": ""You reached level 10.""
                    }
                }
            }";

            var payload = NoctuaNotificationPayload.FromJson(json);

            Assert.AreEqual("Level Up!", payload.Title);
        }

        [Test]
        public void FromJson_IosApsPayload_ParsesBody()
        {
            const string json = @"{
                ""aps"": {
                    ""alert"": {
                        ""title"": ""Level Up!"",
                        ""body"": ""You reached level 10.""
                    }
                }
            }";

            var payload = NoctuaNotificationPayload.FromJson(json);

            Assert.AreEqual("You reached level 10.", payload.Body);
        }

        [Test]
        public void FromJson_IosApsPayload_ApsIsNotNull()
        {
            const string json = @"{""aps"": {""alert"": {""title"": ""Hi"", ""body"": ""there""}}}";

            var payload = NoctuaNotificationPayload.FromJson(json);

            Assert.IsNotNull(payload.Aps, "Aps property must be populated from iOS payload");
        }

        [Test]
        public void FromJson_IosApsSimpleStringAlert_PopulatesBody()
        {
            // iOS simple alert form: "aps": {"alert": "hello world"}
            const string json = @"{""aps"": {""alert"": ""hello world""}}";

            var payload = NoctuaNotificationPayload.FromJson(json);

            Assert.AreEqual("hello world", payload.Body,
                "Simple string alert must be mapped to Body");
        }

        [Test]
        public void FromJson_IosApsSimpleStringAlert_TitleIsNullOrEmpty()
        {
            const string json = @"{""aps"": {""alert"": ""simple message""}}";

            var payload = NoctuaNotificationPayload.FromJson(json);

            // Simple string alert has no title
            Assert.IsTrue(string.IsNullOrEmpty(payload.Title),
                "Simple string alert form has no title");
        }

        // ─── FromJson — Android FCM format ────────────────────────────────────

        [Test]
        public void FromJson_AndroidFcmPayload_ParsesTitle()
        {
            const string json = @"{
                ""notification"": {
                    ""title"": ""Daily Reward"",
                    ""body"": ""Collect your daily reward now!""
                }
            }";

            var payload = NoctuaNotificationPayload.FromJson(json);

            Assert.AreEqual("Daily Reward", payload.Title);
        }

        [Test]
        public void FromJson_AndroidFcmPayload_ParsesBody()
        {
            const string json = @"{
                ""notification"": {
                    ""title"": ""Daily Reward"",
                    ""body"": ""Collect your daily reward now!""
                }
            }";

            var payload = NoctuaNotificationPayload.FromJson(json);

            Assert.AreEqual("Collect your daily reward now!", payload.Body);
        }

        [Test]
        public void FromJson_AndroidFcmPayload_ApsIsNull()
        {
            const string json = @"{""notification"": {""title"": ""test"", ""body"": ""msg""}}";

            var payload = NoctuaNotificationPayload.FromJson(json);

            Assert.IsNull(payload.Aps, "Android FCM payload must not populate the Aps field");
        }

        // ─── FromJson — deeplink fields ────────────────────────────────────────

        [Test]
        public void FromJson_WithDeeplinkField_PopulatesDeeplink()
        {
            const string json = @"{
                ""aps"": {""alert"": {""title"": ""t"", ""body"": ""b""}},
                ""deeplink"": ""myapp://store""
            }";

            var payload = NoctuaNotificationPayload.FromJson(json);

            Assert.AreEqual("myapp://store", payload.Deeplink);
        }

        [Test]
        public void FromJson_WithNoctuaDeeplinkField_PopulatesDeeplink()
        {
            const string json = @"{""noctua_deeplink"": ""myapp://events/summer""}";

            var payload = NoctuaNotificationPayload.FromJson(json);

            Assert.AreEqual("myapp://events/summer", payload.Deeplink);
        }

        [Test]
        public void FromJson_WithRouteField_PopulatesDeeplink()
        {
            const string json = @"{""route"": ""shop/featured""}";

            var payload = NoctuaNotificationPayload.FromJson(json);

            Assert.AreEqual("shop/featured", payload.Deeplink);
        }

        [Test]
        public void FromJson_WithLinkField_PopulatesDeeplink()
        {
            const string json = @"{""link"": ""https://example.com/offer""}";

            var payload = NoctuaNotificationPayload.FromJson(json);

            Assert.AreEqual("https://example.com/offer", payload.Deeplink);
        }

        [Test]
        public void FromJson_WithUrlField_PopulatesDeeplink()
        {
            const string json = @"{""url"": ""https://example.com/page""}";

            var payload = NoctuaNotificationPayload.FromJson(json);

            Assert.AreEqual("https://example.com/page", payload.Deeplink);
        }

        [Test]
        public void FromJson_DeeplinkFieldTakesPriorityOverRoute()
        {
            // "deeplink" appears before "route" in the priority list;
            // whichever field is found first (non-empty) wins.
            const string json = @"{""deeplink"": ""app://a"", ""route"": ""b/c""}";

            var payload = NoctuaNotificationPayload.FromJson(json);

            Assert.AreEqual("app://a", payload.Deeplink,
                "'deeplink' must take priority over 'route'");
        }

        [Test]
        public void FromJson_NoDeeplinkFields_DeeplinkIsEmpty()
        {
            const string json = @"{""aps"": {""alert"": {""title"": ""Hi"", ""body"": ""b""}}}";

            var payload = NoctuaNotificationPayload.FromJson(json);

            Assert.AreEqual(string.Empty, payload.Deeplink,
                "Deeplink must be empty string when no deeplink field is present");
        }

        // ─── FromJson — Custom fields ──────────────────────────────────────────

        [Test]
        public void FromJson_ExtraTopLevelFields_AppearInCustom()
        {
            const string json = @"{""game_mode"": ""pvp"", ""season"": ""3""}";

            var payload = NoctuaNotificationPayload.FromJson(json);

            Assert.IsNotNull(payload.Custom);
            Assert.IsTrue(payload.Custom.ContainsKey("game_mode"),
                "Custom top-level field 'game_mode' must be present in Custom");
        }

        [Test]
        public void FromJson_ApsKeyIsExcludedFromCustom()
        {
            const string json = @"{""aps"": {""alert"": {""title"": ""t""}}, ""extra"": ""val""}";

            var payload = NoctuaNotificationPayload.FromJson(json);

            Assert.IsFalse(payload.Custom.ContainsKey("aps"),
                "'aps' envelope must be excluded from Custom");
        }

        [Test]
        public void FromJson_NotificationKeyIsExcludedFromCustom()
        {
            const string json = @"{""notification"": {""title"": ""t""}, ""custom_key"": ""cv""}";

            var payload = NoctuaNotificationPayload.FromJson(json);

            Assert.IsFalse(payload.Custom.ContainsKey("notification"),
                "'notification' envelope must be excluded from Custom");
        }

        [Test]
        public void FromJson_RawJsonIsPreserved()
        {
            const string json = @"{""key"": ""value""}";

            var payload = NoctuaNotificationPayload.FromJson(json);

            Assert.AreEqual(json, payload.RawJson);
        }

        // ─── FromJson — malformed input ────────────────────────────────────────

        [Test]
        public void FromJson_InvalidJson_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => NoctuaNotificationPayload.FromJson("{not valid json{{"));
        }

        [Test]
        public void FromJson_InvalidJson_ReturnsValidPayload()
        {
            var payload = NoctuaNotificationPayload.FromJson("{not valid");

            Assert.IsNotNull(payload);
        }

        [Test]
        public void FromJson_InvalidJson_TitleIsNullOrEmpty()
        {
            var payload = NoctuaNotificationPayload.FromJson("{{{{");

            Assert.IsTrue(string.IsNullOrEmpty(payload.Title));
        }

        // ─── GetCustomString ───────────────────────────────────────────────────

        [Test]
        public void GetCustomString_OnDefaultInstance_ReturnsEmpty()
        {
            // Default constructor → Custom is null
            var payload = new NoctuaNotificationPayload();

            var result = payload.GetCustomString("any_key");

            Assert.AreEqual(string.Empty, result,
                "GetCustomString must return empty string when Custom is null");
        }

        [Test]
        public void GetCustomString_KeyNotPresent_ReturnsEmpty()
        {
            var payload = NoctuaNotificationPayload.FromJson(@"{""other"": ""val""}");

            var result = payload.GetCustomString("missing_key");

            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void GetCustomString_KeyPresent_ReturnsValue()
        {
            var payload = NoctuaNotificationPayload.FromJson(@"{""campaign_id"": ""summer2024""}");

            var result = payload.GetCustomString("campaign_id");

            Assert.AreEqual("summer2024", result);
        }

        [Test]
        public void GetCustomString_EmptyKeyString_ReturnsEmpty()
        {
            var payload = NoctuaNotificationPayload.FromJson(@"{""key"": ""val""}");

            var result = payload.GetCustomString("");

            Assert.AreEqual(string.Empty, result);
        }
    }
}
