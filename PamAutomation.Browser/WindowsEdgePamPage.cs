using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using Microsoft.Extensions.Logging;
using PamAutomation.Core.Authentication;
using PamAutomation.Core.Resilience;
using PamAutomation.Core.Browser;

namespace PamAutomation.Browser;

/// <summary>Controls only the PAM login controls in a normal Microsoft Edge window.</summary>
internal sealed class WindowsEdgePamPage : IManagedPage, IPamLoginPage
{
    private static TimeSpan BrowserStartTimeout => PamOperationTimingPolicy.InteractiveBrowserStartTimeout;
    private static TimeSpan LoginControlTimeout => PamOperationTimingPolicy.InteractiveLoginControlTimeout;
    private static TimeSpan PortalResponseTimeout => PamOperationTimingPolicy.InteractivePortalResponseTimeout;
    private static readonly string[] AddressBarNames =
    [
        "Address and search bar",
        "Barra de direcciones y de búsqueda",
        "Barra de direcciones y búsqueda"
    ];
    private static readonly string[] CertificateWarningNames =
    [
        "Su conexión no es privada",
        "Su conexión no es privada.",
        "Your connection isn't private",
        "Your connection isn't private.",
        "Error de privacidad",
        "Privacy error",
        "Avanzado",
        "Advanced"
    ];
    private const string TotpRejectionMessage =
        "The provided token was rejected. Please try again or contact your administrator if you cannot login.";

    private readonly string edgeExecutablePath;
    private readonly IPamCertificateTrustPrompt certificatePrompt;
    private readonly ILogger logger;
    private readonly PamOriginPolicy originPolicy = new();
    private readonly HttpClient clockClient;
    private AutomationElement? browserWindow;
    private bool zoomAttempted;
    private bool released;

    internal bool IsZoomConfirmed { get; private set; }

    public WindowsEdgePamPage(
        string edgeExecutablePath,
        IPamCertificateTrustPrompt certificatePrompt,
        ILogger logger)
    {
        this.edgeExecutablePath = edgeExecutablePath;
        this.certificatePrompt = certificatePrompt;
        this.logger = logger;
        clockClient = CreateClockClient();
    }

