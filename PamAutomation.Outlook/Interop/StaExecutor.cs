namespace PamAutomation.Outlook.Interop;

/// <summary>Runs bounded COM operations on a dedicated single-threaded apartment.</summary>
internal sealed class StaExecutor : IStaExecutor
{
    public Task<T> ExecuteAsync<T>(Func<T> operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        TaskCompletionSource<T> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread thread = new(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                completion.TrySetResult(operation());
            }
            catch (OperationCanceledException exception) when (exception.CancellationToken == cancellationToken)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "PamAutomation.Outlook.STA",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
