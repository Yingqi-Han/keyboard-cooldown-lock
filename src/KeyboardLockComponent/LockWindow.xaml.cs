using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace KeyboardCoolDownLock;

public partial class LockWindow : FluentWindow
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private DateTime _deadline;
    private double _totalSeconds;
    private bool _released;

    public LockWindow(TimeSpan duration)
    {
        _deadline = DateTime.Now.Add(duration);
        _totalSeconds = Math.Max(1, duration.TotalSeconds);
        InitializeComponent();
        _timer.Tick += Timer_Tick;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        NativeKeyboardHook.Install();
        UpdateDisplay();
        _timer.Start();
        Activate();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (DateTime.Now >= _deadline) RequestUnlock();
        else UpdateDisplay();
    }

    private void Extend_Click(object sender, RoutedEventArgs e)
    {
        _deadline = _deadline.AddMinutes(5);
        _totalSeconds += 300;
        UpdateDisplay();
    }

    private void Unlock_Click(object sender, RoutedEventArgs e) => RequestUnlock();

    private void OnClosing(object? sender, CancelEventArgs e) => Release();

    public void RequestUnlock()
    {
        Release();
        Close();
        Dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
    }

    private void UpdateDisplay()
    {
        TimeSpan remaining = _deadline - DateTime.Now;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        CountdownText.Text = remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours}:{remaining.Minutes:00}:{remaining.Seconds:00} 后自动解锁"
            : $"{remaining.Minutes:00}:{remaining.Seconds:00} 后自动解锁";
        CountdownProgress.Value = Math.Clamp(remaining.TotalSeconds / _totalSeconds, 0, 1) * 1000;
    }

    private void Release()
    {
        if (_released) return;
        _released = true;
        _timer.Stop();
        NativeKeyboardHook.Uninstall();
    }
}
