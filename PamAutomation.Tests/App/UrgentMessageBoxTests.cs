using System.Windows;
using PamAutomation.App.Services;

namespace PamAutomation.Tests.App;

[TestClass]
public sealed class UrgentMessageBoxTests
{
    private const uint Topmost = 0x00040000;
    private const uint SetForeground = 0x00010000;
    private const uint TaskModal = 0x00002000;
    private const uint DefaultButton2 = 0x00000100;

    [TestMethod]
    public void BuildOptions_ImportantWarning_AlwaysRequestsForegroundTopmostTaskModal()
    {
        uint options = UrgentMessageBox.BuildOptions(
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        Assert.AreEqual(Topmost, options & Topmost);
        Assert.AreEqual(SetForeground, options & SetForeground);
        Assert.AreEqual(TaskModal, options & TaskModal);
        Assert.AreEqual(DefaultButton2, options & DefaultButton2);
        Assert.AreEqual((uint)MessageBoxButton.YesNo, options & 0xF);
        Assert.AreEqual((uint)MessageBoxImage.Warning, options & 0xF0);
    }
}
