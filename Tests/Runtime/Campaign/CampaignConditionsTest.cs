using System.Collections.Generic;
using com.noctuagames.sdk.LiveOpsCampaign;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Tests.Runtime.Campaign
{
    [TestFixture]
    public class CampaignConditionsTest
    {
        private static readonly Dictionary<string, string> Data = new Dictionary<string, string>
        {
            { "progress", "12" },
            { "target", "20" },
            { "claimed", "false" },
            { "word", "abc" },
        };

        private static CampaignCondition All(params CampaignConditionClause[] clauses) =>
            new CampaignCondition { All = new List<CampaignConditionClause>(clauses) };

        private static CampaignConditionClause Clause(string left, string op, string right) =>
            new CampaignConditionClause { Left = left, Op = op, Right = right };

        [TestCase("{{progress}}", "lt", "{{target}}", true)]
        [TestCase("{{progress}}", "lte", "12", true)]
        [TestCase("{{progress}}", "gt", "{{target}}", false)]
        [TestCase("{{progress}}", "gte", "12", true)]
        [TestCase("{{claimed}}", "eq", "false", true)]
        [TestCase("{{claimed}}", "ne", "true", true)]
        [TestCase("{{word}}", "eq", "abc", true)]
        public void Clause_EvaluatesEachOperator(string left, string op, string right, bool expected)
        {
            Assert.AreEqual(expected, CampaignConditions.Evaluate(All(Clause(left, op, right)), Data));
        }

        [Test]
        public void NumericOps_CompareNumbersNotStrings()
        {
            // As strings "9" > "10"; as numbers it is not.
            var data = new Dictionary<string, string> { { "a", "9" } };
            Assert.IsTrue(CampaignConditions.Evaluate(All(Clause("{{a}}", "lt", "10")), data));
        }

        [TestCase("gt")]
        [TestCase("lt")]
        [TestCase("gte")]
        [TestCase("lte")]
        public void NumericOp_WithNonNumber_IsFalse(string op)
        {
            Assert.IsFalse(CampaignConditions.Evaluate(All(Clause("{{word}}", op, "1")), Data));
        }

        [Test]
        public void MissingToken_ResolvesToEmpty()
        {
            Assert.IsTrue(CampaignConditions.Evaluate(All(Clause("{{nope}}", "eq", "")), Data));
        }

        [Test]
        public void All_RequiresEveryClause()
        {
            var claimable = All(Clause("{{progress}}", "gte", "{{target}}"), Clause("{{claimed}}", "ne", "true"));
            Assert.IsFalse(CampaignConditions.Evaluate(claimable, Data));

            var done = new Dictionary<string, string>(Data) { ["progress"] = "20" };
            Assert.IsTrue(CampaignConditions.Evaluate(claimable, done));
        }

        [Test]
        public void NullOrEmptyCondition_IsVisible()
        {
            Assert.IsTrue(CampaignConditions.Evaluate(null, Data));
            Assert.IsTrue(CampaignConditions.Evaluate(All(), Data));
        }

        [Test]
        public void TryValidate_RejectsUnknownOperator()
        {
            Assert.IsFalse(CampaignConditions.TryValidate(All(Clause("a", "contains", "b")), out var error));
            StringAssert.Contains("contains", error);
        }

        [Test]
        public void TryValidate_RejectsNullClause()
        {
            Assert.IsFalse(CampaignConditions.TryValidate(All((CampaignConditionClause)null), out _));
        }

        [Test]
        public void VisibleIf_DeserializesFromJson()
        {
            var node = JsonConvert.DeserializeObject<CampaignNode>(
                "{ \"type\": \"text\", \"visible_if\": { \"all\": [ " +
                "{ \"left\": \"{{progress}}\", \"op\": \"gte\", \"right\": \"20\" } ] } }");

            Assert.NotNull(node.VisibleIf);
            Assert.AreEqual(1, node.VisibleIf.All.Count);
            Assert.AreEqual("gte", node.VisibleIf.All[0].Op);
        }
    }
}
