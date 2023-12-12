namespace Brimborium.RateLimiting;

public sealed class RateLimitingFixedWindowOptions(
        FixedWindowRateLimiterOptions options
        ) : RateLimitingOptions {
    public FixedWindowRateLimiterOptions Options { get; } = options;

    public override RateLimitPartition<TKey> CreateRateLimitPartition<TKey>(TKey partitionKey) {        
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => this.Options);
    }
}

public sealed class RateLimitPartitionFixedWindowFactory
    : RateLimitPartitionFactory {
    public override RateLimitingOptions BindOptions(IConfiguration configuration, RateLimitingOptions limiterOptions) {
        FixedWindowRateLimiterOptions options = new();
        configuration.Bind(options);
        return new RateLimitingFixedWindowOptions(options);
    }
}
