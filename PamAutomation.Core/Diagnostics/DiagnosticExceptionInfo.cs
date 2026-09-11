namespace PamAutomation.Core.Diagnostics;

/// <summary>Contains non-sensitive exception metadata suitable for support diagnostics.</summary>
/// <param name="Type">Fully qualified exception type.</param>
/// <param name="HResult">Exception HRESULT represented as hexadecimal.</param>
/// <param name="StackTrace">Stack trace without source file paths or exception messages.</param>
public sealed record DiagnosticExceptionInfo(string Type, string HResult, string? StackTrace);
