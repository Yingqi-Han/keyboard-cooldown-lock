using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Xunit;

namespace KeyboardCoolDownLock.Tests;

public sealed class KeyboardLockSessionTests : IDisposable
{
    public KeyboardLockSessionTests() => WpfTestHost.Cleanup();

    public void Dispose() => WpfTestHost.Cleanup();

    [Fact]
    public void TryStart_RejectsNonPositiveDuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => KeyboardLockSession.TryStart(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => KeyboardLockSession.TryStart(TimeSpan.FromSeconds(-1)));
        Assert.False(KeyboardLockSession.IsRunning);
        Assert.False(NativeKeyboardHook.IsInstalled);
    }

    [Fact]
    public void SelfTest_InterceptsInjectedKeyAndAlwaysUninstallsHook()
    {
        long before = NativeKeyboardHook.InterceptedEventCount;

        bool result = WpfTestHost.Invoke(KeyboardLockSession.SelfTest);

        Assert.True(result);
        Assert.True(NativeKeyboardHook.InterceptedEventCount >= before + 2);
        Assert.False(NativeKeyboardHook.IsInstalled);
        Assert.False(KeyboardLockSession.IsRunning);
    }

    [Fact]
    public void TryStart_ShowsVisibleRecoveryWindowAndProgrammaticStopCleansState()
    {
        int ended = 0;
        EventHandler handler = (_, _) => Interlocked.Increment(ref ended);
        KeyboardLockSession.SessionEnded += handler;
        try
        {
            Assert.True(WpfTestHost.Invoke(() => KeyboardLockSession.TryStart(TimeSpan.FromSeconds(20))));

            WindowSnapshot snapshot = WpfTestHost.Invoke(() =>
            {
                LockWindow window = Assert.Single(Application.Current.Windows.OfType<LockWindow>());
                return new WindowSnapshot(window.IsVisible, new WindowInteropHelper(window).Handle, window.Title);
            });
            Assert.True(snapshot.IsVisible);
            Assert.NotEqual(IntPtr.Zero, snapshot.Handle);
            Assert.False(string.IsNullOrWhiteSpace(snapshot.Title));
            Assert.True(KeyboardLockSession.IsRunning);
            Assert.True(NativeKeyboardHook.IsInstalled);

            WpfTestHost.Invoke(KeyboardLockSession.Stop);

            Assert.False(KeyboardLockSession.IsRunning);
            Assert.False(NativeKeyboardHook.IsInstalled);
            Assert.Equal(1, Volatile.Read(ref ended));
            Assert.Empty(WpfTestHost.Invoke(() => Application.Current.Windows.OfType<LockWindow>().ToArray()));
        }
        finally
        {
            KeyboardLockSession.SessionEnded -= handler;
        }
    }

    [Fact]
    public void Session_AutomaticallyUnlocksAfterTimeout()
    {
        int ended = 0;
        EventHandler handler = (_, _) => Interlocked.Increment(ref ended);
        KeyboardLockSession.SessionEnded += handler;
        try
        {
            Assert.True(WpfTestHost.Invoke(() => KeyboardLockSession.TryStart(TimeSpan.FromMilliseconds(750))));
            Assert.True(WpfTestHost.WaitUntil(() => !KeyboardLockSession.IsRunning, TimeSpan.FromSeconds(5)));

            Assert.False(NativeKeyboardHook.IsInstalled);
            Assert.Equal(1, Volatile.Read(ref ended));
            Assert.Empty(WpfTestHost.Invoke(() => Application.Current.Windows.OfType<LockWindow>().ToArray()));
        }
        finally
        {
            KeyboardLockSession.SessionEnded -= handler;
        }
    }

    [Fact]
    public void UnlockButton_ClickImmediatelyReleasesSession()
    {
        Assert.True(WpfTestHost.Invoke(() => KeyboardLockSession.TryStart(TimeSpan.FromSeconds(20))));

        WpfTestHost.Invoke(() =>
        {
            LockWindow window = Assert.Single(Application.Current.Windows.OfType<LockWindow>());
            window.UnlockButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        });

        Assert.False(KeyboardLockSession.IsRunning);
        Assert.False(NativeKeyboardHook.IsInstalled);
        Assert.Empty(WpfTestHost.Invoke(() => Application.Current.Windows.OfType<LockWindow>().ToArray()));
    }

    [Fact]
    public void TryStart_RejectsRepeatedSessionWithoutBreakingActiveLock()
    {
        Assert.True(WpfTestHost.Invoke(() => KeyboardLockSession.TryStart(TimeSpan.FromSeconds(20))));

        Assert.False(WpfTestHost.Invoke(() => KeyboardLockSession.TryStart(TimeSpan.FromSeconds(20))));
        Assert.True(KeyboardLockSession.IsRunning);
        Assert.True(NativeKeyboardHook.IsInstalled);
        Assert.Single(WpfTestHost.Invoke(() => Application.Current.Windows.OfType<LockWindow>().ToArray()));

        WpfTestHost.Invoke(KeyboardLockSession.Stop);
        Assert.False(KeyboardLockSession.IsRunning);
        Assert.False(NativeKeyboardHook.IsInstalled);
    }

    [Fact]
    public void UnexpectedWindowClose_ReleasesHookMutexAndAllowsNextSession()
    {
        Assert.True(WpfTestHost.Invoke(() => KeyboardLockSession.TryStart(TimeSpan.FromSeconds(20))));

        WpfTestHost.Invoke(() => Assert.Single(Application.Current.Windows.OfType<LockWindow>()).Close());

        Assert.False(KeyboardLockSession.IsRunning);
        Assert.False(NativeKeyboardHook.IsInstalled);
        Assert.True(WpfTestHost.Invoke(() => KeyboardLockSession.TryStart(TimeSpan.FromSeconds(20))));

        WpfTestHost.Invoke(KeyboardLockSession.Stop);
        Assert.False(KeyboardLockSession.IsRunning);
        Assert.False(NativeKeyboardHook.IsInstalled);
    }

    private sealed record WindowSnapshot(bool IsVisible, IntPtr Handle, string Title);
}
