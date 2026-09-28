namespace PamAutomation.Core.Execution;

/// <summary>Session-only, atomic accumulation of complete new PAM results for one collection.</summary>
public sealed class RequestIdAccumulator
{
    public const string CollectionLockMessage = "Tienes solicitudes acumuladas de esta colección. Envía el correo o descarta el acumulado antes de seleccionar otra.";
    private readonly object sync = new();
    private readonly List<string> ids = [];
    private Guid? collectionId;
    private bool enabled;
    private AccumulatedMailState mailState;

    public bool IsEnabled { get { lock (sync) return enabled; } }
    public Guid? CollectionId { get { lock (sync) return collectionId; } }
    public int Count { get { lock (sync) return ids.Count; } }
    public AccumulatedMailState MailState { get { lock (sync) return mailState; } }
    public IReadOnlyList<string> RequestIds { get { lock (sync) return Array.AsReadOnly(ids.ToArray()); } }

    public void SetEnabled(bool value)
    {
        lock (sync)
        {
            if (!value && ids.Count > 0) throw new InvalidOperationException(CollectionLockMessage);
            enabled = value;
        }
    }

    /// <summary>Validates the entire batch before changing any session state.</summary>
    public void Add(Guid sourceCollectionId, PamExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        lock (sync)
        {
            if (!enabled || mailState != AccumulatedMailState.Ready)
                throw new InvalidOperationException("El acumulado no admite resultados durante un envío pendiente.");
            if (sourceCollectionId == Guid.Empty || (collectionId.HasValue && collectionId != sourceCollectionId))
                throw new InvalidOperationException(CollectionLockMessage);
            string[] batch = result.RequestIds.ToArray();
            if (result.Mode != PamExecutionMode.Real || result.ProcessedCount != result.FavoriteCount ||
                batch.Length != result.ProcessedCount - result.ConflictCount || result.MailDelivery is not null ||
                batch.Any(id => string.IsNullOrEmpty(id) || id.Length > 32 || !id.All(char.IsAsciiDigit)))
                throw new InvalidOperationException("Solo se pueden acumular resultados nuevos, completos y reconciliados.");
            if (batch.Length == 0) return;
            var known = ids.ToHashSet(StringComparer.Ordinal);
            string[] additions = batch.Where(known.Add).ToArray();
            ids.AddRange(additions);
            collectionId = sourceCollectionId;
        }
    }

    public void EnsureSelectionAllowed(Guid? selectedId)
    {
        lock (sync)
            if (collectionId.HasValue && selectedId != collectionId)
                throw new InvalidOperationException(CollectionLockMessage);
    }

    public void EnsureCollectionChangeAllowed(Guid id, bool deleting)
    {
        lock (sync)
            if (collectionId == id && (deleting || mailState == AccumulatedMailState.Sending))
                throw new InvalidOperationException(deleting ? CollectionLockMessage : "Espera a que termine la operación de correo.");
    }

    /// <summary>Freezes the set against additions/discard while sending or observing.</summary>
    public IReadOnlyList<string> BeginMailOperation(bool observationOnly)
    {
        lock (sync)
        {
            if (ids.Count == 0 || mailState == AccumulatedMailState.Sending ||
                (mailState == AccumulatedMailState.AwaitingConfirmation) != observationOnly)
                throw new InvalidOperationException("La operación de correo no corresponde al estado del acumulado.");
            mailState = AccumulatedMailState.Sending;
            return Array.AsReadOnly(ids.ToArray());
        }
    }

    public void CompleteMailOperation(bool confirmed, bool uncertain)
    {
        lock (sync)
        {
            if (mailState != AccumulatedMailState.Sending) throw new InvalidOperationException("No hay una operación de correo activa.");
            if (confirmed) Clear();
            else mailState = uncertain ? AccumulatedMailState.AwaitingConfirmation : AccumulatedMailState.Ready;
        }
    }

    public void Discard()
    {
        lock (sync)
        {
            if (mailState == AccumulatedMailState.Sending) throw new InvalidOperationException("Espera a que termine la operación de correo.");
            Clear();
        }
    }

    private void Clear()
    {
        ids.Clear();
        collectionId = null;
        mailState = AccumulatedMailState.Ready;
    }
}

/// <summary>Only Ready permits another batch or a new send attempt.</summary>
public enum AccumulatedMailState { Ready, Sending, AwaitingConfirmation }
