using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    public LockDisposable(ICrudStore<Lock> crudStore, string id, TimeSpan timeout, ILogger<LockDisposable> logger)
    {
        _id = id;
        _timeout = timeout;
        _logger = logger;
        _crudStore = crudStore;

        // Used for when process is stopped mid way
        AppDomain.CurrentDomain.ProcessExit += OnExit;
        _hostname = Environment.MachineName;
    }

    public async Task<LockDisposable> WaitForLockAsync()
    {
        do
        {
            _ownsLock = await TryLockAsync();
            if (!_ownsLock)
                await Task.Delay(200);
        } while (!_ownsLock);
        return this;
    }

    public async Task<bool> TryLockAsync()
    {
        try
        {
            _logger.LogInformation("Lock - Try lock {Name} on {Host}", _id, _hostname);
            var @lock = new Lock { Id = _id, Expiration = DateTime.UtcNow.AddMilliseconds(_timeout.TotalMilliseconds), Owner = _hostname };
            var result = await _crudStore.InsertAsync(new[] { @lock }, CancellationToken.None);
            _ownsLock = result.FailedIds?.Any() != true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Lock - Failed to Lock => {Message}", e.Message);
        }

        if (!_ownsLock)
        {
            var current = (await _crudStore.GetAllAsync(new[] { _id }, CancellationToken.None)).FirstOrDefault();
            if (current != null)
                return current.Expiration < DateTime.UtcNow || current.Owner == _hostname;
            await Task.Delay(100);
        }
        _logger.LogInformation("Lock - Acquired lock {Name} on {Host}", _id, _hostname);

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
