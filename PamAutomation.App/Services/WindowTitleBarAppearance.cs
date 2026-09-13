using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PamAutomation.App.Services;

internal static class WindowTitleBarAppearance
{
    private const int DarkGrayCaptionColor = 0x00362F2B;
    private const int LightCaptionTextColor = 0x00FFFFFF;

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    internal static bool TryApply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        nint windowHandle = new WindowInteropHelper(window).Handle;
        if (windowHandle == nint.Zero)
        {
            return false;
        }

        try
        {
            int darkModeEnabled = 1;
            _ = DwmSetWindowAttribute(
                windowHandle,
                DwmwaUseImmersiveDarkMode,
                ref darkModeEnabled,
                sizeof(int));

            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            {
                return true;
            }

            int captionColor = DarkGrayCaptionColor;
            int textColor = LightCaptionTextColor;
            _ = DwmSetWindowAttribute(windowHandle, DwmwaBorderColor, ref captionColor, sizeof(int));
            int captionResult = DwmSetWindowAttribute(
                windowHandle,
                DwmwaCaptionColor,
                ref captionColor,
                sizeof(int));
            _ = DwmSetWindowAttribute(windowHandle, DwmwaTextColor, ref textColor, sizeof(int));
            return captionResult >= 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}
