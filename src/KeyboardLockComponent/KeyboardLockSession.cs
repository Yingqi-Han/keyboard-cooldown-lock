using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace KeyboardCoolDownLock;

public static class KeyboardLockSession
{
    private static readonly object Sync = new();
    private static Mutex? _mutex;
    private static LockWindow? _window;
    private static Exception? _lastError;

    public static event EventHandler? SessionEnded;
    public static bool IsRunning
    {
        get { lock (Sync) return _window is not null && NativeKeyboardHook.IsInstalled; }
    }
    public static Exception? LastError { get { lock (Sync) return _lastError; } }

    public static bool SelfTest()
    {
        long baseline = NativeKeyboardHook.InterceptedEventCount;
        Exception? sendError = null;
        DispatcherFrame frame = new();
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        DispatcherTimer readinessTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
        readinessTimer.Tick += (_, _) =>
        {
            if (Volatile.Read(ref sendError) is null
                && NativeKeyboardHook.InterceptedEventCount < baseline + 2
                && DateTime.UtcNow < deadline) return;
            readinessTimer.Stop();
            frame.Continue = false;
        };

        try
        {
            NativeKeyboardHook.Install();
            readinessTimer.Start();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    Thread.Sleep(100);
                    NativeKeyboardHook.SendVerificationKey();
                }
                catch (Exception ex) { Volatile.Write(ref sendError, ex); }
            });

            Dispatcher.PushFrame(frame);
            return Volatile.Read(ref sendError) is null && NativeKeyboardHook.InterceptedEventCount >= baseline + 2;
        }
        finally
        {
            readinessTimer.Stop();
            NativeKeyboardHook.Uninstall();
        }
    }

    public static bool TryStart(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        System.Windows.Threading.Dispatcher? dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            lock (Sync) _lastError = new InvalidOperationException("Keyboard lock requires a running WPF application.");
            return false;
        }
        return dispatcher.CheckAccess()
            ? TryStartOnUiThread(duration)
            : dispatcher.Invoke(() => TryStartOnUiThread(duration));
    }

    private static bool TryStartOnUiThread(TimeSpan duration)
    {
        lock (Sync)
        {
            if (_window is not null) return false;
            _lastError = null;
            _mutex = new Mutex(true, "Local\\KeyboardCoolDownLock.SingleInstance", out bool owns);
            if (!owns)
            {
                _mutex.Dispose();
                _mutex = null;
                return false;
            }
        }

        LockWindow? window = null;
        try
        {
            NativeKeyboardHook.Install();
            window = new LockWindow(duration);
            window.Closed += Window_Closed;
            lock (Sync) _window = window;
            window.Show();
            window.Activate();
            if (!window.IsVisible || !NativeKeyboardHook.IsInstalled)
                throw new InvalidOperationException("Keyboard lock did not become ready.");
            return true;
        }
        catch (Exception ex)
        {
            if (window is not null)
            {
                window.Closed -= Window_Closed;
                if (window.IsVisible) window.Close();
            }
            try { NativeKeyboardHook.Uninstall(); } catch { }
            lock (Sync)
            {
                _lastError = ex;
                _window = null;
                DisposeMutex();
            }
            return false;
        }
    }

    public static void Stop()
    {
        LockWindow? window;
        lock (Sync) window = _window;
        if (window is null) return;
        if (window.Dispatcher.CheckAccess()) window.RequestUnlock();
        else window.Dispatcher.BeginInvoke(window.RequestUnlock);
    }

    private static void Window_Closed(object? sender, EventArgs e)
    {
        try { NativeKeyboardHook.Uninstall(); }
        catch (Exception ex) { lock (Sync) _lastError = ex; }
        lock (Sync)
        {
            if (ReferenceEquals(_window, sender)) _window = null;
            DisposeMutex();
        }
        SessionEnded?.Invoke(null, EventArgs.Empty);
    }

    private static void DisposeMutex()
    {
        if (_mutex is null) return;
        try { _mutex.ReleaseMutex(); }
        catch (ApplicationException) { }
        _mutex.Dispose();
        _mutex = null;
    }
}
