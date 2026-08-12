using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KeyboardCoolDownLock;

internal static class NativeKeyboardHook
{
    private const int WhKeyboardLl = 13;
    private static readonly object Sync = new();
    private static readonly HookProc Callback = OnKeyboardEvent;
    private static IntPtr _hook;
    private static long _interceptedEventCount;
    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    public static bool IsInstalled
    {
        get { lock (Sync) return _hook != IntPtr.Zero; }
    }

    public static long InterceptedEventCount => Interlocked.Read(ref _interceptedEventCount);

    public static void Install()
    {
        lock (Sync)
        {
            if (_hook != IntPtr.Zero) return;
            using Process process = Process.GetCurrentProcess();
            using ProcessModule module = process.MainModule!;
            _hook = SetWindowsHookEx(WhKeyboardLl, Callback, GetModuleHandle(module.ModuleName), 0);
            if (_hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public static void Uninstall()
    {
        lock (Sync)
        {
            IntPtr hook = _hook;
            _hook = IntPtr.Zero;
            if (hook != IntPtr.Zero && !UnhookWindowsHookEx(hook))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public static void SendVerificationKey()
    {
        Input[] inputs =
        [
            new() { Type = InputKeyboard, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = VirtualKeyF24 } } },
            new() { Type = InputKeyboard, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = VirtualKeyF24, Flags = KeyEventKeyUp } } }
        ];
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private static IntPtr OnKeyboardEvent(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0) return CallNextHookEx(_hook, nCode, wParam, lParam);
        Interlocked.Increment(ref _interceptedEventCount);
        return new IntPtr(1);
    }

    private const uint InputKeyboard = 1;
    private const ushort VirtualKeyF24 = 0x87;
    private const uint KeyEventKeyUp = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint inputCount, Input[] inputs, int size);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern IntPtr GetModuleHandle(string moduleName);
}
