using System;
using System.Text;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime.Auth
{
    public class AuthSchemeTest
    {
        [Test]
        public void BasicAuth_EncodesUserPassAsBase64()
        {
            var auth = new BasicAuth("alice", "s3cret");
            var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("alice:s3cret"));
            Assert.AreEqual(expected, auth.Get());
        }

        [Test]
        public void BasicAuth_HandlesEmptyCredentials()
        {
            var auth = new BasicAuth("", "");
            var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(":"));
            Assert.AreEqual(expected, auth.Get());
        }

        [Test]
        public void BasicAuth_HandlesUnicode()
        {
            var auth = new BasicAuth("bób", "páss");
            var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("bób:páss"));
            Assert.AreEqual(expected, auth.Get());
        }

        [Test]
        public void BearerAuth_PrefixesTokenWithBearer()
        {
            var auth = new BearerAuth("abc.def.ghi");
            Assert.AreEqual("Bearer abc.def.ghi", auth.Get());
        }

        [Test]
        public void BearerAuth_AllowsEmptyToken()
        {
            var auth = new BearerAuth("");
            Assert.AreEqual("Bearer ", auth.Get());
        }

        [Test]
        public void BearerAuth_ImplementsIHttpAuth()
        {
            IHttpAuth auth = new BearerAuth("t");
            Assert.AreEqual("Bearer t", auth.Get());
        }
    }
}
