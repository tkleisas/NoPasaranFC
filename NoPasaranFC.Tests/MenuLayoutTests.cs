using NoPasaranFC.Screens;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>The main menu must show every option on all supported resolutions.</summary>
public class MenuLayoutTests
{
    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    [InlineData(1280, 720)]
    [InlineData(1366, 768)]
    [InlineData(1600, 900)]
    [InlineData(1920, 1080)]
    public void MainMenuRowsFitOnScreen(int width, int height)
    {
        float scale = Math.Clamp(height / 720f, 1f, 3f);
        const int optionCount = 7;

        var (startY, itemHeight) = MenuScreen.GetMainMenuLayout(height, scale, optionCount);

        Assert.True(itemHeight > 0f);
        Assert.True(startY + itemHeight * optionCount <= height,
            $"{width}x{height}: menu ends at {startY + itemHeight * optionCount:F1}px");
    }
}
