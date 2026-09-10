using System;
using Microsoft.Win32;

namespace WheelFix
{
    internal sealed class AppSettings
    {
        private const string SettingsPath = @"Software\WheelFix";
        private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "WheelFix";

        public bool Enabled { get; set; }
        public int ConfirmationPulses { get; set; }

        public static AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            settings.Enabled = true;
            settings.ConfirmationPulses = 3;

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SettingsPath, false))
                {
                    if (key != null)
                    {
                        settings.Enabled = ReadInt(key, "Enabled", 1) != 0;
                        settings.ConfirmationPulses = Math.Max(2, Math.Min(4,
                            ReadInt(key, "ConfirmationPulses", 3)));
                    }
                }
            }
            catch (Exception ex)
            {
                // Registry settings are a convenience. Safe defaults keep the
                // filter usable even if a policy blocks this key.
                DiagnosticLog.Write(
                    "Could not load settings; using safe defaults (" +
                    ex.GetType().Name + "): " + ex.Message);
            }

            return settings;
        }

        public void Save()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsPath))
            {
                if (key == null)
                {
                    throw new InvalidOperationException(L.Text(
                        "Could not save the settings.",
                        "Impossibile salvare le impostazioni."));
                }

                key.SetValue("Enabled", Enabled ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("ConfirmationPulses", ConfirmationPulses,
                    RegistryValueKind.DWord);
            }
        }

        public static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunPath, false))
                {
                    return key != null && key.GetValue(RunValueName) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void SetStartupEnabled(bool enabled, string executablePath)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunPath, true))
            {
                if (key == null)
                {
                    throw new InvalidOperationException(L.Text(
                        "Could not change the startup setting.",
                        "Impossibile modificare l'avvio automatico."));
                }

                if (enabled)
                {
                    string command = "\"" + executablePath + "\" --startup";
                    key.SetValue(RunValueName, command, RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(RunValueName, false);
                }
            }
        }

        private static int ReadInt(RegistryKey key, string name, int fallback)
        {
            object value = key.GetValue(name, fallback);
            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return fallback;
            }
        }
    }
}
