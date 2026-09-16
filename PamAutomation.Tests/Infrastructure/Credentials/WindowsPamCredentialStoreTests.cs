using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Interfaces.Credentials;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Infrastructure.Credentials;

namespace PamAutomation.Tests.Infrastructure.Credentials;

[TestClass]
[TestCategory("Unit")]
public sealed class WindowsPamCredentialStoreTests
{
    [TestMethod]
    public void HasCredentials_MissingCredential_ReturnsFalse()
    {
        // Arrange
        var store = new WindowsPamCredentialStore(new FakeWindowsCredentialApi());

        // Act
        bool result = store.HasCredentials();

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Save_CompleteUpdate_RoundTripsThroughSingleStableTarget()
    {
        // Arrange
        var credentialApi = new FakeWindowsCredentialApi();
        var store = new WindowsPamCredentialStore(credentialApi);

        // Act
        store.Save(CreateCompleteUpdate());
        PamCredentials? loaded = store.LoadForAuthentication();

        // Assert
        Assert.IsNotNull(loaded);
        Assert.AreEqual("fake-user", loaded!.Username);
        Assert.AreEqual("fake-password", loaded.Password);
        Assert.AreEqual("JBSWY3DPEHPK3PXP", loaded.TotpSeed);
        Assert.AreEqual(
            WindowsPamCredentialStore.CredentialTargetName,
            credentialApi.StoredEntry?.TargetName);
        Assert.IsNotNull(credentialApi.LastReadBuffer);
        Assert.IsTrue(credentialApi.LastReadBuffer!.All(value => value == 0));
    }

    [TestMethod]
    public void Save_ExplicitTarget_UsesOnlyThatAccountTarget()
    {
        var credentialApi = new FakeWindowsCredentialApi();
        const string target = "PamAutomation/Accounts/00000000000000000000000000000001/PamCredentials/v1";
        var store = new WindowsPamCredentialStore(credentialApi, target);

        store.Save(CreateCompleteUpdate());

        Assert.AreEqual(target, credentialApi.StoredEntry?.TargetName);
    }

    [TestMethod]
    public void Save_BlankFields_PreservesExistingProtectedValues()
    {
        // Arrange
        var credentialApi = new FakeWindowsCredentialApi();
        var store = new WindowsPamCredentialStore(credentialApi);
        store.Save(CreateCompleteUpdate());
        var update = new PamCredentialUpdate
        {
            Username = " ",
            Password = "replacement-password",
            TotpSeed = null
        };

        // Act
        store.Save(update);
        PamCredentials? loaded = store.LoadForAuthentication();

        // Assert
        Assert.IsNotNull(loaded);
        Assert.AreEqual("fake-user", loaded!.Username);
        Assert.AreEqual("replacement-password", loaded.Password);
        Assert.AreEqual("JBSWY3DPEHPK3PXP", loaded.TotpSeed);
    }

    [TestMethod]
    public void Save_IncompleteFirstUpdate_ThrowsIncompleteUpdateFailure()
    {
        // Arrange
        var credentialApi = new FakeWindowsCredentialApi();
        var store = new WindowsPamCredentialStore(credentialApi);
        var update = new PamCredentialUpdate
        {
            Username = "fake-user"
        };

        // Act
        CredentialStoreException exception = Assert.ThrowsExactly<CredentialStoreException>(
            () => store.Save(update));

        // Assert
        Assert.AreEqual(CredentialStoreErrorCode.IncompleteUpdate, exception.ErrorCode);
        Assert.IsNull(credentialApi.StoredEntry);
    }

    [TestMethod]
    public void Save_OversizedPayload_ThrowsPayloadTooLargeFailure()
    {
        // Arrange
        var credentialApi = new FakeWindowsCredentialApi();
        var store = new WindowsPamCredentialStore(credentialApi);
        PamCredentialUpdate update = CreateCompleteUpdate(password: new string('x', 2600));

        // Act
        CredentialStoreException exception = Assert.ThrowsExactly<CredentialStoreException>(
            () => store.Save(update));

        // Assert
        Assert.AreEqual(CredentialStoreErrorCode.PayloadTooLarge, exception.ErrorCode);
        Assert.IsNull(credentialApi.StoredEntry);
    }

    [TestMethod]
    public void Save_UsernameBeyondWindowsLimit_ThrowsPayloadTooLargeFailure()
    {
        // Arrange
        var credentialApi = new FakeWindowsCredentialApi();
        var store = new WindowsPamCredentialStore(credentialApi);
        var update = new PamCredentialUpdate
        {
            Username = new string('u', 514),
            Password = "fake-password",
            TotpSeed = "JBSWY3DPEHPK3PXP"
        };

        // Act
        CredentialStoreException exception = Assert.ThrowsExactly<CredentialStoreException>(
            () => store.Save(update));

        // Assert
        Assert.AreEqual(CredentialStoreErrorCode.PayloadTooLarge, exception.ErrorCode);
        Assert.IsNull(credentialApi.StoredEntry);
    }

    [TestMethod]
    public void LoadForAuthentication_CorruptedPayload_ThrowsCorruptedDataFailure()
    {
        // Arrange
        var credentialApi = new FakeWindowsCredentialApi
        {
            StoredEntry = new CredentialManagerEntry(
                WindowsPamCredentialStore.CredentialTargetName,
                "fake-user",
                [99, 1, 2, 3])
        };
        var store = new WindowsPamCredentialStore(credentialApi);

        // Act
        CredentialStoreException exception = Assert.ThrowsExactly<CredentialStoreException>(
            () => store.LoadForAuthentication());

        // Assert
        Assert.AreEqual(CredentialStoreErrorCode.CorruptedData, exception.ErrorCode);
        Assert.IsNotNull(credentialApi.LastReadBuffer);
        Assert.IsTrue(credentialApi.LastReadBuffer!.All(value => value == 0));
    }

    [TestMethod]
    public void LoadForAuthentication_WindowsReadFails_ThrowsReadFailure()
    {
        // Arrange
        var credentialApi = new FakeWindowsCredentialApi
        {
            ReadException = new Win32Exception(5)
        };
        var store = new WindowsPamCredentialStore(credentialApi);

        // Act
        CredentialStoreException exception = Assert.ThrowsExactly<CredentialStoreException>(
            () => store.LoadForAuthentication());

        // Assert
        Assert.AreEqual(CredentialStoreErrorCode.ReadFailed, exception.ErrorCode);
    }

    [TestMethod]
    public void Save_WindowsWriteFails_ZeroesManagedPayloadAndThrowsWriteFailure()
    {
        // Arrange
        var credentialApi = new FakeWindowsCredentialApi
        {
            WriteException = new Win32Exception(5)
        };
        var store = new WindowsPamCredentialStore(credentialApi);

        // Act
        CredentialStoreException exception = Assert.ThrowsExactly<CredentialStoreException>(
            () => store.Save(CreateCompleteUpdate()));

        // Assert
        Assert.AreEqual(CredentialStoreErrorCode.WriteFailed, exception.ErrorCode);
        Assert.IsNotNull(credentialApi.LastWriteBuffer);
        Assert.IsTrue(credentialApi.LastWriteBuffer!.All(value => value == 0));
    }

    [TestMethod]
    public void Delete_ExistingCredential_RemovesStableTarget()
    {
        // Arrange
        var credentialApi = new FakeWindowsCredentialApi();
        var store = new WindowsPamCredentialStore(credentialApi);
        store.Save(CreateCompleteUpdate());

        // Act
        store.Delete();

        // Assert
        Assert.IsNull(credentialApi.StoredEntry);
        Assert.AreEqual(
            WindowsPamCredentialStore.CredentialTargetName,
            credentialApi.LastDeletedTarget);
    }

    [TestMethod]
    public void Delete_WindowsDeleteFails_ThrowsDeleteFailure()
    {
        // Arrange
        var credentialApi = new FakeWindowsCredentialApi
        {
            DeleteException = new Win32Exception(5)
        };
        var store = new WindowsPamCredentialStore(credentialApi);

        // Act
        CredentialStoreException exception = Assert.ThrowsExactly<CredentialStoreException>(
            store.Delete);

        // Assert
        Assert.AreEqual(CredentialStoreErrorCode.DeleteFailed, exception.ErrorCode);
    }

    [TestMethod]
    public void NativeCredential_X64Process_HasExpectedWindowsLayout()
    {
        // Act
        int structureSize = Marshal.SizeOf<NativeCredential>();
        IntPtr credentialBlobOffset = Marshal.OffsetOf<NativeCredential>(
            nameof(NativeCredential.CredentialBlob));

        // Assert
        Assert.AreEqual(80, structureSize);
        Assert.AreEqual(new IntPtr(40), credentialBlobOffset);
    }

    private static PamCredentialUpdate CreateCompleteUpdate(
        string password = "fake-password")
    {
        return new PamCredentialUpdate
        {
            Username = "fake-user",
            Password = password,
            TotpSeed = "JBSWY3DPEHPK3PXP"
        };
    }

    private sealed class FakeWindowsCredentialApi : IWindowsCredentialApi
    {
        public CredentialManagerEntry? StoredEntry { get; set; }

        public byte[]? LastReadBuffer { get; private set; }

        public byte[]? LastWriteBuffer { get; private set; }

        public string? LastDeletedTarget { get; private set; }

        public Win32Exception? ReadException { get; init; }

        public Win32Exception? WriteException { get; init; }

        public Win32Exception? DeleteException { get; init; }

        public CredentialManagerEntry? Read(string targetName)
        {
            if (ReadException is not null)
            {
                throw ReadException;
            }

            if (StoredEntry is null ||
                !string.Equals(StoredEntry.TargetName, targetName, StringComparison.Ordinal))
            {
                return null;
            }

            LastReadBuffer = StoredEntry.CredentialBlob.ToArray();
            return new CredentialManagerEntry(
                StoredEntry.TargetName,
                StoredEntry.Username,
                LastReadBuffer);
        }

        public void Write(CredentialManagerEntry entry)
        {
            LastWriteBuffer = entry.CredentialBlob;

            if (WriteException is not null)
            {
                throw WriteException;
            }

            StoredEntry = new CredentialManagerEntry(
                entry.TargetName,
                entry.Username,
                entry.CredentialBlob.ToArray());
        }

        public void Delete(string targetName)
        {
            LastDeletedTarget = targetName;

            if (DeleteException is not null)
            {
                throw DeleteException;
            }

            StoredEntry = null;
        }
    }
}
