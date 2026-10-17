using PaintTheTownRedAccess;
using Xunit;

public class RepeatFilterTests
{
    private static readonly DateTime T0 = new(2026, 1, 1);
    private readonly RepeatFilter _f = new(TimeSpan.FromMilliseconds(400));

    [Fact]
    public void DropsSameTextInsideWindow()
    {
        Assert.True(_f.Allow("Options", T0));
        Assert.False(_f.Allow("Options", T0.AddMilliseconds(100)));
    }

    [Fact]
    public void AllowsSameTextAfterWindow()
    {
        _f.Allow("Options", T0);
        Assert.True(_f.Allow("Options", T0.AddMilliseconds(500)));
    }

    [Fact]
    public void AllowsDifferentText()
    {
        _f.Allow("Options", T0);
        Assert.True(_f.Allow("Quit", T0.AddMilliseconds(10)));
    }

    [Fact]
    public void ResetAllowsImmediateRepeat()
    {
        _f.Allow("Options", T0);
        _f.Reset();
        Assert.True(_f.Allow("Options", T0.AddMilliseconds(10)));
    }
}
