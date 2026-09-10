using System;
using System.Drawing;
using System.Windows.Forms;

namespace WheelFix
{
    internal sealed class MainForm : Form
    {
        private readonly CheckBox _enabledCheckBox;
        private readonly Label _statusLabel;
        private readonly TrackBar _confirmationTrackBar;
        private readonly Label _confirmationValueLabel;
        private readonly CheckBox _startupCheckBox;
        private readonly Label _blockedValueLabel;
        private readonly Button _lightButton;
        private readonly Button _balancedButton;
        private readonly Button _strongButton;
        private readonly Action<bool> _enabledChanged;
        private readonly Action<int> _confirmationChanged;
        private readonly Action<bool> _startupChanged;
        private readonly Action _resetCount;
        private bool _updating;
        private bool _allowClose;

        public MainForm(
            Icon icon,
            Action<bool> enabledChanged,
            Action<int> confirmationChanged,
            Action<bool> startupChanged,
            Action resetCount)
        {
            _enabledChanged = enabledChanged;
            _confirmationChanged = confirmationChanged;
            _startupChanged = startupChanged;
            _resetCount = resetCount;

            Text = "WheelFix";
            Icon = icon;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            ClientSize = new Size(520, 442);
            BackColor = Color.FromArgb(247, 249, 252);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 76;
            header.BackColor = Color.FromArgb(17, 24, 39);
            Controls.Add(header);

            Label title = new Label();
            title.AutoSize = true;
            title.Text = "WheelFix";
            title.ForeColor = Color.White;
            title.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold,
                GraphicsUnit.Point);
            title.Location = new Point(20, 10);
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.AutoSize = true;
            subtitle.Text = L.Text(
                "Mouse wheel debounce filter for Windows",
                "Filtro anti-rimbalzo per la rotellina del mouse");
            subtitle.ForeColor = Color.FromArgb(180, 190, 205);
            subtitle.Location = new Point(23, 49);
            header.Controls.Add(subtitle);

            _enabledCheckBox = new CheckBox();
            _enabledCheckBox.AutoSize = true;
            _enabledCheckBox.Font = new Font("Segoe UI Semibold", 11F,
                FontStyle.Bold, GraphicsUnit.Point);
            _enabledCheckBox.Text = L.Text("Filter enabled", "Filtro attivo");
            _enabledCheckBox.Location = new Point(22, 99);
            _enabledCheckBox.CheckedChanged += EnabledCheckBoxChanged;
            Controls.Add(_enabledCheckBox);

            _statusLabel = new Label();
            _statusLabel.AutoSize = false;
            _statusLabel.Size = new Size(108, 25);
            _statusLabel.Location = new Point(388, 98);
            _statusLabel.TextAlign = ContentAlignment.MiddleCenter;
            _statusLabel.Font = new Font("Segoe UI Semibold", 9F,
                FontStyle.Bold, GraphicsUnit.Point);
            Controls.Add(_statusLabel);

            Label confirmationLabel = new Label();
            confirmationLabel.AutoSize = true;
            confirmationLabel.Font = new Font("Segoe UI Semibold", 9.5F,
                FontStyle.Bold, GraphicsUnit.Point);
            confirmationLabel.Text = L.Text(
                "Reversal confirmation",
                "Conferma inversione");
            confirmationLabel.Location = new Point(22, 145);
            Controls.Add(confirmationLabel);

            _confirmationValueLabel = new Label();
            _confirmationValueLabel.AutoSize = false;
            _confirmationValueLabel.Size = new Size(95, 22);
            _confirmationValueLabel.Location = new Point(401, 143);
            _confirmationValueLabel.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(_confirmationValueLabel);

            _confirmationTrackBar = new TrackBar();
            _confirmationTrackBar.Minimum = 2;
            _confirmationTrackBar.Maximum = 4;
            _confirmationTrackBar.TickFrequency = 1;
            _confirmationTrackBar.SmallChange = 1;
            _confirmationTrackBar.LargeChange = 1;
            _confirmationTrackBar.Size = new Size(482, 45);
            _confirmationTrackBar.Location = new Point(16, 169);
            _confirmationTrackBar.ValueChanged += ConfirmationTrackBarValueChanged;
            Controls.Add(_confirmationTrackBar);

            _lightButton = CreatePresetButton(
                L.Text("Light  2 pulses", "Leggero  2 impulsi"), 22);
            _balancedButton = CreatePresetButton(
                L.Text("Balanced  3 pulses", "Bilanciato  3 impulsi"), 180);
            _strongButton = CreatePresetButton(
                L.Text("Strong  4 pulses", "Forte  4 impulsi"), 338);
            _lightButton.Click += delegate { SetPreset(2); };
            _balancedButton.Click += delegate { SetPreset(3); };
            _strongButton.Click += delegate { SetPreset(4); };
            Controls.Add(_lightButton);
            Controls.Add(_balancedButton);
            Controls.Add(_strongButton);

            Label explanation = new Label();
            explanation.AutoSize = false;
            explanation.Size = new Size(474, 46);
            explanation.Location = new Point(23, 257);
            explanation.ForeColor = Color.FromArgb(75, 85, 99);
            explanation.Text = L.Text(
                "Opposite pulses are held until the selected count confirms " +
                    "a reversal, then replayed together without losing scroll " +
                    "distance. Start with 3 pulses.",
                "Gli impulsi contrari vengono trattenuti fino alla conferma, " +
                    "poi riprodotti insieme senza perdere distanza. Parti da " +
                    "3 impulsi.");
            Controls.Add(explanation);

