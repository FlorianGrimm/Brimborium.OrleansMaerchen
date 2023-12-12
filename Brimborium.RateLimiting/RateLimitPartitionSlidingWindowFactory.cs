namespace Brimborium.RateLimiting;

public sealed class RateLimitingSlidingWindowOptions(
        SlidingWindowRateLimiterOptions options
        ) : RateLimitingOptions {

    public SlidingWindowRateLimiterOptions Options { get; } = options;

    public override RateLimitPartition<TKey> CreateRateLimitPartition<TKey>(TKey partitionKey) {
        return RateLimitPartition.GetSlidingWindowLimiter(partitionKey, _ => this.Options);
    }
}

public sealed class RateLimitPartitionSlidingWindowFactory
    : RateLimitPartitionFactory {
    public RateLimitPartitionSlidingWindowFactory() {
    }

    public override RateLimitingOptions BindOptions(IConfiguration configuration, RateLimitingOptions limiterOptions) {
        SlidingWindowRateLimiterOptions options = new();
        configuration.Bind(options);
        return new RateLimitingSlidingWindowOptions(options);
    }
}
