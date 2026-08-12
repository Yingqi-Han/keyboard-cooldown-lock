using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KeyboardCoolDownLock;

internal static class NativeKeyboardHook
{
    private const int WhKeyboardLl = 13;
    private static readonly HookProc Callback = OnKeyboardEvent;
    private static IntPtr _hook;
    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    public static void Install()
    {
        if (_hook != IntPtr.Zero) return;
        using Process process = Process.GetCurrentProcess();
        using ProcessModule module = process.MainModule!;
        _hook = SetWindowsHookEx(WhKeyboardLl, Callback, GetModuleHandle(module.ModuleName), 0);
        if (_hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public static void Uninstall()
    {
        IntPtr hook = _hook;
        _hook = IntPtr.Zero;
        if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook);
    }

    private static IntPtr OnKeyboardEvent(int nCode, IntPtr wParam, IntPtr lParam) =>
        nCode >= 0 ? new IntPtr(1) : CallNextHookEx(_hook, nCode, wParam, lParam);

    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern IntPtr GetModuleHandle(string moduleName);
}
