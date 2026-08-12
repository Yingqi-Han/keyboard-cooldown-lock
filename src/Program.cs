using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KeyboardCoolDownLock
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (Has(args, "--self-test")) return KeyboardLockSession.SelfTest() ? 0 : 1;
                int seconds = Read(args, "--seconds", 0);
                int minutes = Read(args, "--minutes", 15);
                TimeSpan duration = seconds > 0 ? TimeSpan.FromSeconds(Math.Max(3, Math.Min(7200, seconds))) : TimeSpan.FromMinutes(Math.Max(1, Math.Min(120, minutes)));
                try { SetProcessDPIAware(); } catch { }
                if (!KeyboardLockSession.TryStart(duration)) return 2;
                while (KeyboardLockSession.IsRunning) System.Threading.Thread.Sleep(100);
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("\u672a\u80fd\u9501\u5b9a\u952e\u76d8\uff0c\u952e\u76d8\u4ecd\u53ef\u6b63\u5e38\u4f7f\u7528\u3002\n\n" + ex.Message, "\u952e\u76d8\u964d\u6e29\u9501", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        private static bool Has(string[] args, string value) { foreach (string arg in args) if (string.Equals(arg, value, StringComparison.OrdinalIgnoreCase)) return true; return false; }
        private static int Read(string[] args, string key, int fallback) { for (int i = 0; i < args.Length - 1; i++) { int value; if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase) && int.TryParse(args[i + 1], out value)) return value; } return fallback; }
    }
}
