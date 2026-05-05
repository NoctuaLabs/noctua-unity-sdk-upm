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

    /// <summary>
    /// Extended tests for <see cref="NoctuaLogger"/> covering null messages,
    /// very long messages, distinct type contexts, and all log levels individually.
    /// </summary>
    public class CoreLoggingExtendedTests
    {
        // ── Individual log-level isolation ───────────────────────────────────

        [Test]
        public void NoctuaLogger_Debug_DoesNotThrow()
        {
            ILogger log = new NoctuaLogger(typeof(CoreLoggingExtendedTests));
            Assert.DoesNotThrow(() => log.Debug("debug-only message"));
        }

        [Test]
        public void NoctuaLogger_Info_DoesNotThrow()
        {
            ILogger log = new NoctuaLogger(typeof(CoreLoggingExtendedTests));
            Assert.DoesNotThrow(() => log.Info("info-only message"));
        }

        [Test]
        public void NoctuaLogger_Warning_DoesNotThrow()
        {
            ILogger log = new NoctuaLogger(typeof(CoreLoggingExtendedTests));
            Assert.DoesNotThrow(() => log.Warning("warning-only message"));
        }

        [Test]
        public void NoctuaLogger_Error_DoesNotThrow()
        {
            ILogger log = new NoctuaLogger(typeof(CoreLoggingExtendedTests));
            Assert.DoesNotThrow(() => log.Error("error-only message"));
        }

        [Test]
        public void NoctuaLogger_Exception_WithNullInnerData_DoesNotThrow()
        {
            ILogger log = new NoctuaLogger(typeof(CoreLoggingExtendedTests));
            // An exception whose Message is an empty string — still must not throw.
            Assert.DoesNotThrow(() => log.Exception(new InvalidOperationException("")));
        }

        // ── Null / empty message robustness ──────────────────────────────────

        [Test]
        public void NoctuaLogger_Debug_NullMessage_DoesNotThrow()
        {
            ILogger log = new NoctuaLogger(typeof(CoreLoggingExtendedTests));
            // Null is a valid string argument — logger must not NPE internally.
            Assert.DoesNotThrow(() => log.Debug(null));
        }

        [Test]
        public void NoctuaLogger_Info_NullMessage_DoesNotThrow()
        {
            ILogger log = new NoctuaLogger(typeof(CoreLoggingExtendedTests));
            Assert.DoesNotThrow(() => log.Info(null));
        }

        [Test]
        public void NoctuaLogger_Warning_NullMessage_DoesNotThrow()
        {
            ILogger log = new NoctuaLogger(typeof(CoreLoggingExtendedTests));
            Assert.DoesNotThrow(() => log.Warning(null));
        }

        [Test]
        public void NoctuaLogger_Error_NullMessage_DoesNotThrow()
        {
            ILogger log = new NoctuaLogger(typeof(CoreLoggingExtendedTests));
            Assert.DoesNotThrow(() => log.Error(null));
        }

        // ── Very long message (triggers the 800-char chunking path in Info) ───

        [Test]
        public void NoctuaLogger_Info_VeryLongMessage_DoesNotThrow()
        {
            ILogger log = new NoctuaLogger(typeof(CoreLoggingExtendedTests));
            // Exceeds the 800-char chunk threshold several times.
            var longMsg = new string('L', 5000);
            Assert.DoesNotThrow(() => log.Info(longMsg));
        }

        [Test]
        public void NoctuaLogger_Debug_VeryLongMessage_DoesNotThrow()
        {
            ILogger log = new NoctuaLogger(typeof(CoreLoggingExtendedTests));
            Assert.DoesNotThrow(() => log.Debug(new string('D', 3000)));
        }

        // ── Distinct type contexts produce independent instances ──────────────

        [Test]
        public void NoctuaLogger_DifferentTypes_BothLogWithoutThrow()
        {
            ILogger logA = new NoctuaLogger(typeof(string));
            ILogger logB = new NoctuaLogger(typeof(int));

            Assert.DoesNotThrow(() => logA.Info("from string-context logger"));
            Assert.DoesNotThrow(() => logB.Info("from int-context logger"));
        }

        [Test]
        public void NoctuaLogger_SdkType_DoesNotThrow()
        {
            // Use a real SDK type so the type name prefix path is exercised.
            ILogger log = new NoctuaLogger(typeof(NoctuaException));
            Assert.DoesNotThrow(() =>
            {
                log.Debug("debug from sdk-type context");
                log.Info("info from sdk-type context");
                log.Warning("warning from sdk-type context");
                log.Error("error from sdk-type context");
            });
        }

        [Test]
        public void NoctuaLogger_DefaultCtor_DoesNotThrow()
        {
            // Exercises the stack-trace based type inference path.
            Assert.DoesNotThrow(() =>
            {
                ILogger log = new NoctuaLogger();
                log.Debug("default-ctor debug");
                log.Warning("default-ctor warning");
            });
        }

        // ── Empty string message ──────────────────────────────────────────────

        [Test]
        public void NoctuaLogger_AllLevels_EmptyString_DoNotThrow()
        {
            ILogger log = new NoctuaLogger(typeof(CoreLoggingExtendedTests));

            Assert.DoesNotThrow(() => log.Debug(""));
            Assert.DoesNotThrow(() => log.Info(""));
            Assert.DoesNotThrow(() => log.Warning(""));
            Assert.DoesNotThrow(() => log.Error(""));
        }
    }
}
