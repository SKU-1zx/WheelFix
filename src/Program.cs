using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("WheelFix")]
[assembly: AssemblyDescription("Mouse wheel debounce filter for Windows")]
[assembly: AssemblyCompany("SKU-1zx")]
[assembly: AssemblyProduct("WheelFix")]
[assembly: AssemblyCopyright("Copyright © 2026 SKU-1zx")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: AssemblyInformationalVersion("0.1.0")]

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
                try
                {
                    Application.Run(new WheelFixContext(startedWithWindows));
                }
                catch (Exception ex)
                {
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
            int index;
            for (index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], expected,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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
        private bool _disposed;

        public WheelFixContext(bool startedWithWindows)
        {
            _settings = AppSettings.Load();
            _filter = new WheelFilterCore(_settings.Enabled, _settings.WindowMs);
            _mouseHook = new NativeMouseHook(_filter);
            _icon = TrayIconFactory.Create();

            _form = new MainForm(
                _icon,
                SetFilterEnabled,
                SetWindow,
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

            ToolStripMenuItem sensitivityMenu =
                new ToolStripMenuItem(L.Text("Strength", "Sensibilità"));
            _lightMenuItem = CreatePresetMenuItem(
                L.Text("Light (25 ms)", "Leggero (25 ms)"), 25);
            _balancedMenuItem = CreatePresetMenuItem(
                L.Text("Balanced (55 ms)", "Bilanciato (55 ms)"), 55);
            _strongMenuItem = CreatePresetMenuItem(
                L.Text("Strong (90 ms)", "Forte (90 ms)"), 90);
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
                _form.UpdateBlockedCount(_filter.BlockedCount);
            };
            _statsTimer.Start();

            _mouseHook.Start();
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
                _statsTimer.Stop();
                _statsTimer.Dispose();
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _menu.Dispose();
                _mouseHook.Dispose();
                _form.Dispose();
                _icon.Dispose();
            }

            base.ExitThreadCore();
        }

        private ToolStripMenuItem CreatePresetMenuItem(string text, int windowMs)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Tag = windowMs;
            item.Click += delegate { SetWindow(windowMs); };
            return item;
        }

        private void SetFilterEnabled(bool enabled)
        {
            _settings.Enabled = enabled;
            _filter.Enabled = enabled;
            SaveSettings();
            SyncUi();
        }

        private void SetWindow(int windowMs)
        {
            _settings.WindowMs = Math.Max(10, Math.Min(150, windowMs));
            _filter.WindowMs = _settings.WindowMs;
            SaveSettings();
            SyncUi();
        }

        private void SetStartup(bool enabled)
        {
            try
            {
                AppSettings.SetStartupEnabled(enabled, Application.ExecutablePath);
            }
            catch (Exception ex)
            {
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
            _filter.ResetBlockedCount();
            SyncUi();
        }

        private void SaveSettings()
        {
            try
            {
                _settings.Save();
            }
            catch (Exception ex)
            {
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
                _settings.WindowMs,
                startupEnabled,
                blocked);

            _enabledMenuItem.Checked = _settings.Enabled;
            _startupMenuItem.Checked = startupEnabled;
            _lightMenuItem.Checked = _settings.WindowMs == 25;
            _balancedMenuItem.Checked = _settings.WindowMs == 55;
            _strongMenuItem.Checked = _settings.WindowMs == 90;
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
