using System;

namespace Sources.Services;

/// <summary>
/// Local-time helpers for <see cref="TimeProvider"/>. These intentionally use
/// <see cref="TimeProvider.GetLocalNow"/> (not GetUtcNow) so the machine's local
/// time zone (or a fake zone injected via TimeProvider.Testing) determines the
/// wall-clock value, matching the historical DateTime.Now / DateTime.Today behaviour.
/// Do NOT use .LocalDateTime here — it re-converts to the machine's real time zone
/// and would defeat a fake time zone under test.
/// </summary>
public static class TimeProviderExtensions
{
    public static DateTime LocalNow(this TimeProvider tp) =>
        DateTime.SpecifyKind(tp.GetLocalNow().DateTime, DateTimeKind.Local);

    public static DateTime LocalToday(this TimeProvider tp) =>
        tp.LocalNow().Date;
}
