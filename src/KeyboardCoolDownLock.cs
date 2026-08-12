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
        private static IntPtr _hook = IntPtr.Zero;

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        public static bool IsInstalled { get { return _hook != IntPtr.Zero; } }

        public static void Install()
        {
            if (IsInstalled) return;

            using (Process process = Process.GetCurrentProcess())
            using (ProcessModule module = process.MainModule)
            {
                IntPtr moduleHandle = GetModuleHandle(module.ModuleName);
                _hook = SetWindowsHookEx(WhKeyboardLl, Callback, moduleHandle, 0);
            }

            if (_hook == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        public static void Uninstall()
        {
            IntPtr hook = _hook;
            _hook = IntPtr.Zero;
            if (hook != IntPtr.Zero)
                UnhookWindowsHookEx(hook);
        }

        private static IntPtr OnKeyboardEvent(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // Keep this callback deliberately tiny. Windows can silently remove
            // a low-level hook when its callback exceeds LowLevelHooksTimeout.
            if (nCode >= 0) return new IntPtr(1);
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(
            int idHook, HookProc callback, IntPtr moduleHandle, uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(
            IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string moduleName);
    }

    internal sealed class LockForm : Form
    {
        private readonly Label _countdown;
        private readonly ProgressBar _progress;
        private readonly System.Windows.Forms.Timer _timer;
        private DateTime _deadline;
        private double _totalSeconds;
        private bool _unlocked;

        public LockForm(TimeSpan duration)
        {
            _deadline = DateTime.Now.Add(duration);
            _totalSeconds = Math.Max(1, duration.TotalSeconds);

            Text = "\u952e\u76d8\u964d\u6e29\u9501 - \u9f20\u6807\u53ef\u7528";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(520, 310);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            ShowInTaskbar = true;
            TopMost = true;
            BackColor = Color.FromArgb(24, 33, 47);
            ForeColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 10F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Icon = SystemIcons.Shield;

            Label title = new Label();
            title.Text = "\u952e\u76d8\u5df2\u9501\u5b9a";
            title.Font = new Font("Microsoft YaHei UI", 24F, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(110, 231, 183);
            title.TextAlign = ContentAlignment.MiddleCenter;
            title.SetBounds(20, 20, 480, 55);
            Controls.Add(title);

            Label explanation = new Label();
            explanation.Text = "\u9f20\u6807\u4ecd\u53ef\u6b63\u5e38\u4f7f\u7528\u3002\u9700\u8981\u6062\u590d\u952e\u76d8\u65f6\uff0c\n\u8bf7\u7528\u9f20\u6807\u70b9\u51fb\u4e0b\u65b9\u7eff\u8272\u6309\u94ae\u3002";
            explanation.Font = new Font("Microsoft YaHei UI", 11F);
            explanation.ForeColor = Color.FromArgb(209, 213, 219);
            explanation.TextAlign = ContentAlignment.MiddleCenter;
            explanation.SetBounds(20, 77, 480, 58);
            Controls.Add(explanation);

            _countdown = new Label();
            _countdown.Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
            _countdown.ForeColor = Color.FromArgb(147, 197, 253);
            _countdown.TextAlign = ContentAlignment.MiddleCenter;
            _countdown.SetBounds(20, 137, 480, 32);
            Controls.Add(_countdown);

            _progress = new ProgressBar();
            _progress.Minimum = 0;
            _progress.Maximum = 1000;
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.SetBounds(55, 172, 410, 12);
            Controls.Add(_progress);

            Button extendButton = new Button();
            extendButton.Name = "ExtendButton";
            extendButton.Text = "\u5ef6\u957f 5 \u5206\u949f";
            extendButton.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            extendButton.FlatStyle = FlatStyle.Flat;
            extendButton.FlatAppearance.BorderColor = Color.FromArgb(96, 165, 250);
            extendButton.BackColor = Color.FromArgb(30, 64, 175);
            extendButton.ForeColor = Color.White;
            extendButton.SetBounds(55, 202, 145, 54);
            extendButton.Click += delegate
            {
                _deadline = _deadline.AddMinutes(5);
                _totalSeconds += 300;
                UpdateDisplay();
            };
            Controls.Add(extendButton);

            Button unlockButton = new Button();
            unlockButton.Name = "UnlockButton";
            unlockButton.Text = "\u7acb\u5373\u89e3\u9501\u952e\u76d8";
            unlockButton.AccessibleName = "Unlock keyboard";
            unlockButton.Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold);
            unlockButton.FlatStyle = FlatStyle.Flat;
            unlockButton.FlatAppearance.BorderSize = 0;
            unlockButton.BackColor = Color.FromArgb(22, 163, 74);
            unlockButton.ForeColor = Color.White;
            unlockButton.SetBounds(215, 202, 250, 54);
            unlockButton.Click += delegate { UnlockAndClose(); };
            Controls.Add(unlockButton);

            Label safety = new Label();
            safety.Text = "\u5b89\u5168\u515c\u5e95\uff1aCtrl+Alt+Delete \u4e0d\u4f1a\u88ab\u672c\u5de5\u5177\u62e6\u622a";
            safety.Font = new Font("Microsoft YaHei UI", 9F);
            safety.ForeColor = Color.FromArgb(156, 163, 175);
            safety.TextAlign = ContentAlignment.MiddleCenter;
            safety.SetBounds(20, 270, 480, 24);
            Controls.Add(safety);

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 250;
            _timer.Tick += delegate
            {
                if (DateTime.Now >= _deadline)
                    UnlockAndClose();
                else
                    UpdateDisplay();
            };

            Shown += delegate
            {
                NativeKeyboardHook.Install();
                UpdateDisplay();
                _timer.Start();
                Activate();
                BringToFront();
            };
            FormClosing += delegate { ReleaseHook(); };
        }

        private void UpdateDisplay()
        {
            TimeSpan remaining = _deadline - DateTime.Now;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

            int hours = (int)remaining.TotalHours;
            _countdown.Text = hours > 0
                ? string.Format("{0}:{1:00}:{2:00} \u540e\u81ea\u52a8\u89e3\u9501", hours, remaining.Minutes, remaining.Seconds)
                : string.Format("{0:00}:{1:00} \u540e\u81ea\u52a8\u89e3\u9501", remaining.Minutes, remaining.Seconds);

            double ratio = Math.Min(1, Math.Max(0, remaining.TotalSeconds / _totalSeconds));
            _progress.Value = (int)Math.Round(ratio * _progress.Maximum);
        }

        private void UnlockAndClose()
        {
            if (_unlocked) return;
            _unlocked = true;
            ReleaseHook();
            Close();
        }

        private void ReleaseHook()
        {
            if (_timer != null) _timer.Stop();
            NativeKeyboardHook.Uninstall();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _timer != null) _timer.Dispose();
            base.Dispose(disposing);
        }
    }

    internal static class Program
    {
        private static Mutex _singleInstance;

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static int Main(string[] args)
        {
            bool ownsMutex;
            _singleInstance = new Mutex(true, "Local\\KeyboardCoolDownLock.SingleInstance", out ownsMutex);
            if (!ownsMutex) return 2;

            try
            {
                if (HasArgument(args, "--self-test"))
                {
                    NativeKeyboardHook.Install();
                    NativeKeyboardHook.Uninstall();
                    return 0;
                }

                TimeSpan duration = ParseDuration(args);
                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    NativeKeyboardHook.Uninstall();
                    MessageBox.Show(
                        "\u952e\u76d8\u9501\u5df2\u5b89\u5168\u91ca\u653e\u3002\n\n" + e.Exception.Message,
                        "\u952e\u76d8\u964d\u6e29\u9501\u53d1\u751f\u9519\u8bef",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                };

                using (LockForm form = new LockForm(duration))
                    Application.Run(form);

                return 0;
            }
            catch (Exception ex)
            {
                NativeKeyboardHook.Uninstall();
                MessageBox.Show(
                    "\u672a\u80fd\u9501\u5b9a\u952e\u76d8\uff0c\u952e\u76d8\u4ecd\u53ef\u6b63\u5e38\u4f7f\u7528\u3002\n\n" + ex.Message,
                    "\u952e\u76d8\u964d\u6e29\u9501",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 1;
            }
            finally
            {
                NativeKeyboardHook.Uninstall();
                if (_singleInstance != null) _singleInstance.Dispose();
            }
        }

        private static bool HasArgument(string[] args, string expected)
        {
            foreach (string value in args)
                if (string.Equals(value, expected, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static TimeSpan ParseDuration(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                int value;
                if (string.Equals(args[i], "--seconds", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(args[i + 1], out value))
                    return TimeSpan.FromSeconds(Math.Max(3, Math.Min(7200, value)));

                if (string.Equals(args[i], "--minutes", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(args[i + 1], out value))
                    return TimeSpan.FromMinutes(Math.Max(1, Math.Min(120, value)));
            }

            return TimeSpan.FromMinutes(15);
        }
    }
}
