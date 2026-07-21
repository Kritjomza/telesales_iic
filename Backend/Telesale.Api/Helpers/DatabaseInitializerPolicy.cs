namespace Telesale.Api.Helpers;

public static class DatabaseInitializerPolicy
{
    public static bool ShouldRun(string? setting, bool isDevelopment, out bool invalid)
    {
        invalid = false;
        if (string.IsNullOrWhiteSpace(setting)) return isDevelopment;
        if (bool.TryParse(setting, out var enabled)) return enabled;
        invalid = true;
        return false;
    }
}
