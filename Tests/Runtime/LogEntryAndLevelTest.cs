using System;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="LogEntry"/> and <see cref="LogLevel"/>.
    ///
    /// Covers:
    ///   — Constructor field assignment and null-coalescing guards
    ///   — <c>Id</c> uniqueness across instances
    ///   — <c>LogLevel</c> enum ordinal values (ABI-stable contract)
    /// </summary>
    [TestFixture]
    public class LogEntryAndLevelTest
    {
        private static readonly DateTime _ts = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // ─── Constructor — happy path ──────────────────────────────────────

        [Test]
        public void Constructor_AllFields_AssignedCorrectly()
        {
            var entry = new LogEntry(_ts, LogLevel.Info, "Unity", "MyTag", "Hello world", "stack\nhere");

            Assert.AreEqual(_ts,          entry.TimestampUtc);
            Assert.AreEqual(LogLevel.Info, entry.Level);
            Assert.AreEqual("Unity",       entry.Source);
            Assert.AreEqual("MyTag",       entry.Tag);
            Assert.AreEqual("Hello world", entry.Message);
            Assert.AreEqual("stack\nhere", entry.StackTrace);
        }

        [Test]
        public void Constructor_NoStackTrace_StackTraceIsNull()
        {
            var entry = new LogEntry(_ts, LogLevel.Debug, "iOS", "tag", "msg");

            Assert.IsNull(entry.StackTrace,
                "StackTrace defaults to null when omitted");
        }

        // ─── Constructor — null-coalescing guards ──────────────────────────

        [Test]
        public void Constructor_NullSource_SourceIsEmpty()
        {
            var entry = new LogEntry(_ts, LogLevel.Warning, null, "tag", "msg");

            Assert.AreEqual("", entry.Source,
                "null Source must be coalesced to empty string");
        }

        [Test]
        public void Constructor_NullTag_TagIsEmpty()
        {
            var entry = new LogEntry(_ts, LogLevel.Error, "Android", null, "msg");

            Assert.AreEqual("", entry.Tag,
                "null Tag must be coalesced to empty string");
        }

        [Test]
        public void Constructor_NullMessage_MessageIsEmpty()
        {
            var entry = new LogEntry(_ts, LogLevel.Info, "Unity", "tag", null);

            Assert.AreEqual("", entry.Message,
                "null Message must be coalesced to empty string");
        }

        // ─── Id uniqueness ─────────────────────────────────────────────────

        [Test]
        public void Id_IsNotEmpty()
        {
            var entry = new LogEntry(_ts, LogLevel.Info, "Unity", "tag", "msg");

            Assert.AreNotEqual(Guid.Empty, entry.Id);
        }

        [Test]
        public void Id_IsUniqueAcrossInstances()
        {
            var a = new LogEntry(_ts, LogLevel.Info, "Unity", "tag", "msg");
            var b = new LogEntry(_ts, LogLevel.Info, "Unity", "tag", "msg");

            Assert.AreNotEqual(a.Id, b.Id,
                "Each LogEntry must receive a distinct Guid");
        }

        [Test]
        public void Id_IsStableAfterConstruction()
        {
            var entry = new LogEntry(_ts, LogLevel.Debug, "Unity", "tag", "msg");
            var first  = entry.Id;
            var second = entry.Id;

            Assert.AreEqual(first, second,
                "Id must not change between reads");
        }

        // ─── LogLevel enum ordinals — ABI-stable contract ─────────────────

        [Test]
        public void LogLevel_Verbose_OrdinalIsTwo()
        {
            Assert.AreEqual(2, (int)LogLevel.Verbose);
        }

        [Test]
        public void LogLevel_Debug_OrdinalIsThree()
        {
            Assert.AreEqual(3, (int)LogLevel.Debug);
        }

        [Test]
        public void LogLevel_Info_OrdinalIsFour()
        {
            Assert.AreEqual(4, (int)LogLevel.Info);
        }

        [Test]
        public void LogLevel_Warning_OrdinalIsFive()
        {
            Assert.AreEqual(5, (int)LogLevel.Warning);
        }

        [Test]
        public void LogLevel_Error_OrdinalIsSix()
        {
            Assert.AreEqual(6, (int)LogLevel.Error);
        }

        // ─── Level stored correctly in entry ──────────────────────────────

        [Test]
        public void Constructor_LevelError_StoredAsError()
        {
            var entry = new LogEntry(_ts, LogLevel.Error, "Unity", "tag", "msg");

            Assert.AreEqual(LogLevel.Error, entry.Level);
        }

        [Test]
        public void Constructor_LevelWarning_StoredAsWarning()
        {
            var entry = new LogEntry(_ts, LogLevel.Warning, "Unity", "tag", "msg");

            Assert.AreEqual(LogLevel.Warning, entry.Level);
        }
    }
}
