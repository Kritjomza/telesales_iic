using Telesale.Api.Helpers;

namespace Telesale.Api.Tests;

public class DatabaseInitializerPolicyTests
{
    [Theory]
    [InlineData("true", false, true, false)]
    [InlineData("TRUE", false, true, false)]
    [InlineData("false", true, false, false)]
    [InlineData(null, true, true, false)]
    [InlineData("", true, true, false)]
    [InlineData(null, false, false, false)]
    [InlineData("", false, false, false)]
    [InlineData("not-a-boolean", true, false, true)]
    public void ShouldRun_UsesExplicitSettingOrEnvironmentSafeDefault(
        string? setting,
        bool isDevelopment,
        bool expectedShouldRun,
        bool expectedInvalid)
    {
        var shouldRun = DatabaseInitializerPolicy.ShouldRun(setting, isDevelopment, out var invalid);

        Assert.Equal(expectedShouldRun, shouldRun);
        Assert.Equal(expectedInvalid, invalid);
    }
}
