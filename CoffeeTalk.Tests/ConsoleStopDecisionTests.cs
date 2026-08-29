using System;
using CoffeeTalk.Services;

namespace CoffeeTalk.Tests;

public class ConsoleStopDecisionTests
{
    [Theory]
    [InlineData('q', ConsoleKey.Q)]
    [InlineData('Q', ConsoleKey.Q)]
    public void ShouldStop_ReturnsTrue_ForStopKeyRegardlessOfCase(char keyChar, ConsoleKey key)
    {
        Assert.True(ConsoleStopDecision.ShouldStop(keyChar, key));
    }

    [Theory]
    [InlineData('z', ConsoleKey.Z)]
    [InlineData(' ', ConsoleKey.Spacebar)]
    [InlineData('a', ConsoleKey.A)]
    public void ShouldStop_ReturnsFalse_ForOtherKeys(char keyChar, ConsoleKey key)
    {
        Assert.False(ConsoleStopDecision.ShouldStop(keyChar, key));
    }

    [Fact]
    public void ShouldStop_ReturnsTrue_ForEscapeKey()
    {
        Assert.True(ConsoleStopDecision.ShouldStop('\0', ConsoleKey.Escape));
    }

    [Fact]
    public void ShouldStop_ReturnsFalse_WhenNoKeyRead()
    {
        Assert.False(ConsoleStopDecision.ShouldStop(null));
    }

    [Fact]
    public void ShouldStop_ReturnsTrue_ForNullCharKeyWithEscapeKeyInfo()
    {
        var key = new ConsoleKeyInfo('\0', ConsoleKey.Escape, false, false, false);
        Assert.True(ConsoleStopDecision.ShouldStop(key));
    }

    [Fact]
    public void StopKey_IsLowerCaseQ()
    {
        Assert.Equal('q', ConsoleStopDecision.StopKey);
    }

    [Fact]
    public void AlternateStopKey_IsEscape()
    {
        Assert.Equal(ConsoleKey.Escape, ConsoleStopDecision.AlternateStopKey);
    }
}
