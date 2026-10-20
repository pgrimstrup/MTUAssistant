using MTU.TravellerMap;

namespace MTUAssistant.Services;

public enum TileRefreshStatus { Idle, Running, Completed, Cancelled, Faulted }

/// <summary>Per-circuit view model that runs the tile refresh and exposes progress to the UI.</summary>
public class TileRefreshService(TileRefreshWorker worker)
{
    private CancellationTokenSource? _cts;

    public event Action? Changed;

    public bool IsDialogOpen { get; private set; }
    public TileRefreshStatus Status { get; private set; }
    public TileRefreshProgress Progress { get; private set; } = new(0, 0, 0, null, null);
    public string? Error { get; private set; }
    public string Folder => TileRefreshWorker.DefaultRootFolder;

    public double Percent => Progress.Total == 0 ? 0 : 100.0 * Progress.Completed / Progress.Total;

    public async Task StartAsync()
    {
        if (Status == TileRefreshStatus.Running)
        {
            IsDialogOpen = true;
            Changed?.Invoke();
            return;
        }

        _cts = new CancellationTokenSource();
        Status = TileRefreshStatus.Running;
        Progress = new(0, 0, 0, null, null);
        Error = null;
        IsDialogOpen = true;
        Changed?.Invoke();

        try
        {
            await worker.RunAsync(new SynchronousProgress<TileRefreshProgress>(p =>
            {
                Progress = p;
                Changed?.Invoke();
            }), cancellationToken: _cts.Token);
            Status = TileRefreshStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            Status = TileRefreshStatus.Cancelled;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Status = TileRefreshStatus.Faulted;
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            Changed?.Invoke();
        }
    }

    public void Cancel() => _cts?.Cancel();

    public void CloseDialog()
    {
        // Closing while running hides the dialog but lets the download continue.
        IsDialogOpen = false;
        Changed?.Invoke();
    }

    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
