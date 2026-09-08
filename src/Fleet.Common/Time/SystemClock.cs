namespace Fleet.Common.Time;

/// <summary>The production <see cref="IClock"/>: the real wall clock, in UTC.</summary>
public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
