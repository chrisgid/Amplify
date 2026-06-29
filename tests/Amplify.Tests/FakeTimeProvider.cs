namespace Amplify.Tests;

/// <summary>
/// Minimal controllable <see cref="TimeProvider"/> for tests: only <see cref="GetUtcNow"/> is
/// overridden (the code under test reads "now" from it but doesn't create timers), and
/// <see cref="Advance"/> moves the virtual clock forward.
/// </summary>
internal sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
