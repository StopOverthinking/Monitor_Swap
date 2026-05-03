using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using MonitorSwap.Models;

namespace MonitorSwap.Services
{
    internal sealed class RotationTraceService
    {
        private const string BootMarkerFileName = "boot-session.txt";

        public RotationTraceSession BeginSession(AppSettings settings, RotationDirection direction)
        {
            if (settings == null || !settings.EnableRotationDiagnostics)
            {
                return RotationTraceSession.Disabled;
            }

            try
            {
                var rootDirectory = GetLogDirectoryPath();
                Directory.CreateDirectory(rootDirectory);

                var timestamp = DateTime.Now;
                var filePath = Path.Combine(
                    rootDirectory,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "rotation-{0:yyyyMMdd}.log",
                        timestamp));

                var writer = new StreamWriter(new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8)
                {
                    AutoFlush = true
                };

                return new RotationTraceSession(writer, direction, timestamp);
            }
            catch
            {
                return RotationTraceSession.Disabled;
            }
        }

        public void ResetLogsForNewBootIfNeeded()
        {
            try
            {
                var rootDirectory = GetLogDirectoryPath();
                Directory.CreateDirectory(rootDirectory);

                var markerFilePath = Path.Combine(rootDirectory, BootMarkerFileName);
                var currentBootMarker = GetCurrentBootMarker();
                var previousBootMarker = File.Exists(markerFilePath)
                    ? (File.ReadAllText(markerFilePath, Encoding.UTF8) ?? string.Empty).Trim()
                    : string.Empty;

                if (!string.Equals(previousBootMarker, currentBootMarker, StringComparison.Ordinal))
                {
                    foreach (var logFilePath in Directory.GetFiles(rootDirectory, "rotation-*.log", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            File.Delete(logFilePath);
                        }
                        catch
                        {
                        }
                    }
                }

                File.WriteAllText(markerFilePath, currentBootMarker, Encoding.UTF8);
            }
            catch
            {
            }
        }

        private static string GetLogDirectoryPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MonitorSwap",
                "logs");
        }

        private static string GetCurrentBootMarker()
        {
            var uptimeMilliseconds = Native.NativeMethods.GetTickCount64();
            var bootTimeUtc = DateTime.UtcNow - TimeSpan.FromMilliseconds(Math.Min(long.MaxValue, (long)uptimeMilliseconds));
            var roundedTicks = bootTimeUtc.Ticks - (bootTimeUtc.Ticks % TimeSpan.TicksPerMinute);
            return new DateTime(roundedTicks, DateTimeKind.Utc).ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
        }

        internal sealed class RotationTraceSession : IDisposable
        {
            private static readonly RotationTraceSession _disabled = new RotationTraceSession();
            private readonly StreamWriter _writer;
            private readonly string _sessionId;
            private readonly bool _enabled;

            private RotationTraceSession()
            {
            }

            public RotationTraceSession(StreamWriter writer, RotationDirection direction, DateTime startedAt)
            {
                _writer = writer;
                _enabled = writer != null;
                _sessionId = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:HHmmss.fff}-{1}",
                    startedAt,
                    Process.GetCurrentProcess().Id);

                Write("session-start direction={0}", direction);
            }

            public static RotationTraceSession Disabled
            {
                get { return _disabled; }
            }

            public bool IsEnabled
            {
                get { return _enabled; }
            }

            public void Write(string message, params object[] args)
            {
                if (!_enabled || _writer == null)
                {
                    return;
                }

                try
                {
                    var text = args != null && args.Length > 0
                        ? string.Format(CultureInfo.InvariantCulture, message, args)
                        : message;
                    _writer.WriteLine(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0:O} [{1}] {2}",
                            DateTime.Now,
                            _sessionId,
                            text));
                }
                catch
                {
                }
            }

            public void Dispose()
            {
                if (!_enabled || _writer == null)
                {
                    return;
                }

                try
                {
                    Write("session-end");
                    _writer.Dispose();
                }
                catch
                {
                }
            }
        }
    }
}
