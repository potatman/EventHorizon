using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;

namespace Insperex.EventHorizon.EventStore.Locks;

public class LockDisposable : IDisposable
{
    private readonly ICrudStore<Lock> _crudStore;
    private readonly string _id;
    private readonly TimeSpan _timeout;
    private bool _isReleased;
    private bool _ownsLock;

    public LockDisposable(ICrudStore<Lock> crudStore, string id, TimeSpan timeout)
    {
        _id = id;
        _timeout = timeout;
        _crudStore = crudStore;

        // Used for when process is stopped mid way
        AppDomain.CurrentDomain.ProcessExit += OnExit;
    }

    public void Dispose()
    {
        ReleaseAsync().Wait();
    }

    public async Task WaitForLockAsync()
    {
        do
        {
            _ownsLock = await TryLockAsync();
            if (!_ownsLock)
                await Task.Delay(200);
        } while (!_ownsLock);
    }

    public async Task<bool> TryLockAsync()
    {
        var @lock = new Lock { Id = _id, Expiration = DateTime.UtcNow.AddMilliseconds(_timeout.TotalMilliseconds) };
        var result = await _crudStore.InsertAsync(new[] { @lock }, CancellationToken.None);
        _ownsLock = result.FailedIds?.Any() != true;

        if (!_ownsLock)
        {
            var current = (await _crudStore.GetAsync(new[] { _id }, CancellationToken.None)).FirstOrDefault();
            if (current != null)
                return current.Expiration < DateTime.UtcNow;
        }

        SetTimeout();

        return _ownsLock;
    }

    public async Task<LockDisposable> ReleaseAsync()
    {
        if (_isReleased || _ownsLock != true)
            return this;

        await _crudStore.DeleteAsync(new[] { _id }, CancellationToken.None);
        _isReleased = true;
        return this;
    }

    private async void SetTimeout()
    {
        await Task.Delay(_timeout);
        await ReleaseAsync();
    }

    private void OnExit(object sender, EventArgs e)
    {
        ReleaseAsync().Wait();
    }
}