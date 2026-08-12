using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace KeyboardCoolDownLock;

public partial class KeyboardLockControl : UserControl
{
    public KeyboardLockControl()
    {
        InitializeComponent();
        KeyboardLockSession.SessionEnded += OnSessionEnded;
        Unloaded += (_, _) => KeyboardLockSession.SessionEnded -= OnSessionEnded;
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string value } && double.TryParse(value, out double minutes)) MinutesBox.Value = minutes;
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        double rawMinutes = MinutesBox.Value ?? 15;
        double minutes = double.IsNaN(rawMinutes) ? 15 : Math.Clamp(rawMinutes, 1, 120);
        bool started = KeyboardLockSession.TryStart(TimeSpan.FromMinutes(minutes));
        StatusBar.Severity = started ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        StatusBar.Title = started ? "键盘已锁定" : "键盘锁已在运行";
        StatusBar.Message = started ? "鼠标仍可正常使用。" : "请使用现有锁定窗口。";
    }

    private void OnSessionEnded(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            StatusBar.Severity = InfoBarSeverity.Success;
            StatusBar.Title = "键盘已恢复";
            StatusBar.Message = "现在可以正常输入。";
        });
    }
}
