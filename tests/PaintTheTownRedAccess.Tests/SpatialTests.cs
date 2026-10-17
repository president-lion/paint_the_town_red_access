using PaintTheTownRedAccess;
using Xunit;

public class SpatialTests
{
    [Theory]
    [InlineData(0, 5, "ahead")]
    [InlineData(5, 5, "ahead right")]
    [InlineData(5, 0, "right")]
    [InlineData(5, -5, "behind right")]
    [InlineData(0, -5, "behind")]
    [InlineData(-5, -5, "behind left")]
    [InlineData(-5, 0, "left")]
    [InlineData(-5, 5, "ahead left")]
    [InlineData(1, 10, "ahead")]
    public void Direction(float right, float forward, string expected) =>
        Assert.Equal(expected, Spatial.Direction(right, forward));

    [Theory]
    [InlineData(0.4f, "close")]
    [InlineData(1.2f, "1 metre")]
    [InlineData(3.6f, "4 metres")]
    public void Distance(float m, string expected) => Assert.Equal(expected, Spatial.Distance(m));

    [Fact]
    public void PanFollowsSide()
    {
        Assert.True(Spatial.Place(5, 0, 20).Pan > 0.99f);
        Assert.True(Spatial.Place(-5, 0, 20).Pan < -0.99f);
        Assert.Equal(0f, Spatial.Place(0, 5, 20).Pan, 3);
    }

    [Fact]
    public void BehindIsLowerAndQuieter()
    {
        var ahead = Spatial.Place(0, 5, 20);
        var behind = Spatial.Place(0, -5, 20);
        Assert.Equal(1f, ahead.Pitch, 3);
        Assert.True(behind.Pitch < 0.8f);
        Assert.True(behind.Volume < ahead.Volume);
    }

    [Fact]
    public void FartherIsQuieterAndSlower()
    {
        Assert.True(Spatial.Place(0, 2, 20).Volume > Spatial.Place(0, 15, 20).Volume);
        Assert.True(Spatial.PingInterval(2, 20) < Spatial.PingInterval(15, 20));
        Assert.True(Spatial.Place(0, 50, 20).Volume >= 0.2f);
    }

    [Fact]
    public void WavHeader()
    {
        var wav = WavSynth.ToWav(WavSynth.Tone(440, 440, 0.1f));
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal(44 + (int)(WavSynth.Rate * 0.1f) * 2, wav.Length);
    }
}
