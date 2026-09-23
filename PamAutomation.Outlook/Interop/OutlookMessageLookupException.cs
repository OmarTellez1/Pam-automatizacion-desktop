namespace PamAutomation.Outlook.Interop;

/// <summary>Preserves a failed read's location without including mailbox or message contents.</summary>
internal sealed class OutlookMessageLookupException : Exception
{
    public OutlookMessageLookupException(
        string folder, string operation, int? candidateCount, int candidateIndex, Exception innerException)
        : base($"Outlook lookup failed. Folder={folder}, Operation={operation}, CandidateCount={candidateCount}, CandidateIndex={candidateIndex}.", innerException)
    {
        Folder = folder;
        Operation = operation;
        CandidateCount = candidateCount;
        CandidateIndex = candidateIndex;
        HResult = innerException.HResult;
    }

    public string Folder { get; }
    public string Operation { get; }
    public int? CandidateCount { get; }
    public int CandidateIndex { get; }
}
