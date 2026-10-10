using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DIG.App.Services;

/// <summary>
/// Fixes the classic borderless-window (WindowStyle=None) bug where maximizing
/// covers the taskbar. Hooks WM_GETMINMAXINFO to clamp the maximized bounds to the
/// monitor's working area.
/// </summary>
internal static class MaximizeFix
{
    private const int WM_GETMINMAXINFO = 0x0024;
    private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

    public static void Enable(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            HwndSource.FromHwnd(handle)?.AddHook(Hook);
        };
    }

    private static IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            WmGetMinMaxInfo(hwnd, lParam);
            handled = false;
        }
        return IntPtr.Zero;
    }

    private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(monitor, ref info))
            {
                var work = info.rcWork;
                var mon = info.rcMonitor;
                mmi.ptMaxPosition.X = Math.Abs(work.Left - mon.Left);
                mmi.ptMaxPosition.Y = Math.Abs(work.Top - mon.Top);
                mmi.ptMaxSize.X = Math.Abs(work.Right - work.Left);
                mmi.ptMaxSize.Y = Math.Abs(work.Bottom - work.Top);
                // Keep a sensible minimum so the window can still shrink.
                mmi.ptMinTrackSize.X = 820;
                mmi.ptMinTrackSize.Y = 520;
            }
        }
        Marshal.StructureToPtr(mmi, lParam, true);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }
}
