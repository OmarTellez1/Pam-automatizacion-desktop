using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.App.ViewModels;

namespace PamAutomation.Tests;

[TestClass]
public sealed class MainWindowViewModelTests
{
    [TestMethod]
    public void InitialState_IsSafeAndIncomplete()
    {
        var viewModel = new MainWindowViewModel();

        Assert.IsTrue(viewModel.AreControlsEnabled);
        Assert.IsFalse(viewModel.IsConfigurationComplete);
        Assert.IsFalse(viewModel.CanStartAutomation);
        Assert.IsFalse(viewModel.IsTestActionVisible);
        Assert.IsFalse(viewModel.SendEmailAtEnd);
        Assert.AreEqual("Configuración pendiente", viewModel.ConfigurationSummaryText);
    }

    [TestMethod]
    public void RunningState_DisablesAvailableActions()
    {
        var viewModel = CreateConfiguredViewModel();

        viewModel.IsRunning = true;

        Assert.IsFalse(viewModel.AreControlsEnabled);
        Assert.IsFalse(viewModel.CanStartAutomation);
        Assert.IsFalse(viewModel.IsTestActionEnabled);
        Assert.IsFalse(viewModel.ConfigureCredentialsCommand.CanExecute(null));
    }

    [TestMethod]
    public void TestAction_RequiresTestModeAndCompleteConfiguration()
    {
        var viewModel = CreateConfiguredViewModel();

        Assert.IsFalse(viewModel.IsTestActionVisible);

        viewModel.IsTestModeEnabled = true;

        Assert.IsTrue(viewModel.IsTestActionVisible);
        Assert.IsTrue(viewModel.IsTestActionEnabled);

        viewModel.SendEmailAtEnd = true;

        Assert.IsFalse(viewModel.IsConfigurationComplete);
        Assert.IsFalse(viewModel.IsTestActionVisible);

        viewModel.IsMailConfigured = true;

        Assert.IsTrue(viewModel.IsConfigurationComplete);
        Assert.IsTrue(viewModel.IsTestActionVisible);
    }

    private static MainWindowViewModel CreateConfiguredViewModel()
    {
        return new MainWindowViewModel
        {
            AreCredentialsConfigured = true,
            IsRequestConfigured = true
        };
    }
}