    public async Task<PamNavigationResponse> NavigateAsync(Uri uri, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(released, this);
        ArgumentNullException.ThrowIfNull(uri);
        if (!originPolicy.IsAllowed(uri)) throw new PamBrowserNavigationNotAllowedException();

        HashSet<int> previousHandles = EnumerateEdgeWindows()
            .Select(static window => window.Current.NativeWindowHandle)
            .Where(static handle => handle != 0)
            .ToHashSet();
        StartNormalEdge(uri);
        browserWindow = await WaitForPamWindowAsync(previousHandles, cancellationToken).ConfigureAwait(false);
        TrySetFocus(browserWindow);
        await WaitUntilLoginCanBeAutomatedAsync(browserWindow, cancellationToken).ConfigureAwait(false);

        Uri finalUri = ReadPamUri(browserWindow) ?? uri;
        logger.LogInformation("Normal Edge reached the PAM login page and is ready for authentication.");
        return await CreateNavigationResponseAsync(finalUri, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> SubmitCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        AutomationElement window = GetWindow();
        AutomationElement usernameField = await WaitForControlAsync(
            window, "Username", ControlType.Edit, LoginControlTimeout, cancellationToken).ConfigureAwait(false);
        AutomationElement passwordField = await WaitForControlAsync(
            window, "Password", ControlType.Edit, LoginControlTimeout, cancellationToken).ConfigureAwait(false);
        SetValue(usernameField, username);
        SetValue(passwordField, password);
        AutomationElement loginButton = await WaitForControlAsync(
            window, "Log In", ControlType.Button, LoginControlTimeout, cancellationToken).ConfigureAwait(false);
        Invoke(loginButton);

        bool accepted = await WaitUntilAsync(
            () => FindControl(window, "Enter Response", ControlType.Edit) is not null || IsAuthenticated(window),
            PortalResponseTimeout,
            cancellationToken).ConfigureAwait(false);
        if (accepted)
        {
            logger.LogInformation("PAM accepted the primary credentials in normal Edge.");
            return true;
        }

        logger.LogWarning("PAM did not advance after primary credential submission.");
        return false;
    }

    public async Task<bool> SubmitTotpAsync(string code, CancellationToken cancellationToken)
    {
        AutomationElement window = GetWindow();
        AutomationElement responseField = await WaitForControlAsync(
            window, "Enter Response", ControlType.Edit, PortalResponseTimeout, cancellationToken).ConfigureAwait(false);
        AutomationElement submitButton = await WaitForControlAsync(
            window, "Submit", ControlType.Button, PortalResponseTimeout, cancellationToken).ConfigureAwait(false);
        bool rejectionWasVisible = IsTotpRejected(window);
        SetValue(responseField, code);
        Invoke(submitButton);

        bool completed = await WaitUntilAsync(
            () => IsAuthenticated(window) || (IsTotpRejected(window) && !rejectionWasVisible),
            PortalResponseTimeout,
            cancellationToken).ConfigureAwait(false);
        bool authenticated = completed && IsAuthenticated(window);
        logger.LogInformation(
            authenticated
                ? "PAM accepted the temporary code in normal Edge."
                : "PAM did not accept the temporary code in normal Edge.");
        return authenticated;
    }

    public async Task WaitForTotpRejectionToDisappearAsync(CancellationToken cancellationToken)
    {
        AutomationElement window = GetWindow();
        bool disappeared = await WaitUntilAsync(
            () => !IsTotpRejected(window),
            PortalResponseTimeout,
            cancellationToken).ConfigureAwait(false);
        if (!disappeared)
        {
            throw new InvalidOperationException("El mensaje de rechazo del código temporal no desapareció.");
        }
    }

    public async Task<bool> WaitForDashboardAsync(CancellationToken cancellationToken)
    {
        AutomationElement window = GetWindow();
        bool authenticated = await WaitUntilAsync(
            () => IsAuthenticated(window),
            PortalResponseTimeout,
            cancellationToken).ConfigureAwait(false);
        if (!authenticated || zoomAttempted) return authenticated;

        zoomAttempted = true;
        try
        {
            bool zoomConfirmed = await ApplyPamZoomAsync(window, cancellationToken).ConfigureAwait(false);
            IsZoomConfirmed = zoomConfirmed;
            if (zoomConfirmed)
            {
                logger.LogInformation("Normal Edge confirmed 67 percent zoom for the PAM page.");
            }
            else
            {
                logger.LogWarning("PAM login completed, but Edge did not confirm 67 percent zoom.");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ElementNotAvailableException)
        {
            logger.LogWarning(exception, "PAM login completed, but its Edge zoom could not be adjusted.");
        }

        return true;
    }

    public ValueTask DisposeAsync() => ReleaseAsync();

    internal ValueTask ReleaseAsync()
    {
        if (released) return ValueTask.CompletedTask;

        released = true;
        browserWindow = null;
        clockClient.Dispose();
        return ValueTask.CompletedTask;
    }

    private void StartNormalEdge(Uri uri)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = edgeExecutablePath,
            UseShellExecute = true
        };
        startInfo.ArgumentList.Add("--new-window");
        startInfo.ArgumentList.Add(uri.AbsoluteUri);
        using Process? process = Process.Start(startInfo);
        if (process is null) throw new InvalidOperationException("Microsoft Edge no pudo iniciarse.");
        logger.LogInformation("Opened PAM in the user's normal Microsoft Edge profile.");
    }

    private async Task<AutomationElement> WaitForPamWindowAsync(
        IReadOnlySet<int> previousHandles,
        CancellationToken cancellationToken)
    {
        AutomationElement? result = null;
        bool found = await WaitUntilAsync(
            () =>
            {
                IReadOnlyList<AutomationElement> windows = EnumerateEdgeWindows();
                result = windows.FirstOrDefault(window =>
                    !previousHandles.Contains(window.Current.NativeWindowHandle) && IsPamOrCertificateWindow(window));
                return result is not null;
            },
            BrowserStartTimeout,
            cancellationToken).ConfigureAwait(false);

        return found && result is not null
            ? result
            : throw new InvalidOperationException("Microsoft Edge abrió, pero no fue posible identificar la ventana de PAM.");
    }

    private async Task WaitUntilLoginCanBeAutomatedAsync(
        AutomationElement window,
        CancellationToken cancellationToken)
    {
        bool promptShown = false;
        long deadline = TimeProvider.System.GetTimestamp() +
                        (long)(LoginControlTimeout.TotalSeconds * TimeProvider.System.TimestampFrequency);
        while (TimeProvider.System.GetTimestamp() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsAuthenticated(window) || FindControl(window, "Username", ControlType.Edit) is not null) return;

            if (!promptShown && IsCertificateWarningVisible(window))
            {
                promptShown = true;
                logger.LogWarning("Edge displayed the PAM certificate warning; waiting for manual user approval.");
                await certificatePrompt.ShowAsync(cancellationToken).ConfigureAwait(false);
                TrySetFocus(window);
            }

            await Task.Delay(PamOperationTimingPolicy.ObservationInterval, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            promptShown
                ? "No apareció el inicio de sesión de PAM después de confirmar el certificado en Edge."
                : "No apareció el inicio de sesión de PAM en Microsoft Edge.");
    }

