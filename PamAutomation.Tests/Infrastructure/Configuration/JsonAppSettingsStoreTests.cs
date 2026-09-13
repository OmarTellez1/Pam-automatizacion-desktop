using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Infrastructure.Configuration;

namespace PamAutomation.Tests.Infrastructure.Configuration;

[TestClass]
[TestCategory("Unit")]
public sealed class JsonAppSettingsStoreTests : IDisposable
{
    private readonly string testDirectory = Path.Combine(
        Path.GetTempPath(),
        $"PamAutomation.Tests.{Guid.NewGuid():N}");

    [TestMethod]
    public async Task LoadAsync_MissingFile_ReturnsDefaultsWithoutCreatingStorage()
    {
        // Arrange
        var store = new JsonAppSettingsStore(testDirectory);

        // Act
        AppSettings settings = await store.LoadAsync();

        // Assert
        Assert.AreEqual(new AppSettings(), settings);
        Assert.IsFalse(Directory.Exists(testDirectory));
    }

    [TestMethod]
    public async Task SaveAsync_ValidSettings_WritesFlatVersionedJson()
    {
        // Arrange
        var store = new JsonAppSettingsStore(testDirectory);
        AppSettings settings = CreateSettings();

        // Act
        await store.SaveAsync(settings);

        // Assert
        string filePath = GetSettingsFilePath();
        Assert.IsTrue(File.Exists(filePath));

        using JsonDocument json = JsonDocument.Parse(await File.ReadAllTextAsync(filePath));
        JsonElement root = json.RootElement;

        Assert.AreEqual(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual("sendEmail", root.GetProperty("finalAction").GetString());
        Assert.AreEqual("Acceso de mantenimiento", root.GetProperty("requestReason").GetString());
        Assert.AreEqual(8, root.GetProperty("sessionHours").GetInt32());
        Assert.AreEqual("2026-09-10", root.GetProperty("startDate").GetString());
        Assert.AreEqual("14:30:00", root.GetProperty("startTime").GetString());
        Assert.AreEqual("America/Guayaquil", root.GetProperty("timeZone").GetString());
        Assert.IsTrue(root.GetProperty("testModeEnabled").GetBoolean());
        Assert.IsFalse(root.TryGetProperty("request", out _));
    }

    [TestMethod]
    public async Task LoadAsync_PreviouslySavedSettings_ReturnsEquivalentDomainModel()
    {
        // Arrange
        var store = new JsonAppSettingsStore(testDirectory);
        AppSettings expected = CreateSettings();
        await store.SaveAsync(expected);

        // Act
        AppSettings actual = await store.LoadAsync();

        // Assert
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task SaveAsync_ExistingSettings_ReplacesFileWithoutLeavingTemporaryFiles()
    {
        // Arrange
        var store = new JsonAppSettingsStore(testDirectory);
        await store.SaveAsync(CreateSettings());
        AppSettings replacement = CreateSettings() with
        {
            FinalAction = FinalAction.DisplayRequestIds,
            TestModeEnabled = false
        };

        // Act
        await store.SaveAsync(replacement);

        // Assert
        Assert.AreEqual(replacement, await store.LoadAsync());
        Assert.IsEmpty(Directory.EnumerateFiles(testDirectory, "*.tmp"));
    }

    [TestMethod]
    public async Task SaveAsync_CanceledReplacement_PreservesExistingSettingsAndCleansTemporaryFile()
    {
        // Arrange
        var store = new JsonAppSettingsStore(testDirectory);
        AppSettings original = CreateSettings();
        await store.SaveAsync(original);
        AppSettings replacement = original with
        {
            TestModeEnabled = false
        };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.SaveAsync(replacement, cancellation.Token));

        // Assert
        Assert.AreEqual(original, await store.LoadAsync());
        Assert.IsEmpty(Directory.EnumerateFiles(testDirectory, "*.tmp"));
    }

    [TestMethod]
    public async Task LoadAsync_MalformedJson_ThrowsInvalidContentFailure()
    {
        // Arrange
        Directory.CreateDirectory(testDirectory);
        await File.WriteAllTextAsync(GetSettingsFilePath(), "{ invalid-json }");
        var store = new JsonAppSettingsStore(testDirectory);

        // Act
        AppSettingsPersistenceException exception =
            await Assert.ThrowsExactlyAsync<AppSettingsPersistenceException>(
                () => store.LoadAsync());

        // Assert
        Assert.AreEqual(AppSettingsPersistenceErrorCode.InvalidContent, exception.ErrorCode);
    }

    [TestMethod]
    public async Task LoadAsync_UnsupportedSchema_ThrowsUnsupportedSchemaFailure()
    {
        // Arrange
        Directory.CreateDirectory(testDirectory);
        string json = """
            {
              "schemaVersion": 99,
              "finalAction": "displayRequestIds",
              "requestReason": "Prueba",
              "sessionHours": 1,
              "startDate": "2026-09-10",
              "startTime": "14:30:00",
              "timeZone": "America/Guayaquil",
              "testModeEnabled": false
            }
            """;
        await File.WriteAllTextAsync(GetSettingsFilePath(), json);
        var store = new JsonAppSettingsStore(testDirectory);

        // Act
        AppSettingsPersistenceException exception =
            await Assert.ThrowsExactlyAsync<AppSettingsPersistenceException>(
                () => store.LoadAsync());

        // Assert
        Assert.AreEqual(AppSettingsPersistenceErrorCode.UnsupportedSchema, exception.ErrorCode);
    }

    [TestMethod]
    public async Task LoadAsync_UnknownProperty_ThrowsInvalidContentFailure()
    {
        // Arrange
        Directory.CreateDirectory(testDirectory);
        string json = """
            {
              "schemaVersion": 1,
              "finalAction": "displayRequestIds",
              "requestReason": "Prueba",
              "sessionHours": 1,
              "startDate": "2026-09-10",
              "startTime": "14:30:00",
              "timeZone": "America/Guayaquil",
              "testModeEnabled": false,
              "unexpected": true
            }
            """;
        await File.WriteAllTextAsync(GetSettingsFilePath(), json);
        var store = new JsonAppSettingsStore(testDirectory);

        // Act
        AppSettingsPersistenceException exception =
            await Assert.ThrowsExactlyAsync<AppSettingsPersistenceException>(
                () => store.LoadAsync());

        // Assert
        Assert.AreEqual(AppSettingsPersistenceErrorCode.InvalidContent, exception.ErrorCode);
    }

    [TestMethod]
    public async Task SaveAsync_UnsupportedSchema_DoesNotCreateConfigurationFile()
    {
        // Arrange
        var store = new JsonAppSettingsStore(testDirectory);
        AppSettings settings = CreateSettings() with
        {
            SchemaVersion = 2
        };

        // Act
        AppSettingsPersistenceException exception =
            await Assert.ThrowsExactlyAsync<AppSettingsPersistenceException>(
                () => store.SaveAsync(settings));

        // Assert
        Assert.AreEqual(AppSettingsPersistenceErrorCode.UnsupportedSchema, exception.ErrorCode);
        Assert.IsFalse(File.Exists(GetSettingsFilePath()));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("relative-path")]
    public void Constructor_InvalidDirectory_ThrowsArgumentException(string directory)
    {
        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new JsonAppSettingsStore(directory));

        // Assert
        Assert.AreEqual("configurationDirectory", exception.ParamName);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    private string GetSettingsFilePath()
    {
        return Path.Combine(testDirectory, JsonAppSettingsStore.SettingsFileName);
    }

    private static AppSettings CreateSettings()
    {
        return new AppSettings
        {
            FinalAction = FinalAction.SendEmail,
            Request = new RequestSettings
            {
                RequestReason = "Acceso de mantenimiento",
                SessionHours = 8,
                StartDate = new DateOnly(2026, 9, 10),
                StartTime = new TimeOnly(14, 30)
            },
            TestModeEnabled = true
        };
    }
}
