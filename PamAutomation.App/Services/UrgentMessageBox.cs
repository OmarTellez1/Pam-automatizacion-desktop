using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PamAutomation.App.Services;

/// <summary>Displays a task-modal notification above normal desktop windows when user input is required.</summary>
internal static class UrgentMessageBox
{
    private const uint Topmost = 0x00040000;
    private const uint SetForeground = 0x00010000;
    private const uint TaskModal = 0x00002000;
    private const uint DefaultButton2 = 0x00000100;
    private const uint DefaultButton3 = 0x00000200;

    internal static MessageBoxResult Show(
        string message,
        string title,
        MessageBoxButton buttons,
        MessageBoxImage image,
        MessageBoxResult defaultResult = MessageBoxResult.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            return dispatcher.Invoke(() => ShowCore(message, title, buttons, image, defaultResult));
        }

        return ShowCore(message, title, buttons, image, defaultResult);
    }

    internal static uint BuildOptions(
        MessageBoxButton buttons,
        MessageBoxImage image,
        MessageBoxResult defaultResult) =>
        (uint)buttons |
        (uint)image |
        Topmost |
        SetForeground |
        TaskModal |
        GetDefaultButtonOption(buttons, defaultResult);

    private static MessageBoxResult ShowCore(
        string message,
        string title,
        MessageBoxButton buttons,
        MessageBoxImage image,
        MessageBoxResult defaultResult)
    {
        Window? owner = Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive && window.IsVisible)
            ?? Application.Current?.MainWindow;
        if (owner?.WindowState == WindowState.Minimized)
        {
            owner.WindowState = WindowState.Normal;
        }

        owner?.Activate();
        nint ownerHandle = owner is { IsLoaded: true }
            ? new WindowInteropHelper(owner).Handle
            : nint.Zero;
        int result = MessageBox(ownerHandle, message, title, BuildOptions(buttons, image, defaultResult));
        return Enum.IsDefined(typeof(MessageBoxResult), result)
            ? (MessageBoxResult)result
            : MessageBoxResult.None;
    }

    private static uint GetDefaultButtonOption(MessageBoxButton buttons, MessageBoxResult defaultResult) =>
        defaultResult switch
        {
            MessageBoxResult.No when buttons is MessageBoxButton.YesNo or MessageBoxButton.YesNoCancel => DefaultButton2,
            MessageBoxResult.Cancel when buttons == MessageBoxButton.YesNoCancel => DefaultButton3,
            MessageBoxResult.Cancel when buttons == MessageBoxButton.OKCancel => DefaultButton2,
            _ => 0
        };

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBox(nint ownerHandle, string text, string caption, uint type);
}
