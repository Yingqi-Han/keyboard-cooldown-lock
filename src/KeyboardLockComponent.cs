using System;
using System.Diagnostics;
using System.Drawing;
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

    public sealed class KeyboardLockControl : UserControl
    {
        private readonly NumericUpDown _minutes = new NumericUpDown();
        private readonly Label _status = new Label();

        public KeyboardLockControl()
        {
            Dock = DockStyle.Fill; BackColor = Color.White; Font = new Font("Microsoft YaHei UI", 10F);
            Label title = new Label { Text = "\u952e\u76d8\u9501", Font = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold), AutoSize = true, Location = new Point(28, 24) };
            Label description = new Label { Text = "\u4e34\u65f6\u9501\u5b9a\u952e\u76d8\uff0c\u9f20\u6807\u4ecd可\u7528\uff0c\u9002\u5408\u964d\u6e29\u6216\u6e05\u6d01\u952e\u76d8\u3002", AutoSize = true, Location = new Point(31, 72), ForeColor = Color.DimGray };
            Label duration = new Label { Text = "\u81ea\u52a8\u89e3\u9501\u65f6\u95f4\uff08\u5206\u949f\uff09", AutoSize = true, Location = new Point(31, 125) };
            _minutes.Minimum = 1; _minutes.Maximum = 120; _minutes.Value = 15; _minutes.SetBounds(220, 120, 90, 30);
            Button start = new Button { Text = "\u5f00\u59cb\u9501\u5b9a", BackColor = Color.FromArgb(22, 163, 74), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold) };
            start.SetBounds(31, 180, 210, 48); start.Click += delegate { _status.Text = KeyboardLockSession.TryStart(TimeSpan.FromMinutes((double)_minutes.Value)) ? "\u952e\u76d8\u5df2\u9501\u5b9a\u3002" : "\u952e\u76d8\u9501\u5df2\u5728\u8fd0\u884c\u3002"; };
            _status.SetBounds(31, 245, 450, 30); _status.ForeColor = Color.FromArgb(37, 99, 235);
            KeyboardLockSession.SessionEnded += delegate { if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(delegate { _status.Text = "\u952e\u76d8\u5df2\u6062\u590d\u3002"; })); };
            Controls.AddRange(new Control[] { title, description, duration, _minutes, start, _status });
        }
    }
}
