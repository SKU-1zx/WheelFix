using System;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("WheelFix")]
[assembly: AssemblyDescription("Mouse wheel debounce filter for Windows")]
[assembly: AssemblyCompany("SKU-1zx")]
[assembly: AssemblyProduct("WheelFix")]
[assembly: AssemblyCopyright("Copyright © 2026 SKU-1zx")]
[assembly: AssemblyVersion("0.3.0.0")]
[assembly: AssemblyFileVersion("0.3.0.0")]
[assembly: AssemblyInformationalVersion("0.3.0-preview.3")]

namespace WheelFix
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool createdNew;
            using (Mutex singleInstance = new Mutex(
                true, @"Local\WheelFix.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        L.Text(
                            "WheelFix is already running in the notification area.",
                            "WheelFix è già in esecuzione nella tray di Windows."),
                        "WheelFix",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                bool startedWithWindows = HasArgument(args, "--startup");
                DiagnosticLog.Write(
                    "WheelFix " + Application.ProductVersion +
                    " starting; launch=" +
                    (startedWithWindows ? "Windows startup" : "interactive") + ".");
                try
                {
                    Application.Run(new WheelFixContext(startedWithWindows));
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Write(
                        "Fatal error (" + ex.GetType().Name + "): " + ex.Message);
                    MessageBox.Show(
                        L.Text(
                            "WheelFix could not start.\r\n\r\n",
                            "WheelFix non può avviarsi.\r\n\r\n") + ex.Message,
                        L.Text("WheelFix error", "Errore WheelFix"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private static bool HasArgument(string[] args, string expected)
        {
            return Array.Exists(args, value => string.Equals(
                value, expected, StringComparison.OrdinalIgnoreCase));
        }
    }

    internal sealed class WheelFixContext : ApplicationContext
    {
        private readonly AppSettings _settings;
        private readonly WheelFilterCore _filter;
        private readonly NativeMouseHook _mouseHook;
        private readonly MainForm _form;
        private readonly Icon _icon;
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _enabledMenuItem;
        private readonly ToolStripMenuItem _startupMenuItem;
        private readonly ToolStripMenuItem _lightMenuItem;
        private readonly ToolStripMenuItem _balancedMenuItem;
        private readonly ToolStripMenuItem _strongMenuItem;
        private readonly System.Windows.Forms.Timer _statsTimer;
        private long _lastLoggedBlockedCount;
        private bool _disposed;

        public WheelFixContext(bool startedWithWindows)
        {
            _settings = AppSettings.Load();
            _filter = new WheelFilterCore(
                _settings.Enabled,
                _settings.ConfirmationPulses);
            _mouseHook = new NativeMouseHook(_filter);
            _icon = TrayIconFactory.Create();
            DiagnosticLog.Write(
                "Settings loaded: filter=" +
                (_settings.Enabled ? "enabled" : "paused") +
                ", reversal confirmation=" +
                _settings.ConfirmationPulses + " pulses.");

            _form = new MainForm(
                _icon,
                SetFilterEnabled,
                SetConfirmationPulses,
                SetStartup,
                ResetCounter);
            MainForm = _form;

            _menu = new ContextMenuStrip();
            ToolStripMenuItem openItem = new ToolStripMenuItem(
                L.Text("Open WheelFix", "Apri WheelFix"));
            openItem.Font = new Font(openItem.Font, FontStyle.Bold);
            openItem.Click += delegate { _form.ShowFront(); };
            _menu.Items.Add(openItem);
            _menu.Items.Add(new ToolStripSeparator());

            _enabledMenuItem = new ToolStripMenuItem(
                L.Text("Filter enabled", "Filtro attivo"));
            _enabledMenuItem.Click += delegate
            {
                SetFilterEnabled(!_settings.Enabled);
            };
            _menu.Items.Add(_enabledMenuItem);

            ToolStripMenuItem sensitivityMenu = new ToolStripMenuItem(
                L.Text("Reversal confirmation", "Conferma inversione"));
            _lightMenuItem = CreatePresetMenuItem(
                L.Text("Light (2 pulses)", "Leggero (2 impulsi)"), 2);
            _balancedMenuItem = CreatePresetMenuItem(
                L.Text("Balanced (3 pulses)", "Bilanciato (3 impulsi)"), 3);
            _strongMenuItem = CreatePresetMenuItem(
                L.Text("Strong (4 pulses)", "Forte (4 impulsi)"), 4);
            sensitivityMenu.DropDownItems.Add(_lightMenuItem);
            sensitivityMenu.DropDownItems.Add(_balancedMenuItem);
            sensitivityMenu.DropDownItems.Add(_strongMenuItem);
            _menu.Items.Add(sensitivityMenu);

            _startupMenuItem = new ToolStripMenuItem(
                L.Text("Start with Windows", "Avvia con Windows"));
            _startupMenuItem.Click += delegate
            {
                SetStartup(!AppSettings.IsStartupEnabled());
            };
            _menu.Items.Add(_startupMenuItem);

            ToolStripMenuItem openLogItem = new ToolStripMenuItem(
                L.Text("Open diagnostic log", "Apri log diagnostico"));
            openLogItem.Click += delegate { OpenDiagnosticLog(); };
            _menu.Items.Add(openLogItem);
            _menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exitItem = new ToolStripMenuItem(
                L.Text("Exit", "Esci"));
            exitItem.Click += delegate { ExitApplication(); };
            _menu.Items.Add(exitItem);

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = _icon;
            _notifyIcon.ContextMenuStrip = _menu;
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += delegate { _form.ShowFront(); };

            _statsTimer = new System.Windows.Forms.Timer();
            _statsTimer.Interval = 250;
            _statsTimer.Tick += delegate
            {
                long blocked = _filter.BlockedCount;
                _form.UpdateBlockedCount(blocked);

                if (blocked > _lastLoggedBlockedCount)
                {
                    DiagnosticLog.Write(
                        "Blocked bad pulses: delta=" +
                        (blocked - _lastLoggedBlockedCount) +
                        "; total=" + blocked + ".");
                }

                _lastLoggedBlockedCount = blocked;
                FlushWheelTrace();
            };
            _statsTimer.Start();

            _mouseHook.Start();
            DiagnosticLog.Write("Global mouse hook installed.");
            SyncUi();

            if (!startedWithWindows)
            {
                _form.ShowFront();
            }
        }

        protected override void ExitThreadCore()
        {
            if (!_disposed)
            {
                _disposed = true;
                DiagnosticLog.Write(
                    "WheelFix stopping; blocked_total=" +
                    _filter.BlockedCount + ".");
                _statsTimer.Stop();
                FlushWheelTrace();
                _statsTimer.Dispose();
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _menu.Dispose();
                _mouseHook.Dispose();
                DiagnosticLog.Write("Global mouse hook removed.");
                _form.Dispose();
                _icon.Dispose();
                DiagnosticLog.Write("WheelFix stopped.");
            }

            base.ExitThreadCore();
        }

        private void FlushWheelTrace()
        {
            WheelTraceEvent traceEvent;
            StringBuilder line = null;

            while (_mouseHook.TryDequeueTrace(out traceEvent))
            {
                if (line == null)
                {
                    line = new StringBuilder("Wheel events:");
                }
                else if (line.Length > 3500)
                {
                    DiagnosticLog.Write(line.ToString());
                    line.Length = 0;
                    line.Append("Wheel events:");
                }

                line.Append(" t=")
                    .Append(traceEvent.Timestamp)
                    .Append(" d=")
                    .Append(traceEvent.Delta);

                if (traceEvent.Decision == WheelFilterDecision.Block)
                {
                    line.Append(" HOLD");
                }
                else if (traceEvent.Decision == WheelFilterDecision.Replay)
                {
                    line.Append(" REPLAY d=")
                        .Append(traceEvent.ReplayDelta);
                }
                else if (traceEvent.Decision ==
                    WheelFilterDecision.ReplayFailed)
                {
                    line.Append(" REPLAY_FAILED d=")
                        .Append(traceEvent.ReplayDelta);
                }
                else
                {
                    line.Append(" ALLOW");
                }
            }

            if (line != null)
            {
                DiagnosticLog.Write(line.ToString());
            }
        }

        private ToolStripMenuItem CreatePresetMenuItem(
            string text,
            int confirmationPulses)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += delegate
            {
                SetConfirmationPulses(confirmationPulses);
            };
            return item;
        }

        private void SetFilterEnabled(bool enabled)
        {
            _settings.Enabled = enabled;
            _filter.Enabled = enabled;
            SaveSettings();
            DiagnosticLog.Write(
                enabled ? "Filter enabled." : "Filter paused.");
            SyncUi();
        }

        private void SetConfirmationPulses(int confirmationPulses)
        {
            _filter.ConfirmationPulses = confirmationPulses;
            _settings.ConfirmationPulses = _filter.ConfirmationPulses;
            SaveSettings();
            DiagnosticLog.Write(
                "Reversal confirmation changed to " +
                _settings.ConfirmationPulses + " pulses.");
            SyncUi();
        }

        private void SetStartup(bool enabled)
        {
            try
            {
                AppSettings.SetStartupEnabled(enabled, Application.ExecutablePath);
                DiagnosticLog.Write(
                    enabled
                        ? "Windows startup enabled."
                        : "Windows startup disabled.");
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write(
                    "Could not change Windows startup (" +
                    ex.GetType().Name + "): " + ex.Message);
                MessageBox.Show(
                    L.Text(
                        "Could not change the startup setting.\r\n\r\n",
                        "Non riesco a modificare l'avvio automatico.\r\n\r\n") +
                        ex.Message,
                    "WheelFix",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            SyncUi();
        }

        private void ResetCounter()
        {
            long previousCount = _filter.BlockedCount;
            _filter.ResetBlockedCount();
            _lastLoggedBlockedCount = 0L;
            DiagnosticLog.Write(
                "Blocked-pulse counter reset; previous total=" +
                previousCount + ".");
            SyncUi();
        }

        private void OpenDiagnosticLog()
        {
            try
            {
                DiagnosticLog.Open();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write(
                    "Could not open diagnostic log (" +
                    ex.GetType().Name + "): " + ex.Message);
                MessageBox.Show(
                    L.Text(
                        "Could not open the diagnostic log.\r\n\r\n",
                        "Non riesco ad aprire il log diagnostico.\r\n\r\n") +
                        ex.Message,
                    "WheelFix",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void SaveSettings()
        {
            try
            {
                _settings.Save();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write(
                    "Could not save settings (" + ex.GetType().Name +
                    "): " + ex.Message);
                MessageBox.Show(
                    L.Text(
                        "The filter remains active, but its settings could " +
                            "not be saved.\r\n\r\n",
                        "Il filtro resta attivo, ma non riesco a salvare le " +
                            "impostazioni.\r\n\r\n") + ex.Message,
                    "WheelFix",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void SyncUi()
        {
            bool startupEnabled = AppSettings.IsStartupEnabled();
            long blocked = _filter.BlockedCount;

            _form.ApplyState(
                _settings.Enabled,
                _settings.ConfirmationPulses,
                startupEnabled,
                blocked);

            _enabledMenuItem.Checked = _settings.Enabled;
            _startupMenuItem.Checked = startupEnabled;
            _lightMenuItem.Checked = _settings.ConfirmationPulses == 2;
            _balancedMenuItem.Checked = _settings.ConfirmationPulses == 3;
            _strongMenuItem.Checked = _settings.ConfirmationPulses == 4;
            _notifyIcon.Text = _settings.Enabled
                ? L.Text("WheelFix - filter enabled", "WheelFix - filtro attivo")
                : L.Text("WheelFix - filter paused", "WheelFix - filtro in pausa");
        }

        private void ExitApplication()
        {
            _form.AllowClose();
            _form.Close();
            ExitThread();
        }
    }

    internal static class TrayIconFactory
    {
        public static Icon Create()
        {
            try
            {
                using (Icon executableIcon =
                    Icon.ExtractAssociatedIcon(Application.ExecutablePath))
                {
                    if (executableIcon != null)
                    {
                        return (Icon)executableIcon.Clone();
                    }
                }
            }
            catch
            {
                // Fall back to a stock icon only if Windows cannot read the
                // icon embedded in the executable.
            }

            return (Icon)SystemIcons.Application.Clone();
        }
    }
}
