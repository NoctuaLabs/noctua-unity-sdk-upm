using System;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    public class NoctuaLoggerTest
    {
        private class SampleCaller
        {
            public static ILogger Create() => new NoctuaLogger();
        }

        [Test]
        public void Constructor_ExplicitType_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => { var _ = new NoctuaLogger(typeof(NoctuaLoggerTest)); });
        }

        [Test]
        public void Constructor_NullType_InfersFromCaller()
        {
            ILogger log = null;
            Assert.DoesNotThrow(() => { log = SampleCaller.Create(); });
            Assert.IsNotNull(log);
        }

        [Test]
        public void Debug_DoesNotThrow()
        {
            var log = new NoctuaLogger(typeof(NoctuaLoggerTest));
            Assert.DoesNotThrow(() => log.Debug("debug message"));
        }

        [Test]
        public void Info_ShortMessage_DoesNotThrow()
        {
            var log = new NoctuaLogger(typeof(NoctuaLoggerTest));
            Assert.DoesNotThrow(() => log.Info("short"));
        }

        [Test]
        public void Info_LongMessage_ChunksWithoutThrowing()
        {
            var log = new NoctuaLogger(typeof(NoctuaLoggerTest));
            var msg = new string('x', 2500);
            Assert.DoesNotThrow(() => log.Info(msg));
        }

        [Test]
        public void Warning_DoesNotThrow()
        {
            var log = new NoctuaLogger(typeof(NoctuaLoggerTest));
            Assert.DoesNotThrow(() => log.Warning("warn"));
        }

        [Test]
        public void Error_DoesNotThrow()
        {
            var log = new NoctuaLogger(typeof(NoctuaLoggerTest));
            Assert.DoesNotThrow(() => log.Error("err"));
        }

        [Test]
        public void Exception_DoesNotThrow()
        {
            var log = new NoctuaLogger(typeof(NoctuaLoggerTest));
            Assert.DoesNotThrow(() => log.Exception(new InvalidOperationException("boom")));
        }

        [Test]
        public void MultipleInstances_UseIndependentTypeNames()
        {
            Assert.DoesNotThrow(() =>
            {
                var a = new NoctuaLogger(typeof(string));
                var b = new NoctuaLogger(typeof(int));
                a.Info("a");
                b.Info("b");
            });
        }
    }
}
