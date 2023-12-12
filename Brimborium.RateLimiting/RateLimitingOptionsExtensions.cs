namespace Microsoft.Extensions.Configuration;

public static class RateLimitingOptionsExtensions {
    public static RateLimitingOptions GetLimiterOption(
        this IConfiguration configuration,
        string? key,
        RateLimitingOptions? defaultOptions = null
        ) {
        var configurationSection = configuration.GetConfigurationSection(key);
        defaultOptions ??= new() { IsEnabled = true };
        var limiterOptions = new RateLimitingOptions();
        configurationSection.Bind(limiterOptions);
        var rateLimitCreator = RateLimitCreator.GetInstance();
        var effectivOptions = limiterOptions.ApplyDefault(defaultOptions);
        return rateLimitCreator.BindOption(
            configurationSection,
            effectivOptions);
    }

    private static IConfiguration GetConfigurationSection(this IConfiguration configuration, string? key) {
        return string.IsNullOrEmpty(key)
            ? configuration
            : configuration.GetSection(key);
    }
}
