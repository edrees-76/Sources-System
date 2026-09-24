using System;
using System.Threading;

namespace Sources.Services;

/// <summary>
/// Static clock accessor used ONLY where constructor injection of TimeProvider is not
/// possible (entity members / property initializers in AllModels.cs). Backed by an
/// AsyncLocal override so tests can scope a fake clock without leaking across parallel
/// test classes; production code always sees TimeProvider.System unless a test has
/// pushed an override on the current async flow.
/// </summary>
public static class AppClock
{
    private static readonly AsyncLocal<TimeProvider?> _override = new();

    public static TimeProvider Current => _override.Value ?? TimeProvider.System;

    /// <summary>
    /// Overrides the clock for the current async flow. Restores the previous value when disposed.
    /// </summary>
    public static IDisposable Override(TimeProvider tp)
    {
        var previous = _override.Value;
        _override.Value = tp;
        return new OverrideScope(previous);
    }

    private sealed class OverrideScope : IDisposable
    {
        private readonly TimeProvider? _previous;
        private bool _disposed;

        public OverrideScope(TimeProvider? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _override.Value = _previous;
        }
    }
}
