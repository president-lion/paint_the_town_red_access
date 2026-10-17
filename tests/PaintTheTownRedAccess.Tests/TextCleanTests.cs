using PaintTheTownRedAccess;
using Xunit;

public class TextCleanTests
{
    [Theory]
    [InlineData("SINGLEPLAYER", "singleplayer")]
    [InlineData("GAME & INPUT", "game & input")]
    [InlineData("1.3.4     r5682", "1.3.4 r5682")]
    [InlineData("Open in Workshop", "Open in Workshop")]
    [InlineData("<color=#ff0000>RANK:</color> ENSIGN", "rank: ensign")]
    [InlineData("0/21 Shard Upgrades", "0/21 Shard Upgrades")]
    [InlineData("  line\none ", "line one")]
    [InlineData("A", "A")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Clean(string input, string expected) => Assert.Equal(expected, TextClean.Clean(input));

    [Theory]
    [InlineData("ButtonStandard-ControlRemapper", "control remapper")]
    [InlineData("ButtonToggle-Punching Only", "punching only")]
    [InlineData("ButtonMain-PlayerCreatedLevels", "player created levels")]
    [InlineData("Panel-MainMenu", "main menu")]
    [InlineData("ButtonToggle-SUPERHOT", "superhot")]
    [InlineData("ButtonMain-Scenarios (1)", "scenarios")]
    [InlineData("Dropdown", "dropdown")]
    [InlineData("HUDOpacity", "hud opacity")]
    public void FromObjectName(string input, string expected) => Assert.Equal(expected, TextClean.FromObjectName(input));

    [Theory]
    [InlineData("MOUSE SENSITIVITY (1.0)", "MOUSE SENSITIVITY", "1.0")]
    [InlineData("FIELD OF VIEW (VERTICAL) (65.0)", "FIELD OF VIEW (VERTICAL)", "65.0")]
    [InlineData("HUD OPACITY", "HUD OPACITY", null)]
    public void SplitTrailingValue(string input, string title, string value)
    {
        var (t, v) = TextClean.SplitTrailingValue(input);
        Assert.Equal(title, t);
        Assert.Equal(value, v);
    }
}

public class MenuFormatTests
{
    [Fact]
    public void Selector()
    {
        var it = new MenuItem { Label = "mode", Role = Strings.RoleSelector, Value = "standard", OptionIndex = 0, OptionCount = 3, Position = 2, Count = 4 };
        Assert.Equal("mode, selector, standard, 1 of 3, 2 of 4", MenuFormat.Item(it));
        Assert.Equal("standard, 1 of 3", MenuFormat.ValueChange(it));
    }

    [Fact]
    public void PlainButtonAlone() =>
        Assert.Equal("play", MenuFormat.Item(new MenuItem { Label = "play", Position = 1, Count = 1 }));

    [Fact]
    public void ValueSameAsLabelNotRepeated() =>
        Assert.Equal("credits, 3 of 5", MenuFormat.Item(new MenuItem { Label = "credits", Value = "credits", Position = 3, Count = 5 }));

    [Fact]
    public void Context()
    {
        Assert.Equal("options. vibration", MenuFormat.WithContext("options", "vibration"));
        Assert.Equal("vibration", MenuFormat.WithContext("", "vibration"));
    }
}
