namespace Brimborium.RateLimiting;

public sealed class RateLimitingNoLimiterOptions : RateLimitingOptions {
    public override RateLimitPartition<TKey> CreateRateLimitPartition<TKey>(TKey partitionKey) {
        return RateLimitPartition.GetNoLimiter(partitionKey);
    }
}

public sealed class RateLimitPartitionNoLimiterFactory
    : RateLimitPartitionFactory {
    public RateLimitPartitionNoLimiterFactory() {
    }

    public override RateLimitingOptions BindOptions(IConfiguration configuration, RateLimitingOptions limiterOptions) {
        return new RateLimitingNoLimiterOptions();
    }
}
