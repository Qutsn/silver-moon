using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Avalonia.Controls;

namespace GlassPlayground;

// Read-only diagnostics. These calls do not enable transparency or change OS settings.
internal static class NativeDiagnostics
{
    [DllImport("dwmapi.dll")] private static extern int DwmIsCompositionEnabled(out int enabled);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("kernel32.dll")] private static extern bool GetSystemPowerStatus(out PowerStatus status);
    [StructLayout(LayoutKind.Sequential)] private struct PowerStatus
    {
        public byte Ac, BatteryFlag, BatteryPercent, Saver;
        public uint LifeSeconds, FullLifeSeconds;
    }

    public static string Read(Window window)
    {
        if (!OperatingSystem.IsWindows()) return "Native diagnostics: Windows only";
        int compositionHr = DwmIsCompositionEnabled(out int composition);
        bool powerOk = GetSystemPowerStatus(out var power);
        object? transparency = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", null);
        return $"DWM={composition} (0x{compositionHr:X8}), TransparencySetting={transparency ?? "unknown"}, RemoteSession={GetSystemMetrics(0x1000)}, LegacyBatterySaver={(powerOk ? power.Saver.ToString() : "unknown")}. New Energy Saver is not ruled out by this legacy flag. HostBackdrop is a set-only documented attribute; no readback inference.";
    }
}
