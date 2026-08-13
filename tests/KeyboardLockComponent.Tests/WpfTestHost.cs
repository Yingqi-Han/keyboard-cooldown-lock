using System.Windows;
using System.Windows.Threading;

namespace KeyboardCoolDownLock.Tests;

internal static class WpfTestHost
{
    private static readonly Lazy<Dispatcher> UiDispatcher = new(CreateDispatcher);

    public static T Invoke<T>(Func<T> action) =>
        UiDispatcher.Value.Invoke(action);

    public static void Invoke(Action action) =>
        UiDispatcher.Value.Invoke(action);

    public static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;
            Thread.Sleep(25);
        }
        return condition();
    }

    public static void Cleanup()
    {
        Invoke(() =>
        {
            KeyboardLockSession.Stop();
            foreach (Window window in Application.Current.Windows.Cast<Window>().ToArray())
                window.Close();
        });
        if (NativeKeyboardHook.IsInstalled)
            NativeKeyboardHook.Uninstall();
    }

    private static Dispatcher CreateDispatcher()
    {
        using ManualResetEventSlim ready = new();
        Dispatcher? dispatcher = null;
        Exception? startupError = null;
        Thread uiThread = new(() =>
        {
            try
            {
                _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                dispatcher = Dispatcher.CurrentDispatcher;
            }
            catch (Exception ex)
            {
                startupError = ex;
            }
            finally
            {
                ready.Set();
            }

            if (dispatcher is not null) Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "Keyboard lock test UI"
        };
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(10)))
            throw new TimeoutException("The WPF test dispatcher did not start.");
        if (startupError is not null)
            throw new InvalidOperationException("The WPF test dispatcher failed to start.", startupError);
        return dispatcher ?? throw new InvalidOperationException("The WPF dispatcher is unavailable.");
    }
}
