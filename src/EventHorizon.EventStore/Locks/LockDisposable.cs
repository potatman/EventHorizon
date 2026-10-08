using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;
using Microsoft.Extensions.Logging;

// Alias to disambiguate from System.Threading.Lock introduced in .NET 9+
using Lock = EventHorizon.EventStore.Models.Lock;

namespace EventHorizon.EventStore.Locks;

public class LockDisposable : IAsyncDisposable
{
    private readonly ICrudStore<Lock> _crudStore;
    private readonly string _id;
    private readonly TimeSpan _timeout;
    private readonly ILogger<LockDisposable> _logger;
    private bool _isReleased;
    private bool _ownsLock;
    private bool _isTimeoutStarted;
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(2);
    private readonly string _hostname;

    public LockDisposable(ICrudStore<Lock> crudStore, string id, string hostname, TimeSpan timeout, ILogger<LockDisposable> logger)
    {
        _id = id;
        _hostname = hostname;
        _timeout = timeout;
        _logger = logger;
        _crudStore = crudStore;

        // Used for when process is stopped mid way
        AppDomain.CurrentDomain.ProcessExit += OnExit;
    }

    public Task<LockDisposable> WaitForLockAsync()
    {
        return WaitForLockAsync(CancellationToken.None);
    }

    public async Task<LockDisposable> WaitForLockAsync(CancellationToken ct)
    {
        _logger.LogInformation("Lock - Try lock {Name} on {Host}", _id, _hostname);

        do
        {
            _ownsLock = await TryLockAsync();
            if (!_ownsLock)
                await Task.Delay(200, ct);
        } while (!_ownsLock);

        _logger.LogInformation("Lock - Acquired lock {Name} on {Host}", _id, _hostname);
        return this;
    }

    public async Task<bool> TryLockAsync()
    {
        try
        {
            var @lock = new Lock { Id = _id, Expiration = DateTime.UtcNow.AddMilliseconds(_timeout.TotalMilliseconds), Owner = _hostname };
            var result = await _crudStore.InsertAsync(new[] { @lock }, CancellationToken.None);
            _ownsLock = result.FailedIds?.Any() != true;
        }
        catch (Exception)
        {
            // ignore
        }

        if (!_ownsLock)
        {
            var current = (await _crudStore.GetAllAsync(new[] { _id }, CancellationToken.None)).FirstOrDefault();
            _ownsLock = current is not null && (current.Expiration < DateTime.UtcNow || current.Owner == _hostname);
        }

        // Only an acquired lock gets an expiry timer; failed attempts must not schedule a release.
        if (_ownsLock)
            _ = ExpireAsync();

        return _ownsLock;
    }

    public async Task<LockDisposable> ReleaseAsync()
    {
        if (_isReleased || _ownsLock != true)
            return this;

        _isReleased = true;
        AppDomain.CurrentDomain.ProcessExit -= OnExit;
        await _crudStore.DeleteAsync(new[] { _id }, CancellationToken.None);
        _logger.LogInformation("Lock - Released lock {Name} on {Host}", _id, Environment.MachineName);
        return this;
    }

    // Fire-and-forget: exceptions are logged here, since nothing awaits this task.
    private async Task ExpireAsync()
    {
        if (_isTimeoutStarted)
            return;

        _isTimeoutStarted = true;
        try
        {
            await Task.Delay(_timeout);
            await ReleaseAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lock - Failed to release expired lock {Name} on {Host}", _id, _hostname);
        }
    }

    private void OnExit(object sender, EventArgs e)
    {
        // ProcessExit handlers are synchronous and time-limited, so wait briefly and never throw.
        if (_isReleased)
            return;

        try
        {
            ReleaseAsync().Wait(ExitTimeout);
        }
        catch (Exception)
        {
            // Best effort: the lock expires on its own.
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Unsubscribe even when the lock was never acquired, so the ProcessExit handler does not keep this instance alive.
        AppDomain.CurrentDomain.ProcessExit -= OnExit;
        if(!_isReleased)
            await ReleaseAsync();
    }
}