            Panel separator = new Panel();
            separator.BackColor = Color.FromArgb(220, 225, 233);
            separator.Size = new Size(474, 1);
            separator.Location = new Point(23, 311);
            Controls.Add(separator);

            _startupCheckBox = new CheckBox();
            _startupCheckBox.AutoSize = true;
            _startupCheckBox.Text = L.Text(
                "Start automatically with Windows",
                "Avvia automaticamente con Windows");
            _startupCheckBox.Location = new Point(22, 329);
            _startupCheckBox.CheckedChanged += StartupCheckBoxChanged;
            Controls.Add(_startupCheckBox);

            Label blockedLabel = new Label();
            blockedLabel.AutoSize = true;
            blockedLabel.Text = L.Text(
                "Bad pulses blocked:",
                "Impulsi errati bloccati:");
            blockedLabel.Location = new Point(22, 370);
            Controls.Add(blockedLabel);

            _blockedValueLabel = new Label();
            _blockedValueLabel.AutoSize = true;
            _blockedValueLabel.Font = new Font("Segoe UI Semibold", 9F,
                FontStyle.Bold, GraphicsUnit.Point);
            _blockedValueLabel.Location = new Point(171, 370);
            Controls.Add(_blockedValueLabel);

            Button resetButton = new Button();
            resetButton.Text = L.Text("Reset", "Azzera");
            resetButton.FlatStyle = FlatStyle.Flat;
            resetButton.FlatAppearance.BorderColor = Color.FromArgb(195, 202, 213);
            resetButton.Size = new Size(70, 28);
            resetButton.Location = new Point(229, 363);
            resetButton.Click += delegate
            {
                if (_resetCount != null)
                {
                    _resetCount();
                }
            };
            Controls.Add(resetButton);

            Button hideButton = new Button();
            hideButton.Text = L.Text(
                "Hide to notification area",
                "Nascondi nella tray");
            hideButton.BackColor = Color.FromArgb(37, 99, 235);
            hideButton.ForeColor = Color.White;
            hideButton.FlatStyle = FlatStyle.Flat;
            hideButton.FlatAppearance.BorderSize = 0;
            hideButton.Size = new Size(190, 34);
            hideButton.Location = new Point(306, 397);
            hideButton.Click += delegate { Hide(); };
            Controls.Add(hideButton);
        }

        public void ApplyState(
            bool enabled,
            int confirmationPulses,
            bool startup,
            long blocked)
        {
            _updating = true;
            try
            {
                _enabledCheckBox.Checked = enabled;
                _confirmationTrackBar.Value = Math.Max(
                    _confirmationTrackBar.Minimum,
                    Math.Min(_confirmationTrackBar.Maximum,
                        confirmationPulses));
                _startupCheckBox.Checked = startup;
                _blockedValueLabel.Text = blocked.ToString("N0");
                UpdateVisualState(enabled, confirmationPulses);
            }
            finally
            {
                _updating = false;
            }
        }

        public void UpdateBlockedCount(long blocked)
        {
            _blockedValueLabel.Text = blocked.ToString("N0");
        }

        public void ShowFront()
        {
            if (!Visible)
            {
                Show();
            }

            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }

            Activate();
            BringToFront();
        }

        public void AllowClose()
        {
            _allowClose = true;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            base.OnFormClosing(e);
        }

        private Button CreatePresetButton(string text, int x)
        {
            Button button = new Button();
            button.Text = text;
            button.Size = new Size(142, 32);
            button.Location = new Point(x, 218);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(195, 202, 213);
            button.BackColor = Color.White;
            return button;
        }

        private void SetPreset(int confirmationPulses)
        {
            _confirmationTrackBar.Value = confirmationPulses;
        }

        private void EnabledCheckBoxChanged(object sender, EventArgs e)
        {
            if (_updating)
            {
                return;
            }

            UpdateVisualState(
                _enabledCheckBox.Checked,
                _confirmationTrackBar.Value);
            if (_enabledChanged != null)
            {
                _enabledChanged(_enabledCheckBox.Checked);
            }
        }

        private void ConfirmationTrackBarValueChanged(object sender, EventArgs e)
        {
            int value = _confirmationTrackBar.Value;
            UpdateVisualState(_enabledCheckBox.Checked, value);

            if (!_updating && _confirmationChanged != null)
            {
                _confirmationChanged(value);
            }
        }

        private void StartupCheckBoxChanged(object sender, EventArgs e)
        {
            if (!_updating && _startupChanged != null)
            {
                _startupChanged(_startupCheckBox.Checked);
            }
        }

        private void UpdateVisualState(bool enabled, int confirmationPulses)
        {
            _confirmationValueLabel.Text = confirmationPulses.ToString() +
                L.Text(" pulses", " impulsi");
            _confirmationTrackBar.Enabled = enabled;
            _lightButton.Enabled = enabled;
            _balancedButton.Enabled = enabled;
            _strongButton.Enabled = enabled;

            if (enabled)
            {
                _statusLabel.Text = L.Text("ACTIVE", "ATTIVO");
                _statusLabel.ForeColor = Color.FromArgb(21, 128, 61);
                _statusLabel.BackColor = Color.FromArgb(220, 252, 231);
            }
            else
            {
                _statusLabel.Text = L.Text("PAUSED", "IN PAUSA");
                _statusLabel.ForeColor = Color.FromArgb(146, 64, 14);
                _statusLabel.BackColor = Color.FromArgb(254, 243, 199);
            }
        }
    }
}
