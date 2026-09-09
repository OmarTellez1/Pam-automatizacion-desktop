using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PamAutomation.Infrastructure.Credentials;

internal static class CredentialNativeMethods
{
    internal const uint GenericCredentialType = 1;
    internal const uint LocalMachinePersistence = 2;
    internal const int ErrorNotFound = 1168;
    internal const int MaximumCredentialBlobSize = 5 * 512;
    internal const int MaximumUsernameLength = 513;

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode,
        ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CredRead(
        string targetName,
        uint type,
        uint flags,
        out IntPtr credential);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode,
        ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CredWrite(
        [In] ref NativeCredential credential,
        uint flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode,
        ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CredDelete(string targetName, uint type, uint flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("Advapi32.dll", ExactSpelling = true)]
    internal static extern void CredFree(IntPtr buffer);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NativeCredential
{
    public uint Flags;
    public uint Type;

    [MarshalAs(UnmanagedType.LPWStr)]
    public string TargetName;

    [MarshalAs(UnmanagedType.LPWStr)]
    public string? Comment;

    public long LastWritten;
    public uint CredentialBlobSize;
    public IntPtr CredentialBlob;
    public uint Persist;
    public uint AttributeCount;
    public IntPtr Attributes;

    [MarshalAs(UnmanagedType.LPWStr)]
    public string? TargetAlias;

    [MarshalAs(UnmanagedType.LPWStr)]
    public string UserName;
}

internal sealed class SafeCredentialBufferHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeCredentialBufferHandle(IntPtr handle)
        : base(ownsHandle: true)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
        CredentialNativeMethods.CredFree(handle);
        return true;
    }
}
