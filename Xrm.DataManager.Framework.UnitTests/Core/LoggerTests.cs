using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Xrm.DataManager.Framework.UnitTests.Core
{
    public class LoggerTests
    {
        private class TempFileLogger : FileLogger
        {
            public TempFileLogger(string filePath) : base(new JobSettings())
            {
                FilePath = filePath;
            }

            public override void LogDisplay(string message) { }
        }

        [Fact]
        public void Extensions_logger_forwards_message_event_exception_and_properties()
        {
            var inner = new CapturingExtensionsLogger();
            ILogger logger = new ExtensionsLogger(new JobSettings(), inner);

            logger.LogInformation("info {not a placeholder}", new Dictionary<string, string> { { "k1", "v1" } }, display: false);
            logger.LogSuccess("success", new Dictionary<string, string> { { "k2", "v2" } });
            logger.LogFailure(new InvalidOperationException("boom"), new Dictionary<string, string> { { "k3", "v3" } });

            Assert.Equal("Information||info {not a placeholder}||k1=v1", inner.Entries[0]);
            Assert.Equal("Information|Success|success||k2=v2", inner.Entries[1]);
            Assert.Equal("Error|Failure|boom|boom|k3=v3", inner.Entries[2]);
        }

        [Fact]
        public void File_logger_supports_concurrent_writes()
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.log");
            try
            {
                var logger = new TempFileLogger(filePath);

                Parallel.For(0, 1000, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i => logger.LogSuccess($"record {i}", new Dictionary<string, string>()));
                logger.LogFailure(new Exception("file failure"));

                var lines = File.ReadAllLines(filePath);
                Assert.Equal(1000, lines.Count(l => l.Contains("[SUC] record ")));
                Assert.Contains(lines, l => l.Contains("[ERR] Failure"));
                Assert.Contains(lines, l => l.Contains("file failure"));
            }
            finally
            {
                File.Delete(filePath);
            }
        }
    }
}
