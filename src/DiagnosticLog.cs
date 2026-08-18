using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace WheelFix
{
    internal static class DiagnosticLog
    {
        private const long MaximumBytes = 1024L * 1024L;
        private const int MaximumMessageCharacters = 4096;
        private static readonly Encoding Utf8 = new UTF8Encoding(false);
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WheelFix");

        public static string FilePath
        {
            get { return Path.Combine(LogDirectory, "WheelFix.log"); }
        }

        public static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);

                string line = FormatLine(message);
                if (File.Exists(FilePath) &&
                    new FileInfo(FilePath).Length + Utf8.GetByteCount(line) >
                        MaximumBytes)
                {
                    File.WriteAllText(
                        FilePath,
                        FormatLine("Log restarted after reaching 1 MB.") + line,
                        Utf8);
                    return;
                }

                File.AppendAllText(FilePath, line, Utf8);
            }
            catch
            {
                // Diagnostics must never interfere with mouse input or startup.
            }
        }

        public static void Open()
        {
            Write("Diagnostic log opened by user.");
            Process.Start(new ProcessStartInfo(FilePath)
            {
                UseShellExecute = true
            });
        }

        private static string FormatLine(string message)
        {
            string safeMessage = (message ?? string.Empty)
                .Replace('\r', ' ')
                .Replace('\n', ' ');
            if (safeMessage.Length > MaximumMessageCharacters)
            {
                safeMessage = safeMessage.Substring(
                    0, MaximumMessageCharacters) + " [truncated]";
            }

            return DateTimeOffset.Now.ToString(
                "yyyy-MM-dd HH:mm:ss.fff zzz",
                CultureInfo.InvariantCulture) + "  " + safeMessage +
                Environment.NewLine;
        }
    }
}
