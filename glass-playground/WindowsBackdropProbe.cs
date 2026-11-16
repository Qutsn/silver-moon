using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace GlassPlayground;

// Diagnostic alternatives using documented Windows 11 DWM APIs only.
internal static class WindowsBackdropProbe
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
    [StructLayout(LayoutKind.Sequential)] private struct Margins { public int Left, Right, Top, Bottom; }

    public static string ReapplyHost(Window window)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return "HostBackdrop requires Windows 11";
        int enabled = 1;
        int hr = DwmSetWindowAttribute(window.TryGetPlatformHandle()!.Handle, 17, ref enabled, 4);
        return $"Reapply DWMWA_USE_HOSTBACKDROPBRUSH HRESULT=0x{hr:X8}";
    }

    public static string SetSystemMaterial(Window window, bool enabled)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621)) return "System backdrop requires Windows 11 build 22621+";
        var hwnd = window.TryGetPlatformHandle()!.Handle;
        int material = enabled ? 3 /* DWMSBT_TRANSIENTWINDOW */ : 1 /* DWMSBT_NONE */;
        int hr = DwmSetWindowAttribute(hwnd, 38 /* DWMWA_SYSTEMBACKDROP_TYPE */, ref material, 4);
        var margins = enabled ? new Margins { Left=-1, Right=-1, Top=-1, Bottom=-1 } : default;
        int frameHr = DwmExtendFrameIntoClientArea(hwnd, ref margins);
        return $"DWM Desktop Acrylic={enabled}, material HRESULT=0x{hr:X8}, frame HRESULT=0x{frameHr:X8}";
    }
}
