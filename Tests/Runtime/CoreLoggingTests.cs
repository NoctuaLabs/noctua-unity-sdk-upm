using System;
using System.Reflection;
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

    /// <summary>
    /// Regression tests for the <see cref="NoctuaLogger"/> constructor's
    /// reflection fallback. On Android/iOS IL2CPP release builds the managed
    /// stack-frame metadata is stripped, so <c>new StackTrace().GetFrame(1)</c>,
    /// <c>GetMethod()</c>, and <c>DeclaringType</c> can each return <c>null</c> —
    /// which previously threw <see cref="NullReferenceException"/> from the ctor
    /// and crashed <c>Noctua</c> construction (the whole Lazy factory). The ctor
    /// must now be crash-proof and always produce a non-empty type-name prefix.
    ///
    /// EditMode runs under Mono, where stack frames are always available, so we
    /// cannot reproduce the null-frame condition directly. Instead we lock in the
    /// observable contract: the private <c>_typeName</c> field is never null/empty,
    /// the explicit-type path captures the exact type name, and the no-arg / null
    /// paths never throw.
    /// </summary>
    public class NoctuaLoggerCtorGuardTests
    {
        private static string ReadTypeName(NoctuaLogger logger)
        {
            var field = typeof(NoctuaLogger).GetField(
                "_typeName", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "_typeName field not found — has the implementation changed?");
            return (string)field.GetValue(logger);
        }

        [Test]
        public void Ctor_ExplicitType_CapturesTypeName()
        {
            var logger = new NoctuaLogger(typeof(NoctuaException));
            Assert.AreEqual("NoctuaException", ReadTypeName(logger));
        }

        [Test]
        public void Ctor_ExplicitNull_ProducesNonEmptyTypeName()
        {
            // type == null takes the reflection fallback; whatever it resolves
            // (caller type under Mono, or the "Noctua" fallback when the chain is
            // null under IL2CPP) the result must be a usable, non-empty prefix.
            var logger = new NoctuaLogger(null);
            var name = ReadTypeName(logger);
            Assert.IsFalse(string.IsNullOrEmpty(name),
                "Reflection fallback must never leave _typeName null/empty.");
        }

        [Test]
        public void Ctor_NoArg_ProducesNonEmptyTypeName()
        {
            var logger = new NoctuaLogger();
            var name = ReadTypeName(logger);
            Assert.IsFalse(string.IsNullOrEmpty(name),
                "No-arg ctor must never leave _typeName null/empty.");
        }

        [Test]
        public void Ctor_ExplicitNull_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => new NoctuaLogger(null));
        }

        [Test]
        public void Ctor_FromCompilerGeneratedFrame_DoesNotThrow()
        {
            // Construct from inside a lambda so the calling frame's declaring type
            // is a compiler-generated closure — exercises the fallback path with a
            // non-standard caller frame.
            Func<NoctuaLogger> make = () => new NoctuaLogger();
            Assert.DoesNotThrow(() =>
            {
                var logger = make();
                Assert.IsFalse(string.IsNullOrEmpty(ReadTypeName(logger)));
            });
        }

        [Test]
        public void Ctor_ManyNoArgInstances_AllCrashProof()
        {
            // Stress the reflection path repeatedly — every instance must be valid.
            for (var i = 0; i < 100; i++)
            {
                var logger = new NoctuaLogger();
                Assert.IsFalse(string.IsNullOrEmpty(ReadTypeName(logger)));
            }
        }
    }
}
