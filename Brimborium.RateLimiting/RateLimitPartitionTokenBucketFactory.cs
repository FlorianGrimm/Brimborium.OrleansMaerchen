namespace Brimborium.RateLimiting;

public sealed class RateLimitingTokenBucketOptions(
        TokenBucketRateLimiterOptions options
        ) : RateLimitingOptions {
    public TokenBucketRateLimiterOptions Options { get; } = options;

    public override RateLimitPartition<TKey> CreateRateLimitPartition<TKey>(TKey partitionKey) {
        return RateLimitPartition.GetTokenBucketLimiter(partitionKey, _ => this.Options);
    }
}

public sealed class RateLimitPartitionTokenBucketFactory
    : RateLimitPartitionFactory {
    public RateLimitPartitionTokenBucketFactory() {
    }

    public override RateLimitingOptions BindOptions(IConfiguration configuration, RateLimitingOptions limiterOptions) {
        TokenBucketRateLimiterOptions options = new();
        configuration.Bind(options);
        return new RateLimitingTokenBucketOptions(options);
    }
}
