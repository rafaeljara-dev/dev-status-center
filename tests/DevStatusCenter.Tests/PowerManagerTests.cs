using DevStatusCenter.Application.Power;
using DevStatusCenter.Domain.Enums;

namespace DevStatusCenter.Tests;

public sealed class PowerManagerTests
{
    [Theory]
    [InlineData(PowerMode.Normal, true)]
    [InlineData(PowerMode.Eco, true)]
    [InlineData(PowerMode.Paused, false)]
    [InlineData(PowerMode.Gaming, false)]
    public void AllowsBackgroundActivity_MatchesMode(PowerMode mode, bool expected)
    {
        var manager = new PowerManager(mode);

        Assert.Equal(expected, manager.AllowsBackgroundActivity);
    }

    [Fact]
    public void SetMode_RaisesOnlyWhenChanged()
    {
        var manager = new PowerManager();
        var events = 0;
        manager.ModeChanged += (_, _) => events++;

        manager.SetMode(PowerMode.Normal);
        manager.SetMode(PowerMode.Eco);

        Assert.Equal(1, events);
    }
}

