using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace KeyboardCoolDownLock
{
    internal static class NativeKeyboardHook
    {
        private const int WhKeyboardLl = 13;
        private static readonly HookProc Callback = OnKeyboardEvent;
        private static IntPtr _hook;
        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        public static void Install()
        {
            if (_hook != IntPtr.Zero) return;
            using (Process process = Process.GetCurrentProcess())
            using (ProcessModule module = process.MainModule)
                _hook = SetWindowsHookEx(WhKeyboardLl, Callback, GetModuleHandle(module.ModuleName), 0);
            if (_hook == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        public static void Uninstall()
        {
            IntPtr hook = _hook;
            _hook = IntPtr.Zero;
            if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook);
        }

        private static IntPtr OnKeyboardEvent(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0) return new IntPtr(1);
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr module, uint threadId);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string moduleName);
    }

    internal sealed class LockForm : Form
    {
        private readonly Label _countdown = new Label();
        private readonly ProgressBar _progress = new ProgressBar();
        private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer();
        private DateTime _deadline;
        private double _totalSeconds;
        private bool _released;

        public LockForm(TimeSpan duration)
        {
            _deadline = DateTime.Now.Add(duration);
            _totalSeconds = Math.Max(1, duration.TotalSeconds);
            Text = "\u952e\u76d8\u964d\u6e29\u9501 - \u9f20\u6807\u53ef\u7528";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(520, 310);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = ControlBox = false;
            TopMost = ShowInTaskbar = true;
            BackColor = Color.FromArgb(24, 33, 47);
            ForeColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 10F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Icon = SystemIcons.Shield;

            AddLabel("\u952e\u76d8\u5df2\u9501\u5b9a", 20, 20, 480, 55, 24F, FontStyle.Bold, Color.FromArgb(110, 231, 183));
            AddLabel("\u9f20\u6807\u4ecd\u53ef\u6b63\u5e38\u4f7f\u7528\u3002\u9700\u8981\u6062\u590d\u952e\u76d8\u65f6\uff0c\n\u8bf7\u7528\u9f20\u6807\u70b9\u51fb\u4e0b\u65b9\u7eff\u8272\u6309\u94ae\u3002", 20, 77, 480, 58, 11F, FontStyle.Regular, Color.FromArgb(209, 213, 219));
            _countdown.Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
            _countdown.ForeColor = Color.FromArgb(147, 197, 253);
            _countdown.TextAlign = ContentAlignment.MiddleCenter;
            _countdown.SetBounds(20, 137, 480, 32);
            Controls.Add(_countdown);
            _progress.SetBounds(55, 172, 410, 12);
            _progress.Maximum = 1000;
            Controls.Add(_progress);

            Button extend = MakeButton("\u5ef6\u957f 5 \u5206\u949f", "ExtendButton", 55, 202, 145, 54, Color.FromArgb(30, 64, 175), 11F);
            extend.Click += delegate { _deadline = _deadline.AddMinutes(5); _totalSeconds += 300; UpdateDisplay(); };
            Controls.Add(extend);
            Button unlock = MakeButton("\u7acb\u5373\u89e3\u9501\u952e\u76d8", "UnlockButton", 215, 202, 250, 54, Color.FromArgb(22, 163, 74), 15F);
            unlock.AccessibleName = "Unlock keyboard";
            unlock.Click += delegate { ReleaseAndClose(); };
            Controls.Add(unlock);
            AddLabel("\u5b89\u5168\u515c\u5e95\uff1aCtrl+Alt+Delete \u4e0d\u4f1a\u88ab\u672c\u5de5\u5177\u62e6\u622a", 20, 270, 480, 24, 9F, FontStyle.Regular, Color.FromArgb(156, 163, 175));

            _timer.Interval = 250;
            _timer.Tick += delegate { if (DateTime.Now >= _deadline) ReleaseAndClose(); else UpdateDisplay(); };
            Shown += delegate { NativeKeyboardHook.Install(); UpdateDisplay(); _timer.Start(); Activate(); BringToFront(); };
            FormClosing += delegate { Release(); };
        }

        private void AddLabel(string text, int x, int y, int w, int h, float size, FontStyle style, Color color)
        {
            Label label = new Label { Text = text, Font = new Font("Microsoft YaHei UI", size, style), ForeColor = color, TextAlign = ContentAlignment.MiddleCenter };
            label.SetBounds(x, y, w, h); Controls.Add(label);
        }

        private static Button MakeButton(string text, string name, int x, int y, int w, int h, Color color, float size)
        {
            Button button = new Button { Text = text, Name = name, Font = new Font("Microsoft YaHei UI", size, FontStyle.Bold), FlatStyle = FlatStyle.Flat, BackColor = color, ForeColor = Color.White };
            button.FlatAppearance.BorderSize = 0; button.SetBounds(x, y, w, h); return button;
        }

        private void UpdateDisplay()
        {
            TimeSpan remaining = _deadline - DateTime.Now;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            int hours = (int)remaining.TotalHours;
            _countdown.Text = hours > 0 ? string.Format("{0}:{1:00}:{2:00} \u540e\u81ea\u52a8\u89e3\u9501", hours, remaining.Minutes, remaining.Seconds) : string.Format("{0:00}:{1:00} \u540e\u81ea\u52a8\u89e3\u9501", remaining.Minutes, remaining.Seconds);
            _progress.Value = (int)Math.Round(Math.Min(1, Math.Max(0, remaining.TotalSeconds / _totalSeconds)) * 1000);
        }

        private void ReleaseAndClose() { Release(); Close(); }
        private void Release() { if (_released) return; _released = true; _timer.Stop(); NativeKeyboardHook.Uninstall(); }
        protected override void Dispose(bool disposing) { if (disposing) _timer.Dispose(); base.Dispose(disposing); }
    }

    public static class KeyboardLockSession
    {
        private static readonly object Sync = new object();
        private static Mutex _mutex;
        private static Thread _thread;
        private static LockForm _form;
        public static event EventHandler SessionEnded;
        public static bool IsRunning { get { lock (Sync) return _thread != null; } }

        public static bool SelfTest()
        {
            NativeKeyboardHook.Install(); NativeKeyboardHook.Uninstall(); return true;
        }

        public static bool TryStart(TimeSpan duration)
        {
            lock (Sync)
            {
                if (_thread != null) return false;
                bool owns;
                _mutex = new Mutex(true, "Local\\KeyboardCoolDownLock.SingleInstance", out owns);
                if (!owns) { _mutex.Dispose(); _mutex = null; return false; }
                _thread = new Thread(new ThreadStart(delegate
                {
                    try
                    {
                        Application.EnableVisualStyles();
                        using (LockForm form = new LockForm(duration))
                        {
                            lock (Sync) _form = form;
                            Application.Run(form);
                        }
                    }
                    finally
                    {
                        NativeKeyboardHook.Uninstall();
                        lock (Sync) { _form = null; _thread = null; if (_mutex != null) { _mutex.Dispose(); _mutex = null; } }
                        EventHandler ended = SessionEnded; if (ended != null) ended(null, EventArgs.Empty);
                    }
                }));
                _thread.IsBackground = true;
                _thread.SetApartmentState(ApartmentState.STA);
                _thread.Start();
                return true;
            }
        }

        public static void Stop()
        {
            LockForm form; lock (Sync) form = _form;
            if (form != null && !form.IsDisposed) form.BeginInvoke(new Action(form.Close));
        }
    }

    internal static class KeyboardTheme
    {
        public static readonly Color Canvas = Color.FromArgb(245, 247, 251);
        public static readonly Color Card = Color.White;
        public static readonly Color Text = Color.FromArgb(20, 30, 47);
        public static readonly Color Muted = Color.FromArgb(103, 117, 139);
        public static readonly Color Border = Color.FromArgb(224, 229, 238);
        public static readonly Color Primary = Color.FromArgb(39, 174, 96);
        public static readonly Color PrimaryDark = Color.FromArgb(31, 148, 81);
        public static readonly Color Blue = Color.FromArgb(61, 123, 253);

        public static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class KeyboardCard : Panel
    {
        private Color _fillColor = KeyboardTheme.Card;
        private Color _borderColor = KeyboardTheme.Border;
        public Color FillColor { get { return _fillColor; } set { _fillColor = value; Invalidate(); } }
        public Color BorderColor { get { return _borderColor; } set { _borderColor = value; Invalidate(); } }

        public KeyboardCard()
        {
            BackColor = KeyboardTheme.Canvas;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(1, 1, Width - 3, Height - 3);
            using (GraphicsPath path = KeyboardTheme.RoundedRectangle(bounds, 16))
            using (Brush fill = new SolidBrush(_fillColor))
            using (Pen border = new Pen(_borderColor))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }
        }
    }

    internal class KeyboardRoundedButton : Button
    {
        private Color _normalColor = KeyboardTheme.Primary;
        private Color _hoverColor = KeyboardTheme.PrimaryDark;
        private bool _hovered;
        public Color NormalColor { get { return _normalColor; } set { _normalColor = value; Invalidate(); } }
        public Color HoverColor { get { return _hoverColor; } set { _hoverColor = value; Invalidate(); } }

        public KeyboardRoundedButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            ForeColor = Color.White;
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }
        protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Color color = Enabled ? (_hovered ? _hoverColor : _normalColor) : Color.FromArgb(187, 197, 211);
            using (GraphicsPath path = KeyboardTheme.RoundedRectangle(bounds, 11))
            using (Brush brush = new SolidBrush(color)) e.Graphics.FillPath(brush, path);
            TextRenderer.DrawText(e.Graphics, Text, Font, bounds, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    internal sealed class DurationButton : KeyboardRoundedButton
    {
        private bool _selected;
        public bool Selected { get { return _selected; } set { _selected = value; Invalidate(); } }
        public DurationButton()
        {
            ForeColor = KeyboardTheme.Text;
            NormalColor = Color.White;
            HoverColor = Color.FromArgb(242, 246, 255);
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(1, 1, Width - 3, Height - 3);
            Color fillColor = Selected ? Color.FromArgb(237, 244, 255) : Color.White;
            Color lineColor = Selected ? KeyboardTheme.Blue : KeyboardTheme.Border;
            using (GraphicsPath path = KeyboardTheme.RoundedRectangle(bounds, 10))
            using (Brush fill = new SolidBrush(fillColor))
            using (Pen line = new Pen(lineColor, Selected ? 1.5F : 1F))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(line, path);
            }
            Color textColor = Selected ? Color.FromArgb(40, 95, 205) : KeyboardTheme.Text;
            TextRenderer.DrawText(e.Graphics, Text, Font, bounds, textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    public sealed class KeyboardLockControl : UserControl
    {
        private readonly NumericUpDown _minutes = new NumericUpDown();
        private readonly Label _status = new Label();
        private readonly FlowLayoutPanel _choices = new FlowLayoutPanel();
        private readonly KeyboardCard _setupCard = new KeyboardCard();
        private readonly KeyboardCard _safetyCard = new KeyboardCard();
        private readonly KeyboardRoundedButton _start = new KeyboardRoundedButton();

        public KeyboardLockControl()
        {
            Dock = DockStyle.Fill;
            BackColor = KeyboardTheme.Canvas;
            Font = new Font("Microsoft YaHei UI", 10F);
            AutoScroll = true;

            Panel header = new Panel { Dock = DockStyle.Top, Height = 102, BackColor = KeyboardTheme.Canvas };
            Label eyebrow = MakeLabel("KEYBOARD COOLDOWN", 0, 0, 320, 20, 8.5F, FontStyle.Bold, KeyboardTheme.Blue);
            Label title = MakeLabel("键盘锁", 0, 24, 500, 42, 25F, FontStyle.Bold, KeyboardTheme.Text);
            Label description = MakeLabel("临时停用键盘，让鼠标保持可用。适合给电脑散热或清洁键盘。", 1, 70, 650, 26, 10F, FontStyle.Regular, KeyboardTheme.Muted);
            header.Controls.AddRange(new Control[] { eyebrow, title, description });

            _setupCard.Dock = DockStyle.Top;
            _setupCard.Height = 275;
            Label question = MakeLabel("锁定多长时间？", 28, 24, 300, 30, 14F, FontStyle.Bold, KeyboardTheme.Text);
            Label helper = MakeLabel("到时会自动恢复，也可以随时用鼠标手动解锁。", 28, 55, 500, 24, 9F, FontStyle.Regular, KeyboardTheme.Muted);
            _choices.SetBounds(23, 91, 480, 50);
            _choices.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            _choices.BackColor = Color.White;
            _choices.WrapContents = false;
            _choices.AutoSize = false;
            AddDurationChoice("5 分钟", 5);
            AddDurationChoice("15 分钟", 15);
            AddDurationChoice("30 分钟", 30);
            AddDurationChoice("60 分钟", 60);

            Label custom = MakeLabel("自定义", 28, 157, 66, 34, 9F, FontStyle.Bold, KeyboardTheme.Muted);
            _minutes.Minimum = 1;
            _minutes.Maximum = 120;
            _minutes.Value = 15;
            _minutes.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            _minutes.TextAlign = HorizontalAlignment.Center;
            _minutes.BorderStyle = BorderStyle.FixedSingle;
            _minutes.SetBounds(96, 153, 86, 36);
            Label unit = MakeLabel("分钟", 190, 157, 52, 34, 9F, FontStyle.Regular, KeyboardTheme.Muted);
            _minutes.ValueChanged += delegate { UpdateChoiceSelection(); };

            _start.Text = "开始锁定";
            _start.AccessibleName = "开始锁定键盘";
            _start.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            _start.SetBounds(28, 210, 200, 48);
            _start.Click += delegate
            {
                bool started = KeyboardLockSession.TryStart(TimeSpan.FromMinutes((double)_minutes.Value));
                SetStatus(started ? "键盘已锁定，鼠标仍可正常使用。" : "键盘锁已经在运行。", started);
            };
            _status.SetBounds(248, 210, 430, 48);
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
            _status.ForeColor = KeyboardTheme.Muted;
            _status.Text = "当前未锁定";
            _setupCard.Controls.AddRange(new Control[] { question, helper, _choices, custom, _minutes, unit, _start, _status });
            _setupCard.Resize += delegate { _status.Width = Math.Max(120, _setupCard.ClientSize.Width - 276); };

            Panel gap = new Panel { Dock = DockStyle.Top, Height = 18, BackColor = KeyboardTheme.Canvas };
            _safetyCard.Dock = DockStyle.Top;
            _safetyCard.Height = 116;
            _safetyCard.FillColor = Color.FromArgb(240, 246, 255);
            _safetyCard.BorderColor = Color.FromArgb(211, 225, 249);
            Label safetyMark = MakeLabel("✓", 26, 26, 42, 42, 17F, FontStyle.Bold, Color.FromArgb(43, 111, 225));
            safetyMark.TextAlign = ContentAlignment.MiddleCenter;
            Label safetyTitle = MakeLabel("始终留有安全退路", 80, 23, 300, 28, 11F, FontStyle.Bold, KeyboardTheme.Text);
            Label safetyText = MakeLabel("锁定窗口会保持可见，鼠标可点击“立即解锁”；超时也会自动恢复。Ctrl+Alt+Delete 不会被拦截。", 80, 52, 610, 44, 9F, FontStyle.Regular, KeyboardTheme.Muted);
            _safetyCard.Controls.AddRange(new Control[] { safetyMark, safetyTitle, safetyText });
            _safetyCard.Resize += delegate { safetyText.Width = Math.Max(200, _safetyCard.ClientSize.Width - 104); };

            Controls.Add(_safetyCard);
            Controls.Add(gap);
            Controls.Add(_setupCard);
            Controls.Add(header);
            KeyboardLockSession.SessionEnded += delegate { if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(delegate { SetStatus("键盘已恢复，可以正常输入。", false); })); };
            UpdateChoiceSelection();
        }

        private void AddDurationChoice(string text, decimal minutes)
        {
            DurationButton button = new DurationButton();
            button.Text = text;
            button.Tag = minutes;
            button.Size = new Size(100, 42);
            button.Margin = new Padding(5, 3, 5, 3);
            button.Click += delegate { _minutes.Value = (decimal)button.Tag; };
            _choices.Controls.Add(button);
        }

        private void UpdateChoiceSelection()
        {
            foreach (Control control in _choices.Controls)
            {
                DurationButton button = control as DurationButton;
                if (button != null) button.Selected = (decimal)button.Tag == _minutes.Value;
            }
        }

        private void SetStatus(string text, bool active)
        {
            _status.Text = text;
            _status.ForeColor = active ? Color.FromArgb(25, 135, 78) : KeyboardTheme.Muted;
        }

        private static Label MakeLabel(string text, int x, int y, int w, int h, float size, FontStyle style, Color color)
        {
            Label label = new Label { Text = text, Font = new Font("Microsoft YaHei UI", size, style), ForeColor = color, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft };
            label.SetBounds(x, y, w, h);
            return label;
        }
    }
}
