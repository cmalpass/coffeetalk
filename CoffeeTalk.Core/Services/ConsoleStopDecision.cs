using System;

namespace CoffeeTalk.Services;

/// <summary>
/// Pure decision logic for whether a CLI user has requested that the conversation stop.
///
/// Kept in Core (rather than the console host) so it can be unit-tested without a real
/// terminal. The console host is responsible for guarding against redirected stdin and
/// actually reading keys; this type only decides whether a given key means "stop".
/// </summary>
public static class ConsoleStopDecision
{
    /// <summary>The character a user can press to request a stop between turns.</summary>
    public const char StopKey = 'q';

    /// <summary>An alternate key a user can press to request a stop between turns.</summary>
    public const ConsoleKey AlternateStopKey = ConsoleKey.Escape;

    /// <summary>
    /// Returns <see langword="true"/> when the given key requests a stop.
    /// Matching is case-insensitive for the character key.
    /// </summary>
    public static bool ShouldStop(char keyChar, ConsoleKey key)
        => char.ToLowerInvariant(keyChar) == char.ToLowerInvariant(StopKey)
           || key == AlternateStopKey;

    /// <summary>
    /// Returns <see langword="true"/> when the given (optional) key requests a stop.
    /// A <see langword="null"/> value (no key was read) never requests a stop.
    /// </summary>
    public static bool ShouldStop(ConsoleKeyInfo? key)
        => key.HasValue && ShouldStop(key.Value.KeyChar, key.Value.Key);
}
