using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace PamAutomation.Core.Outlook;

/// <summary>Sanitizes application-generated XHTML and composes the final Outlook message body.</summary>
public static class OutlookHtmlComposer
{
    /// <summary>The required placeholder for the current PAM request identifiers.</summary>
    public const string RequestIdsMarker = "{{REQUEST_IDS}}";

    private const int MaximumTemplateLength = 8_000_000;
    private const int MaximumRequestIdLength = 32;
    private const int MaximumAttributeLength = 1_024;
    private const string OutlookDefaultTypography = "font-family:Aptos,Calibri,'Segoe UI',sans-serif;font-size:11pt";
    private static readonly HashSet<string> AllowedElements = new(StringComparer.Ordinal)
    {
        "b", "blockquote", "br", "div", "em", "h1", "h2", "h3", "h4", "h5", "h6",
        "a", "col", "colgroup", "i", "img", "li", "ol", "p", "pre", "span", "strong", "table", "tbody", "td",
        "tfoot", "th", "thead", "tr", "u", "ul",
    };
    private static readonly HashSet<string> DangerousElements = new(StringComparer.Ordinal)
    {
        "audio", "base", "button", "embed", "form", "frame", "frameset", "head", "iframe",
        "input", "link", "math", "meta", "object", "script", "source", "style", "svg", "video",
    };
    private static readonly HashSet<string> AllowedStyleProperties = new(StringComparer.Ordinal)
    {
        "background-color", "border", "border-collapse", "border-color", "border-style",
        "border-width", "color", "font-family", "font-size", "font-style", "font-weight", "height",
        "margin", "margin-bottom", "margin-left", "margin-right", "margin-top", "padding",
        "padding-bottom", "padding-left", "padding-right", "padding-top", "text-align",
        "text-decoration", "vertical-align", "width",
    };
    private static readonly string[] ForbiddenStyleTokens =
    {
        "url(", "expression(", "@import", "behavior:", "javascript:", "vbscript:", "data:",
        "-moz-binding", "position:fixed",
    };

    /// <summary>Sanitizes a generated XHTML template and verifies its marker.</summary>
    /// <param name="htmlTemplate">The XHTML fragment produced from the native editor.</param>
    /// <returns>A deterministic allowlisted XHTML fragment containing one marker.</returns>
    /// <exception cref="OutlookHtmlCompositionException">The template is invalid or unsafe.</exception>
    public static string SanitizeTemplate(string? htmlTemplate)
    {
        XElement root = ParseAndSanitize(htmlTemplate);
        EnsureSingleMarker(root);
        return SerializeFragment(root);
    }

    /// <summary>Sanitizes an XHTML fragment without requiring the request identifier marker.</summary>
    /// <param name="htmlFragment">The untrusted fragment to sanitize.</param>
    /// <returns>A deterministic allowlisted XHTML fragment.</returns>
    /// <exception cref="OutlookHtmlCompositionException">The fragment is invalid or unsafe.</exception>
    public static string SanitizeFragment(string? htmlFragment)
    {
        XElement root = ParseAndSanitize(htmlFragment);
        return SerializeFragment(root);
    }

