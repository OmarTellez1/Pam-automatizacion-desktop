using System.Diagnostics;

namespace PamAutomation.Core.Outlook;

[DebuggerDisplay("Outlook inline image (redacted)")]
public sealed record OutlookInlineImage
{
    private readonly byte[] data;
    public OutlookInlineImage(string contentId, string fileName, string contentType, byte[] data)
    {
        ContentId = contentId;
        FileName = fileName;
        ContentType = contentType;
        this.data = data?.ToArray() ?? throw new ArgumentNullException(nameof(data));
    }

    public string ContentId { get; }
    public string FileName { get; }
    public string ContentType { get; }
    public byte[] Data => data.ToArray();
    public override string ToString() => "Outlook inline image (redacted)";
}
