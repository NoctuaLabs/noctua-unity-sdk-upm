using System.IO;
using System.Linq;
using com.noctuagames.sdk.LiveOpsCampaign;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Tests.Runtime.Campaign
{
    /// <summary>
    /// The payload's <c>i18n</c> block: per-language overrides of <c>data</c> keys that noctua-admin
    /// writes for every translated copy, image and label. The SDK lays the player's language over
    /// <c>data</c> before rendering; the base copy stays for any other language.
    /// </summary>
    [TestFixture]
    public class CampaignLanguageTest
    {
        private const string FixturePath =
            "Packages/com.noctuagames.sdk/Tests/Runtime/Campaign/Fixtures/single-offer-extras.json";

        private static CampaignItem Fixture() =>
            JsonConvert.DeserializeObject<CampaignItem>(File.ReadAllText(Path.GetFullPath(FixturePath)));

        [Test]
        public void I18n_DeserializesFromThePayload()
        {
            var item = Fixture();
            Assert.NotNull(item.I18n);
            Assert.AreEqual("PALING HEMAT", item.I18n["id"]["x_badge_text"]);
        }

        [TestCase("id")]
        [TestCase("ID")]
        [TestCase("id-ID")]
        [TestCase("id_ID")]
        public void ApplyLanguage_OverridesTheTranslatedKeys(string language)
        {
            var item = Fixture();
            var shown = NoctuaLiveOpsCampaign.ApplyLanguage(item, language);

            Assert.AreEqual("PALING HEMAT", shown.Data["x_badge_text"]);
            Assert.AreEqual(item.Data["title"], shown.Data["title"], "keys with no translation keep the base copy");
            Assert.AreEqual("BEST VALUE", item.Data["x_badge_text"], "the stored campaign is not changed");
        }

        [TestCase("en")]
        [TestCase("vi")]
        [TestCase("")]
        [TestCase(null)]
        public void ApplyLanguage_WithoutATranslation_ShowsTheBaseCopy(string language)
        {
            var item = Fixture();
            Assert.AreSame(item, NoctuaLiveOpsCampaign.ApplyLanguage(item, language));
        }

        [Test]
        public void ApplyLanguage_WithoutAnI18nBlock_ShowsTheBaseCopy()
        {
            var item = Fixture();
            item.I18n = null;
            Assert.AreSame(item, NoctuaLiveOpsCampaign.ApplyLanguage(item, "id"));
        }

        [Test]
        public void Renders_TheTranslatedCopy()
        {
            var item = NoctuaLiveOpsCampaign.ApplyLanguage(Fixture(), "id");
            var renderer = new CampaignRenderer(new RecordingActions().Dispatch, new FakeImageSource(), new FakeFontSource().GetFont);
            using var controller = new CampaignRuntimeController();

            var root = renderer.RenderCampaign(CampaignPlayerData.Merge(item, null), controller, 1);

            var texts = root.Query<TextElement>().ToList().Select(t => t.text).ToList();
            CollectionAssert.Contains(texts, "PALING HEMAT");
            CollectionAssert.DoesNotContain(texts, "BEST VALUE");
        }
    }
}
