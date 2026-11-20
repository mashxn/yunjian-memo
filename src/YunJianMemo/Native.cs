using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace YunJianMemo;

internal static class Native
{
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;
    public const int WS_CHILD = 0x40000000;
    public const int WS_POPUP = unchecked((int)0x80000000);
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_APPWINDOW = 0x00040000;

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_FRAMECHANGED = 0x0020;
    public const uint SWP_SHOWWINDOW = 0x0040;

    public static readonly IntPtr HWND_TOP = IntPtr.Zero;
    public static readonly IntPtr HWND_BOTTOM = new(1);

    public const uint WM_HOTKEY = 0x0312;
    public const uint WM_SPAWN_WORKER = 0x052C;

    public const int SW_HIDE = 0;
    public const int SW_SHOW = 5;
    public const int SW_RESTORE = 9;

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter,
        string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam,
        IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y,
        int cx, int cy, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        => IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

    public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value)
        => IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, value)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, value.ToInt32()));

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }
}

public sealed class DesktopHost
{
    private IntPtr _hwnd;
    private IntPtr _worker;
    private IntPtr _originalStyle;
    private bool _attached;

    public bool IsAttached => _attached;
    public IntPtr WorkerHandle => _worker;

    public bool Attach(Window window)
    {
        if (_attached) return true;
        _hwnd = new WindowInteropHelper(window).Handle;
        if (_hwnd == IntPtr.Zero) return false;

        _worker = FindWallpaperHost();
        if (_worker == IntPtr.Zero) return false;

        Native.GetWindowRect(_hwnd, out var rect);
        var point = new Native.POINT { X = rect.Left, Y = rect.Top };
        Native.ScreenToClient(_worker, ref point);

        _originalStyle = Native.GetWindowLongPtr(_hwnd, Native.GWL_STYLE);
        Marshal.SetLastPInvokeError(0);
        var previous = Native.SetParent(_hwnd, _worker);
        var error = Marshal.GetLastPInvokeError();
        if (previous == IntPtr.Zero && error != 0)
        {
            _worker = IntPtr.Zero;
            return false;
        }

        Native.SetWindowPos(_hwnd, Native.HWND_TOP, point.X, point.Y,
            Math.Max(1, rect.Width), Math.Max(1, rect.Height),
            Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW | Native.SWP_FRAMECHANGED);
        _attached = true;
        return true;
    }

    public void Detach(Window window)
    {
        if (!_attached || _hwnd == IntPtr.Zero) return;
        Native.SetParent(_hwnd, IntPtr.Zero);
        if (_originalStyle != IntPtr.Zero)
            Native.SetWindowLongPtr(_hwnd, Native.GWL_STYLE, _originalStyle);
        Native.SetWindowPos(_hwnd, Native.HWND_TOP, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER |
            Native.SWP_NOACTIVATE | Native.SWP_FRAMECHANGED);
        _attached = false;
        _worker = IntPtr.Zero;
    }

    public void BringToFront()
    {
        if (!_attached || _hwnd == IntPtr.Zero) return;
        Native.SetWindowPos(_hwnd, Native.HWND_TOP, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    public bool MoveToScreen(int left, int top, int width, int height)
    {
        if (_hwnd == IntPtr.Zero) return false;
        if (_attached && _worker != IntPtr.Zero)
        {
            var origin = new Native.POINT { X = 0, Y = 0 };
            Native.ClientToScreen(_worker, ref origin);
            return Native.SetWindowPos(_hwnd, IntPtr.Zero,
                left - origin.X, top - origin.Y, width, height,
                Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
        }

        return Native.SetWindowPos(_hwnd, IntPtr.Zero, left, top, width, height,
            Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
    }

    public Rect GetScreenRect()
    {
        if (_hwnd == IntPtr.Zero || !Native.GetWindowRect(_hwnd, out var rect))
            return Rect.Empty;
        return new Rect(rect.Left, rect.Top, rect.Width, rect.Height);
    }

    public void SetBottomMost()
    {
        if (_attached || _hwnd == IntPtr.Zero) return;
        Native.SetWindowPos(_hwnd, Native.HWND_BOTTOM, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    private static IntPtr FindWallpaperHost()
    {
        var progman = Native.FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
        {
            for (var i = 0; i < 3; i++)
            {
                Native.SendMessageTimeout(progman, Native.WM_SPAWN_WORKER,
                    IntPtr.Zero, IntPtr.Zero, 0, 800, out _);
            }
        }

        var workerw = IntPtr.Zero;
        Native.EnumWindows((top, _) =>
        {
            var shellView = Native.FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView != IntPtr.Zero && workerw == IntPtr.Zero)
            {
                // 挂在“图标宿主” WorkerW 上并置顶，才能接住鼠标；
                // 若挂到其后面的壁纸 WorkerW，窗口虽可见，但点击会被桌面图标层吞掉。
                workerw = top;
            }
            return true;
        }, IntPtr.Zero);

        if (workerw != IntPtr.Zero) return workerw;

        // 部分系统把桌面图标挂在 Progman 下，退回 Progman。
        return progman;
    }
}

public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "YunJianMemo";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string s && !string.IsNullOrWhiteSpace(s);
        }
        catch { return false; }
    }

    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey);
            if (key == null) return false;
            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exe)) return false;
                key.SetValue(ValueName, $"\"{exe}\" --autostart");
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }
            return true;
        }
        catch (Exception ex)
        {
            Store.Log("设置开机自启失败。", ex);
            return false;
        }
    }
}
