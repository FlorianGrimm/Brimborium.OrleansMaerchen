namespace Brimborium.RateLimiting.Test;

public class RateLimitCreatorTest {
    [Fact]
    public void RateLimitCreatorNoLimiter() {
        var configuration = (new ConfigurationBuilder())
            .AddInMemoryCollection(new Dictionary<string, string?>() {
            { "RateLimiting:Kind", "NoLimiter" }
        }).Build();

        var rateLimitingOptions = configuration.GetLimiterOption(
            "RateLimiting",
            new RateLimitingOptions() { IsEnabled = true });
        Assert.NotNull(rateLimitingOptions);
        Assert.IsType<RateLimitingNoLimiterOptions>(rateLimitingOptions);
    }
}