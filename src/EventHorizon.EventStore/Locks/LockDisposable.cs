using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;
using Microsoft.Extensions.Logging;

namespace EventHorizon.EventStore.Locks;

public class LockDisposable : IAsyncDisposable
{
    private readonly ICrudStore<Lock> _crudStore;
    private readonly string _id;
    private readonly TimeSpan _timeout;
    private readonly ILogger<LockDisposable> _logger;
    private bool _isReleased;
    private bool _ownsLock;
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

    public async Task<LockDisposable> WaitForLockAsync()
    {
        _logger.LogInformation("Lock - Try lock {Name} on {Host}", _id, _hostname);

        do
        {
            _ownsLock = await TryLockAsync();
            if (!_ownsLock)
                await Task.Delay(200);
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
        catch (Exception e)
        {
            // ignore
        }

        if (!_ownsLock)
        {
            var current = (await _crudStore.GetAllAsync(new[] { _id }, CancellationToken.None)).FirstOrDefault();
            if (current != null)
                return current.Expiration < DateTime.UtcNow || current.Owner == _hostname;
        }

        SetTimeout();

        return _ownsLock;
    }

    public async Task<LockDisposable> ReleaseAsync()
    {
        if (_isReleased || _ownsLock != true)
            return this;

        _isReleased = true;
        await _crudStore.DeleteAsync(new[] { _id }, CancellationToken.None);
        _logger.LogInformation("Lock - Released lock {Name} on {Host}", _id, Environment.MachineName);
        return this;
    }

    private async void SetTimeout()
    {
        await Task.Delay(_timeout);
        await ReleaseAsync();
    }

    private void OnExit(object sender, EventArgs e)
    {
        if(!_isReleased)
            ReleaseAsync().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        if(!_isReleased)
            await ReleaseAsync();
    }
}
