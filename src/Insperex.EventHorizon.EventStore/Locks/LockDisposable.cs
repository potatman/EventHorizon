using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.EventStore.Extensions;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStore.Locks;

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
            _ownsLock = await TryLockAsync().ConfigureAwait(false);
            if (!_ownsLock)
                await Task.Delay(200).ConfigureAwait(false);
        } while (!_ownsLock);

        _logger.LogInformation("Lock - Acquired lock {Name} on {Host}", _id, _hostname);
        return this;
    }

    public async Task<bool> TryLockAsync()
    {
        try
        {
            var @lock = new Lock { Id = _id, Expiration = DateTime.UtcNow.AddMilliseconds(_timeout.TotalMilliseconds), Owner = _hostname };
            var result = await _crudStore.InsertAllAsync(new[] { @lock }, CancellationToken.None).ConfigureAwait(false);
            _ownsLock = result.FailedIds?.Any() != true;
        }
        catch (Exception e)
        {
            // ignore
        }

        if (!_ownsLock)
        {
            var current = await _crudStore.GetOneAsync(_id, CancellationToken.None).ConfigureAwait(false);
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
        await _crudStore.DeleteOneAsync(_id, CancellationToken.None).ConfigureAwait(false);
        _logger.LogInformation("Lock - Released lock {Name} on {Host}", _id, Environment.MachineName);
        return this;
    }

    private async void SetTimeout()
    {
        await Task.Delay(_timeout).ConfigureAwait(false);
        await ReleaseAsync().ConfigureAwait(false);
    }

    private void OnExit(object sender, EventArgs e)
    {
        if(!_isReleased)
            ReleaseAsync().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        if(!_isReleased)
            await ReleaseAsync().ConfigureAwait(false);
    }
}