    private async Task<PamNavigationResponse> CreateNavigationResponseAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, PamAuthenticationService.LoginUri);
        using HttpResponseMessage response = await clockClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        return new PamNavigationResponse(
            uri,
            (int)response.StatusCode,
            response.Headers.Date?.ToString("R"),
            TimeSpan.Zero);
    }

    private HttpClient CreateClockClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (request, _, _, _) =>
                request.RequestUri is not null && originPolicy.IsAllowed(request.RequestUri)
        };
        return new HttpClient(handler) { Timeout = PamOperationTimingPolicy.PortalReadinessTimeout };
    }

    private AutomationElement GetWindow()
    {
        ObjectDisposedException.ThrowIf(released, this);
        return browserWindow ?? throw new InvalidOperationException("La ventana de PAM no está disponible.");
    }

    private static IReadOnlyList<AutomationElement> EnumerateEdgeWindows()
    {
        var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window);
        AutomationElementCollection windows = AutomationElement.RootElement.FindAll(TreeScope.Children, condition);
        var results = new List<AutomationElement>(windows.Count);
        for (int index = 0; index < windows.Count; index++)
        {
            AutomationElement window = windows[index];
            try
            {
                using Process process = Process.GetProcessById(window.Current.ProcessId);
                if (string.Equals(process.ProcessName, "msedge", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(window);
                }
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or ElementNotAvailableException)
            {
                // A browser window may close while the accessibility tree is being enumerated.
            }
        }

        return results;
    }

    private static bool IsPamOrCertificateWindow(AutomationElement window) =>
        ReadPamUri(window) is not null || IsCertificateWarningVisible(window);

    private static bool IsAuthenticated(AutomationElement window)
    {
        Uri? uri = ReadPamUri(window);
        return uri is not null &&
               !uri.AbsolutePath.Contains("/login", StringComparison.OrdinalIgnoreCase);
    }

    private static Uri? ReadPamUri(AutomationElement window)
    {
        try
        {
            AutomationElement? addressBar = FindAddressBar(window);
            if (addressBar?.GetCurrentPattern(ValuePattern.Pattern) is not ValuePattern valuePattern) return null;

            string address = valuePattern.Current.Value.Trim();
            if (!address.Contains("://", StringComparison.Ordinal)) address = $"https://{address}";
            return Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) && new PamOriginPolicy().IsAllowed(uri)
                ? uri
                : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ElementNotAvailableException)
        {
            return null;
        }
    }

    private static AutomationElement? FindAddressBar(AutomationElement window)
    {
        foreach (string name in AddressBarNames)
        {
            AutomationElement? control = FindControl(window, name, ControlType.Edit);
            if (control is not null) return control;
        }

        var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
        AutomationElementCollection edits = window.FindAll(TreeScope.Descendants, condition);
        for (int index = 0; index < edits.Count; index++)
        {
            AutomationElement candidate = edits[index];
            if (candidate.Current.BoundingRectangle.Top < 200) return candidate;
        }

        return null;
    }

    private static bool IsCertificateWarningVisible(AutomationElement window)
    {
        try
        {
            return CertificateWarningNames.Any(name =>
                window.FindFirst(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.NameProperty, name)) is { Current.IsOffscreen: false });
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }
    }

    private static AutomationElement? FindControl(
        AutomationElement window,
        string name,
        ControlType controlType)
    {
        try
        {
            var condition = new AndCondition(
                new PropertyCondition(AutomationElement.NameProperty, name),
                new PropertyCondition(AutomationElement.ControlTypeProperty, controlType));
            AutomationElement? control = window.FindFirst(TreeScope.Descendants, condition);
            return control is { Current.IsEnabled: true } ? control : null;
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
    }

    private static async Task<AutomationElement> WaitForControlAsync(
        AutomationElement window,
        string name,
        ControlType controlType,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        AutomationElement? result = null;
        bool found = await WaitUntilAsync(
            () => (result = FindControl(window, name, controlType)) is not null,
            timeout,
            cancellationToken).ConfigureAwait(false);
        return found && result is not null
            ? result
            : throw new InvalidOperationException($"No apareció el control '{name}' en la página de PAM.");
    }

    private static bool IsTotpRejected(AutomationElement window)
    {
        try
        {
            AutomationElement? message = window.FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.NameProperty, TotpRejectionMessage));
            return message is { Current.IsOffscreen: false };
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }
    }

    private static void SetValue(AutomationElement control, string value)
    {
        if (control.GetCurrentPattern(ValuePattern.Pattern) is not ValuePattern pattern)
        {
            throw new InvalidOperationException($"El control '{control.Current.Name}' no permite ingresar texto.");
        }
        pattern.SetValue(value);
    }

    private static void Invoke(AutomationElement control)
    {
        if (control.GetCurrentPattern(InvokePattern.Pattern) is not InvokePattern pattern)
        {
            throw new InvalidOperationException($"El control '{control.Current.Name}' no permite ejecutarse.");
        }
        pattern.Invoke();
    }

    private static void TrySetFocus(AutomationElement element)
    {
        try
        {
            element.SetFocus();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ElementNotAvailableException)
        {
            // Edge can deny programmatic focus while another modal window is active; UI Automation remains usable.
        }
    }

    private static async Task<bool> ApplyPamZoomAsync(
        AutomationElement window,
        CancellationToken cancellationToken)
    {
        if (!IsAuthenticated(window))
        {
            throw new InvalidOperationException("El zoom solo puede ajustarse en una página PAM autenticada.");
        }

        AutomationElement addressBar = FindAddressBar(window)
            ?? throw new InvalidOperationException("No se encontró la barra de direcciones de Edge para ajustar el zoom.");
        int windowHandle = window.Current.NativeWindowHandle;
        if (windowHandle == 0)
        {
            throw new InvalidOperationException("La ventana de Edge no proporcionó un identificador válido.");
        }

        NativeKeyboard.TryBringToForeground(windowHandle);
        addressBar.SetFocus();
        await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        if (!HasKeyboardFocus(addressBar))
        {
            throw new InvalidOperationException("Edge no permitió enfocar de forma segura su barra de direcciones.");
        }

        NativeKeyboard.SendControlShortcut(NativeKeyboard.VirtualKeyZero);
        await Task.Delay(150, cancellationToken).ConfigureAwait(false);
        for (int step = 0; step < 4; step++)
        {
            NativeKeyboard.SendControlShortcut(NativeKeyboard.VirtualKeySubtract);
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        return await WaitUntilAsync(
            () => IsZoom67Visible(window),
            TimeSpan.FromSeconds(3),
            cancellationToken).ConfigureAwait(false);
    }

    private static bool HasKeyboardFocus(AutomationElement expected)
    {
        try
        {
            AutomationElement focused = AutomationElement.FocusedElement;
            return expected.GetRuntimeId().SequenceEqual(focused.GetRuntimeId());
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }
    }

    private static bool IsZoom67Visible(AutomationElement window)
    {
        try
        {
            var condition = new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Group));
            AutomationElementCollection elements = window.FindAll(TreeScope.Descendants, condition);
            for (int index = 0; index < elements.Count; index++)
            {
                string name = elements[index].Current.Name;
                string compactName = string.Concat(name.Where(static character => !char.IsWhiteSpace(character)));
                if (string.Equals(compactName, "67%", StringComparison.Ordinal) ||
                    (compactName.Contains("zoom", StringComparison.OrdinalIgnoreCase) &&
                     compactName.Contains("67%", StringComparison.Ordinal)))
                {
                    return true;
                }
            }
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }

        return false;
    }

    private static async Task<bool> WaitUntilAsync(
        Func<bool> condition,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        long deadline = TimeProvider.System.GetTimestamp() +
                        (long)(timeout.TotalSeconds * TimeProvider.System.TimestampFrequency);
        while (TimeProvider.System.GetTimestamp() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (condition()) return true;
            }
            catch (ElementNotAvailableException)
            {
                // The accessibility tree is refreshed during browser navigation.
            }

            await Task.Delay(PamOperationTimingPolicy.ObservationInterval, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private static class NativeKeyboard
    {
        internal const byte VirtualKeyZero = 0x30;
        internal const byte VirtualKeySubtract = 0x6D;
        private const byte VirtualKeyControl = 0x11;
        private const uint KeyUp = 0x0002;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(nint windowHandle);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, nuint extraInfo);

        internal static void TryBringToForeground(int windowHandle) =>
            _ = SetForegroundWindow(windowHandle);

        internal static void SendControlShortcut(byte virtualKey)
        {
            keybd_event(VirtualKeyControl, 0, 0, 0);
            keybd_event(virtualKey, 0, 0, 0);
            keybd_event(virtualKey, 0, KeyUp, 0);
            keybd_event(VirtualKeyControl, 0, KeyUp, 0);
        }
    }
}
