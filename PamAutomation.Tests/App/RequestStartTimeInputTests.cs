using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.App.Services;

namespace PamAutomation.Tests.App;

[TestClass]
public sealed class RequestStartTimeInputTests
{
    [TestMethod]
    [DataRow("12", "00", "AM", 0, 0)]
    [DataRow("12", "59", "PM", 12, 59)]
    [DataRow("01", "5", "PM", 13, 5)]
    [DataRow("11", "09", "AM", 11, 9)]
    public void TryParse_ValidSeparatedTime_ReturnsTwentyFourHourTime(
        string hour,
        string minute,
        string meridiem,
        int expectedHour,
        int expectedMinute)
    {
        RequestStartTimeInputError error = RequestStartTimeInput.TryParse(
            hour,
            minute,
            meridiem,
            out TimeOnly result);

        Assert.AreEqual(RequestStartTimeInputError.None, error);
        Assert.AreEqual(new TimeOnly(expectedHour, expectedMinute), result);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("-1")]
    [DataRow("60")]
    [DataRow("99")]
    [DataRow("texto")]
    public void TryParse_InvalidMinute_ReturnsMinuteError(string minute)
    {
        RequestStartTimeInputError error = RequestStartTimeInput.TryParse(
            "09",
            minute,
            "AM",
            out TimeOnly result);

        Assert.AreEqual(RequestStartTimeInputError.InvalidMinute, error);
        Assert.AreEqual(default, result);
    }

    [TestMethod]
    public void TryParse_InvalidHourOrMeridiem_ReturnsSpecificError()
    {
        RequestStartTimeInputError hourError = RequestStartTimeInput.TryParse(
            "13",
            "00",
            "AM",
            out _);
        RequestStartTimeInputError meridiemError = RequestStartTimeInput.TryParse(
            "09",
            "00",
            null,
            out _);

        Assert.AreEqual(RequestStartTimeInputError.InvalidHour, hourError);
        Assert.AreEqual(RequestStartTimeInputError.InvalidMeridiem, meridiemError);
    }
}
