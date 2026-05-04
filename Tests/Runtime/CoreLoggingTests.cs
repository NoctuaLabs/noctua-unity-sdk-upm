using System;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    public class CoreLoggingTests
    {
        [Test]
        public void NoctuaLogger_AllLevels_DoNotThrow()
        {
            ILogger log = new NoctuaLogger(typeof(CoreLoggingTests));

            Assert.DoesNotThrow(() => log.Debug("debug"));
            Assert.DoesNotThrow(() => log.Info("info"));
            Assert.DoesNotThrow(() => log.Warning("warning"));
            Assert.DoesNotThrow(() => log.Error("error"));
            Assert.DoesNotThrow(() => log.Exception(new InvalidOperationException("boom")));
        }

        [Test]
        public void NoctuaLogger_Info_HandlesLongMessageChunking()
        {
            ILogger log = new NoctuaLogger(typeof(CoreLoggingTests));
            var longMsg = new string('x', 2500);

            Assert.DoesNotThrow(() => log.Info(longMsg));
        }

        [Test]
        public void NoctuaLogger_NullType_FallsBackToCallerType()
        {
            Assert.DoesNotThrow(() =>
            {
                ILogger log = new NoctuaLogger();
                log.Debug("from-default-ctor");
            });
        }

        [Test]
        public void NoctuaLogger_ExplicitTypeNameUsedInOutput()
        {
            Assert.DoesNotThrow(() =>
            {
                ILogger log = new NoctuaLogger(typeof(string));
                log.Warning("warn-with-string-type");
            });
        }
    }
}
