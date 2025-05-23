using System.Diagnostics.CodeAnalysis;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("NetDaemon.Extensions.Scheduling.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
namespace NetDaemon.Extensions.Scheduler;

/// <summary>
///     Provides scheduling capability to be injected into apps
/// </summary>
/// <remarks>
///     Constructor
/// </remarks>
/// <param name="logger">Injected logger</param>
/// <param name="reactiveScheduler">Used for unit testing the scheduler</param>
internal sealed class NetDaemonScheduler(ILogger<NetDaemonScheduler>? logger = null, IScheduler? reactiveScheduler = null) : INetDaemonScheduler, IDisposable
{
    private readonly CancellationTokenSource _cancelTimers = new();
    private volatile bool _disposed;

    private static ILoggerFactory DefaultLoggerFactory => LoggerFactory.Create(builder =>
    {
        builder
            .ClearProviders()
            .AddConsole();
    });

    private readonly ILogger _logger = logger ?? DefaultLoggerFactory.CreateLogger<NetDaemonScheduler>();
    private readonly IScheduler _reactiveScheduler = reactiveScheduler ?? TaskPoolScheduler.Default;

    /// <inheritdoc/>
    public IDisposable RunEvery(TimeSpan timespan, Action action)
    {
        var cts = new CancellationTokenSource();

        Observable.Interval(timespan, _reactiveScheduler)
            .Subscribe(
                _ => RunAction(action),
                _ => _logger.LogTrace("Exiting timer using trigger with span {Span}",
                    timespan)
                , cts.Token);

        return Disposable.Create(()=> {cts.Cancel(); cts.Dispose();});
    }

    /// <inheritdoc/>
    public IDisposable RunEvery(TimeSpan period, DateTimeOffset startTime, Action action)
    {
        var cts = new CancellationTokenSource();

        Observable.Timer(
                startTime,
                period,
                _reactiveScheduler)
            .Subscribe(
                _ => RunAction(action),
                () => _logger.LogTrace("Exiting timer that was scheduled at {StartTime} and every {Period}",
                    startTime, period),
                cts.Token
            );

        return Disposable.Create(()=> {cts.Cancel(); cts.Dispose();});
    }

    /// <inheritdoc/>
    public IDisposable RunIn(TimeSpan timespan, Action action)
    {
        var cts = new CancellationTokenSource();
        Observable.Timer(timespan, _reactiveScheduler)
            .Subscribe(
                _ => RunAction(action),
                () => _logger.LogTrace("Exiting scheduled run at {Timespan}", timespan)
                , cts.Token);

        return Disposable.Create(()=> {cts.Cancel(); cts.Dispose();});
    }

    /// <inheritdoc/>
    public IDisposable RunAt(DateTimeOffset timeOffset, Action action)
    {
        var cts = new CancellationTokenSource();

        Observable.Timer(
                timeOffset,
                _reactiveScheduler)
            .Subscribe(
                _ => RunAction(action),
                () => _logger.LogTrace("Exiting timer that was scheduled at {TimeOffset}",
                    timeOffset),
               cts.Token
            );

        return Disposable.Create(()=> {cts.Cancel(); cts.Dispose();});
    }

    /// <inheritdoc/>
    public DateTimeOffset Now => _reactiveScheduler.Now;

    [SuppressMessage("", "CA1031")]
    private void RunAction(Action action)
    {
        try
        {
            if (_disposed)
               return;

            action();
        }
        catch (OperationCanceledException)
        {
            // Do nothing
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error in scheduled timer!");
        }
    }

    /// <summary>
    ///     Implements Dispose pattern
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cancelTimers.Cancel();
        _cancelTimers.Dispose();
    }
}
