using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace KeyboardCoolDownLock;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        if (e.Args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            Shutdown(KeyboardLockSession.SelfTest() ? 0 : 1);
            return;
        }
        int seconds = Read(e.Args, "--seconds", 0);
        int minutes = Read(e.Args, "--minutes", 15);
        TimeSpan duration = seconds > 0
            ? TimeSpan.FromSeconds(Math.Clamp(seconds, 3, 7200))
            : TimeSpan.FromMinutes(Math.Clamp(minutes, 1, 120));
        if (!KeyboardLockSession.TryStart(duration)) { Shutdown(2); return; }
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (_, _) =>
        {
            if (KeyboardLockSession.IsRunning) return;
            timer.Stop();
            Exception? error = KeyboardLockSession.LastError;
            string? diagnosticPath = ReadText(e.Args, "--diagnostic-file");
            if (error is not null && !string.IsNullOrWhiteSpace(diagnosticPath))
                File.WriteAllText(diagnosticPath, error.ToString());
            Shutdown(error is null ? 0 : 3);
        };
        timer.Start();
    }

    private static int Read(string[] args, string key, int fallback)
    {
        for (int index = 0; index < args.Length - 1; index++)
            if (string.Equals(args[index], key, StringComparison.OrdinalIgnoreCase) && int.TryParse(args[index + 1], out int value)) return value;
        return fallback;
    }

    private static string? ReadText(string[] args, string key)
    {
        for (int index = 0; index < args.Length - 1; index++)
            if (string.Equals(args[index], key, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
        return null;
    }
}
