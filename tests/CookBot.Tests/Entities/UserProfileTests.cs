using CookBot.Domain.Entities;

namespace CookBot.Tests.Entities;

public class UserProfileTests
{
    [Fact]
    public void New_UserProfile_AiEnabled_IsOptInOff()
    {
        var profile = new UserProfile();
        Assert.False(profile.AiEnabled);
    }
}
