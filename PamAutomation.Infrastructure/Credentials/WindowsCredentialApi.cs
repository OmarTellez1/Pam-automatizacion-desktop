using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace PamAutomation.Infrastructure.Credentials;

internal sealed class WindowsCredentialApi : IWindowsCredentialApi
{
    public CredentialManagerEntry? Read(string targetName)
    {
        if (!CredentialNativeMethods.CredRead(
                targetName,
                CredentialNativeMethods.GenericCredentialType,
                flags: 0,
                out IntPtr credentialPointer))
        {
            int error = Marshal.GetLastWin32Error();
            return error == CredentialNativeMethods.ErrorNotFound
                ? null
                : throw new Win32Exception(error);
        }

        using var credentialHandle = new SafeCredentialBufferHandle(credentialPointer);
        NativeCredential credential = Marshal.PtrToStructure<NativeCredential>(
            credentialHandle.DangerousGetHandle());

        if (credential.CredentialBlobSize > CredentialNativeMethods.MaximumCredentialBlobSize ||
            (credential.CredentialBlobSize > 0 && credential.CredentialBlob == IntPtr.Zero))
        {
            throw new InvalidDataException("Windows devolvió una credencial no válida.");
        }

        byte[] credentialBlob = new byte[(int)credential.CredentialBlobSize];
        try
        {
            if (credentialBlob.Length > 0)
            {
                Marshal.Copy(
                    credential.CredentialBlob,
                    credentialBlob,
                    startIndex: 0,
                    length: credentialBlob.Length);
            }

            return new CredentialManagerEntry(
                targetName,
                credential.UserName ?? string.Empty,
                credentialBlob);
        }
        finally
        {
            ZeroUnmanagedMemory(credential.CredentialBlob, credentialBlob.Length);
        }
    }

    public void Write(CredentialManagerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.CredentialBlob.Length > CredentialNativeMethods.MaximumCredentialBlobSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(entry),
                "La credencial excede el tamaño permitido por Windows.");
        }

        IntPtr credentialBlobPointer = Marshal.AllocHGlobal(entry.CredentialBlob.Length);

        try
        {
            Marshal.Copy(
                entry.CredentialBlob,
                startIndex: 0,
                credentialBlobPointer,
                length: entry.CredentialBlob.Length);

            var credential = new NativeCredential
            {
                Flags = 0,
                Type = CredentialNativeMethods.GenericCredentialType,
                TargetName = entry.TargetName,
                Comment = "Pam Automation protected PAM credential",
                LastWritten = 0,
                CredentialBlobSize = (uint)entry.CredentialBlob.Length,
                CredentialBlob = credentialBlobPointer,
                Persist = CredentialNativeMethods.LocalMachinePersistence,
                AttributeCount = 0,
                Attributes = IntPtr.Zero,
                TargetAlias = null,
                UserName = entry.Username
            };

            if (!CredentialNativeMethods.CredWrite(ref credential, flags: 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            ZeroUnmanagedMemory(credentialBlobPointer, entry.CredentialBlob.Length);
            Marshal.FreeHGlobal(credentialBlobPointer);
        }
    }

    public void Delete(string targetName)
    {
        if (CredentialNativeMethods.CredDelete(
                targetName,
                CredentialNativeMethods.GenericCredentialType,
                flags: 0))
        {
            return;
        }

        int error = Marshal.GetLastWin32Error();
        if (error != CredentialNativeMethods.ErrorNotFound)
        {
            throw new Win32Exception(error);
        }
    }

    private static void ZeroUnmanagedMemory(IntPtr pointer, int length)
    {
        if (pointer == IntPtr.Zero || length == 0)
        {
            return;
        }

        byte[] zeros = new byte[length];
        try
        {
            Marshal.Copy(zeros, startIndex: 0, pointer, length);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(zeros);
        }
    }
}