    /// <summary>Sanitizes a template and replaces its single marker with numeric request identifiers.</summary>
    /// <param name="htmlTemplate">The XHTML fragment produced from the native editor.</param>
    /// <param name="requestIds">The unique numeric PAM request identifiers in display order.</param>
    /// <returns>The final safe XHTML fragment with one request identifier per line.</returns>
    /// <exception cref="OutlookHtmlCompositionException">The template or identifiers are invalid.</exception>
    public static string Compose(string? htmlTemplate, IReadOnlyList<string>? requestIds)
    {
        string[] validatedRequestIds = ValidateRequestIds(requestIds);
        XElement root = ParseAndSanitize(htmlTemplate);
        XText markerText = EnsureSingleMarker(root);
        ReplaceMarker(markerText, validatedRequestIds);
        var body = new XElement(
            "div",
            new XAttribute("style", OutlookDefaultTypography),
            root.Nodes());
        return body.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>Validates that a final body is sanitized and contains every expected identifier.</summary>
    /// <param name="htmlBody">The already composed XHTML body.</param>
    /// <param name="expectedRequestIds">The numeric identifiers expected in the body.</param>
    /// <exception cref="OutlookHtmlCompositionException">The body is unsafe or incomplete.</exception>
    internal static void ValidateComposedBody(
        string htmlBody,
        IReadOnlyList<string>? expectedRequestIds)
    {
        if (expectedRequestIds is { Count: 0 })
        {
            XElement bodyRoot = ParseAndSanitize(htmlBody);
            string safeBody = SerializeFragment(bodyRoot);
            if (!string.Equals(htmlBody, safeBody, StringComparison.Ordinal) ||
                bodyRoot.DescendantNodes().OfType<XText>().Any(text => text.Value.Contains(RequestIdsMarker, StringComparison.Ordinal)))
            {
                throw new OutlookHtmlCompositionException(OutlookHtmlCompositionErrorCode.UnsafeComposedBody);
            }
            return;
        }

        string[] validatedRequestIds = ValidateRequestIds(expectedRequestIds);
        XElement root = ParseAndSanitize(htmlBody);
        string sanitized = SerializeFragment(root);
        if (!string.Equals(htmlBody, sanitized, StringComparison.Ordinal) ||
            root.DescendantNodes().OfType<XText>().Any(text =>
                text.Value.Contains(RequestIdsMarker, StringComparison.Ordinal)))
        {
            throw new OutlookHtmlCompositionException(
                OutlookHtmlCompositionErrorCode.UnsafeComposedBody);
        }

        XText[] textNodes = root.DescendantNodes().OfType<XText>().ToArray();
        if (validatedRequestIds.Any(requestId =>
            !textNodes.Any(text => ContainsNumericToken(text.Value, requestId))))
        {
            throw new OutlookHtmlCompositionException(
                OutlookHtmlCompositionErrorCode.MissingComposedRequestId);
        }
    }

    private static XElement ParseAndSanitize(string? htmlTemplate)
    {
        if (string.IsNullOrWhiteSpace(htmlTemplate))
        {
            throw new OutlookHtmlCompositionException(OutlookHtmlCompositionErrorCode.MissingTemplate);
        }

        if (htmlTemplate.Length > MaximumTemplateLength)
        {
            throw new OutlookHtmlCompositionException(OutlookHtmlCompositionErrorCode.TemplateTooLarge);
        }

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreProcessingInstructions = false,
                MaxCharactersInDocument = MaximumTemplateLength + 32,
                XmlResolver = null,
            };
            using var textReader = new StringReader("<root>" + htmlTemplate + "</root>");
            using XmlReader reader = XmlReader.Create(textReader, settings);
            XDocument document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            XElement root = document.Root
                ?? throw new OutlookHtmlCompositionException(OutlookHtmlCompositionErrorCode.InvalidMarkup);
            SanitizeTree(root);
            return root;
        }
        catch (OutlookHtmlCompositionException)
        {
            throw;
        }
        catch (XmlException)
        {
            throw new OutlookHtmlCompositionException(OutlookHtmlCompositionErrorCode.InvalidMarkup);
        }
        catch (InvalidOperationException)
        {
            throw new OutlookHtmlCompositionException(OutlookHtmlCompositionErrorCode.InvalidMarkup);
        }
    }

    private static void SanitizeTree(XElement root)
    {
        foreach (XNode node in root.DescendantNodes().ToArray())
        {
            if (node is XComment or XProcessingInstruction)
            {
                node.Remove();
            }
        }

        foreach (XElement element in root.Descendants().Reverse().ToArray())
        {
            string elementName = element.Name.LocalName.ToLowerInvariant();
            if (DangerousElements.Contains(elementName))
            {
                element.Remove();
                continue;
            }

            if (!AllowedElements.Contains(elementName))
            {
                element.ReplaceWith(element.Nodes().ToArray());
                continue;
            }

            element.Name = elementName;
            SanitizeAttributes(element, elementName);
            if (elementName == "img" && element.Attribute("src") is null)
            {
                element.Remove();
            }
        }
    }

    private static void SanitizeAttributes(XElement element, string elementName)
    {
        var safeAttributes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (XAttribute attribute in element.Attributes().ToArray())
        {
            string attributeName = attribute.Name.LocalName.ToLowerInvariant();
            string? safeValue = attribute.Name.Namespace == XNamespace.None
                ? SanitizeAttribute(elementName, attributeName, attribute.Value)
                : null;
            if (safeValue is not null)
            {
                safeAttributes.TryAdd(attributeName, safeValue);
            }
        }

        element.RemoveAttributes();
        foreach ((string name, string value) in safeAttributes)
        {
            element.SetAttributeValue(name, value);
        }
    }

    private static string? SanitizeAttribute(string elementName, string attributeName, string value)
    {
        if (value.Length > MaximumAttributeLength || ContainsUnsafeControlCharacter(value))
        {
            return null;
        }

        return attributeName switch
        {
            "style" => SanitizeStyle(value),
            "title" or "alt" => value.Trim(),
            "align" => AllowKeyword(value, "left", "center", "right", "justify"),
            "valign" => AllowKeyword(value, "top", "middle", "bottom", "baseline"),
            "dir" => AllowKeyword(value, "ltr", "rtl"),
            "width" or "height" => SanitizeDimension(value),
            "border" or "cellpadding" or "cellspacing" when elementName == "table" =>
                SanitizeInteger(value, 0, 100),
            "colspan" or "rowspan" when elementName is "td" or "th" =>
                SanitizeInteger(value, 1, 100),
            "src" when elementName == "img" => SanitizeContentId(value),
            "href" when elementName == "a" => SanitizeLink(value),
            _ => null,
        };
    }

    private static string? SanitizeStyle(string value)
    {
        var declarations = new List<string>();
        foreach (string declaration in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = declaration.IndexOf(':');
            if (separator <= 0 || separator == declaration.Length - 1)
            {
                continue;
            }

            string property = declaration[..separator].Trim().ToLowerInvariant();
            string propertyValue = declaration[(separator + 1)..].Trim();
            if (!AllowedStyleProperties.Contains(property) ||
                propertyValue.Length > 256 ||
                ContainsUnsafeControlCharacter(propertyValue) ||
                ForbiddenStyleTokens.Any(token =>
                    propertyValue.Contains(token, StringComparison.OrdinalIgnoreCase)) ||
                !propertyValue.All(IsSafeStyleCharacter))
            {
                continue;
            }

            declarations.Add(property + ":" + propertyValue);
        }

        return declarations.Count == 0 ? null : string.Join(';', declarations);
    }

    private static string? AllowKeyword(string value, params string[] allowed)
    {
        string normalized = value.Trim().ToLowerInvariant();
        return allowed.Contains(normalized, StringComparer.Ordinal) ? normalized : null;
    }

    private static string? SanitizeDimension(string value)
    {
        string normalized = value.Trim().ToLowerInvariant();
        string digits = normalized.EndsWith('%')
            ? normalized[..^1]
            : normalized.EndsWith("px", StringComparison.Ordinal)
                ? normalized[..^2]
                : normalized;
        return digits.Length is > 0 and <= 4 && digits.All(char.IsAsciiDigit)
            ? normalized
            : null;
    }

    private static string? SanitizeInteger(string value, int minimum, int maximum)
    {
        string normalized = value.Trim();
        return int.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) &&
            parsed >= minimum && parsed <= maximum
                ? parsed.ToString(CultureInfo.InvariantCulture)
                : null;
    }

    private static string? SanitizeContentId(string value)
    {
        string normalized = value.Trim();
        const string prefix = "cid:";
        if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string identifier = normalized[prefix.Length..];
        return identifier.Length is > 0 and <= 200 && identifier.All(IsContentIdCharacter)
            ? prefix + identifier
            : null;
    }

    private static string? SanitizeLink(string value)
    {
        string normalized = value.Trim();
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && !string.Equals(uri.Scheme, "mailto", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return normalized;
    }

    private static XText EnsureSingleMarker(XElement root)
    {
        XText? markerText = null;
        int markerCount = 0;
        foreach (XText text in root.DescendantNodes().OfType<XText>())
        {
            int searchFrom = 0;
            while (true)
            {
                int markerIndex = text.Value.IndexOf(RequestIdsMarker, searchFrom, StringComparison.Ordinal);
                if (markerIndex < 0)
                {
                    break;
                }

                markerCount++;
                markerText ??= text;
                searchFrom = markerIndex + RequestIdsMarker.Length;
            }
        }

        if (markerCount == 0)
        {
            throw new OutlookHtmlCompositionException(
                OutlookHtmlCompositionErrorCode.MissingRequestIdsMarker);
        }

        if (markerCount != 1)
        {
            throw new OutlookHtmlCompositionException(
                OutlookHtmlCompositionErrorCode.MultipleRequestIdsMarkers);
        }

        return markerText!;
    }

    private static string[] ValidateRequestIds(IReadOnlyList<string>? requestIds)
    {
        if (requestIds is null || requestIds.Count == 0)
        {
            throw new OutlookHtmlCompositionException(OutlookHtmlCompositionErrorCode.MissingRequestIds);
        }

        var unique = new HashSet<string>(StringComparer.Ordinal);
        var validated = new string[requestIds.Count];
        for (int index = 0; index < requestIds.Count; index++)
        {
            string? requestId = requestIds[index];
            if (string.IsNullOrEmpty(requestId) ||
                requestId.Length > MaximumRequestIdLength ||
                !requestId.All(char.IsAsciiDigit))
            {
                throw new OutlookHtmlCompositionException(OutlookHtmlCompositionErrorCode.InvalidRequestId);
            }

            if (!unique.Add(requestId))
            {
                throw new OutlookHtmlCompositionException(OutlookHtmlCompositionErrorCode.DuplicateRequestId);
            }

            validated[index] = requestId;
        }

        return validated;
    }

    private static void ReplaceMarker(XText markerText, IReadOnlyList<string> requestIds)
    {
        int markerIndex = markerText.Value.IndexOf(RequestIdsMarker, StringComparison.Ordinal);
        string prefix = markerText.Value[..markerIndex];
        string suffix = markerText.Value[(markerIndex + RequestIdsMarker.Length)..];
        var replacement = new List<XNode>(requestIds.Count * 2 + 2);
        if (prefix.Length > 0)
        {
            replacement.Add(new XText(prefix));
        }

        for (int index = 0; index < requestIds.Count; index++)
        {
            replacement.Add(new XText(requestIds[index]));
            if (index < requestIds.Count - 1)
            {
                replacement.Add(new XElement("br"));
            }
        }

        if (suffix.Length > 0)
        {
            replacement.Add(new XText(suffix));
        }

        markerText.ReplaceWith(replacement);
    }

    private static string SerializeFragment(XElement root) =>
        string.Concat(root.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting)));

    private static bool ContainsUnsafeControlCharacter(string value) =>
        value.Any(character => char.IsControl(character) && character is not '\t' and not '\r' and not '\n');

    private static bool IsSafeStyleCharacter(char character) =>
        char.IsLetterOrDigit(character) || char.IsWhiteSpace(character) ||
        "#%.,()'\"-/".Contains(character);

    private static bool IsContentIdCharacter(char character) =>
        char.IsLetterOrDigit(character) || character is '.' or '_' or '@' or '-';

    private static bool ContainsNumericToken(string value, string requestId)
    {
        int searchFrom = 0;
        while (searchFrom < value.Length)
        {
            int index = value.IndexOf(requestId, searchFrom, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            int end = index + requestId.Length;
            bool validStart = index == 0 || !char.IsAsciiDigit(value[index - 1]);
            bool validEnd = end == value.Length || !char.IsAsciiDigit(value[end]);
            if (validStart && validEnd)
            {
                return true;
            }

            searchFrom = index + 1;
        }

        return false;
    }
}
