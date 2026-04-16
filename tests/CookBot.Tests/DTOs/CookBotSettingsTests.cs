using CookBot.Application.DTOs;

namespace CookBot.Tests.DTOs;

public class CookBotSettingsTests
{
    [Fact]
    public void Default_AiFeaturesEnabled_AllowsHostToOfferOptionalAi()
    {
        var settings = new CookBotSettings();
        Assert.True(settings.AiFeaturesEnabled);
    }
}
