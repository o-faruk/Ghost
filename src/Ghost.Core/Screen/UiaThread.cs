using System.Threading.Channels;

namespace Ghost.Core.Screen;

/// <summary>
/// A single dedicated background thread in the MTA COM apartment. UIA is COM; calling it from
/// the WPF UI thread (STA) will deadlock or freeze the overlay animation. Every FlaUI/UIA call
/// in the codebase must be marshalled onto this thread via <see cref="RunAsync{T}"/> — nothing
/// else touches a FlaUI object directly. Registering or removing UIA event handlers from
/// multiple threads causes undefined behavior per the UIA docs, so this is also the only place
/// that ever does so.
/// </summary>
public sealed class UiaThread : IDisposable
{
    private readonly Channel<WorkItem> _channel = Channel.CreateUnbounded<WorkItem>(
        new UnboundedChannelOptions { SingleReader = true });

    private readonly Thread _thread;

    public UiaThread()
    {
        _thread = new Thread(RunLoop) { IsBackground = true, Name = "Ghost-UIA" };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }

    /// <summary>Runs <paramref name="work"/> on the UIA thread and awaits its result from the caller's thread.</summary>
    public Task<T> RunAsync<T>(Func<T> work, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.CanBeCanceled ? ct.Register(() => tcs.TrySetCanceled(ct)) : default;

        var item = new WorkItem(() =>
        {
            using var _ = registration;
            if (ct.IsCancellationRequested)
            {
                tcs.TrySetCanceled(ct);
                return;
            }

            try
            {
                tcs.TrySetResult(work());
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        if (!_channel.Writer.TryWrite(item))
        {
            tcs.TrySetException(new ObjectDisposedException(nameof(UiaThread)));
        }

        return tcs.Task;
    }

    private void RunLoop()
    {
        var reader = _channel.Reader;
        try
        {
            while (true)
            {
                var item = reader.ReadAsync().AsTask().GetAwaiter().GetResult();
                item.Execute();
            }
        }
        catch (ChannelClosedException)
        {
        }
    }

    public void Dispose()
    {
        _channel.Writer.TryComplete();
    }

    private readonly record struct WorkItem(Action Execute);
}
