using System;
using System.Timers;

namespace EventHorizon.Abstractions.Util;

/// <summary>
/// Timer class that restarts its timer only when the call to Check() returns true.
/// </summary>
internal class OnCheckTimer
{
    private readonly Timer _timer;
    private bool _ready;
    private object _lockObject = new object();

    public OnCheckTimer(TimeSpan interval)
    {
        _timer = new(interval);
        _timer.Elapsed += OnTimerElapsed;
        _timer.Start();
    }

    private void OnTimerElapsed(object state, ElapsedEventArgs elapsedEventArgs) {
        lock (_lockObject)
        {
            _ready = true;
            _timer.Stop();
        }
    }

    public bool Check()
    {
        lock (_lockObject)
        {
            if (!_ready) return false;
            _ready = false;
            _timer.Start();
            return true;
        }
    }
}
