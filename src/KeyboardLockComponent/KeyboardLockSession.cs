using System.Threading;
using System.Windows.Threading;

namespace KeyboardCoolDownLock;

public static class KeyboardLockSession
{
    private static readonly object Sync = new();
    private static Mutex? _mutex;
    private static Thread? _thread;
    private static LockWindow? _window;
    private static Exception? _lastError;

    public static event EventHandler? SessionEnded;
    public static bool IsRunning { get { lock (Sync) return _thread is not null; } }
    public static Exception? LastError { get { lock (Sync) return _lastError; } }

    public static bool SelfTest()
    {
        NativeKeyboardHook.Install();
        NativeKeyboardHook.Uninstall();
        return true;
    }

    public static bool TryStart(TimeSpan duration)
    {
        lock (Sync)
        {
            if (_thread is not null) return false;
            _lastError = null;
            _mutex = new Mutex(true, "Local\\KeyboardCoolDownLock.SingleInstance", out bool owns);
            if (!owns)
            {
                _mutex.Dispose();
                _mutex = null;
                return false;
            }

            _thread = new Thread(() =>
            {
                try
                {
                    var window = new LockWindow(duration);
                    lock (Sync) _window = window;
                    window.Show();
                    Dispatcher.Run();
                }
                catch (Exception ex)
                {
                    lock (Sync) _lastError = ex;
                }
                finally
                {
                    NativeKeyboardHook.Uninstall();
                    lock (Sync)
                    {
                        _window = null;
                        _thread = null;
                        _mutex?.Dispose();
                        _mutex = null;
                    }
                    SessionEnded?.Invoke(null, EventArgs.Empty);
                }
            }) { IsBackground = true, Name = "YingqiTools.KeyboardLock" };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            return true;
        }
    }

    public static void Stop()
    {
        LockWindow? window;
        lock (Sync) window = _window;
        window?.Dispatcher.BeginInvoke(window.RequestUnlock);
    }
}
