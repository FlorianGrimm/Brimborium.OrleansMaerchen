namespace Brimborium.RateLimiting;

public sealed class RateLimitingConcurrencyOptions(
        ConcurrencyLimiterOptions options
        ) : RateLimitingOptions {

    public ConcurrencyLimiterOptions Options { get; } = options;

    public override RateLimitPartition<TKey> CreateRateLimitPartition<TKey>(TKey partitionKey) {
        return RateLimitPartition.GetConcurrencyLimiter(partitionKey, _ => this.Options);
    }
}

public sealed class RateLimitPartitionConcurrencyFactory
    : RateLimitPartitionFactory {
    public override RateLimitingOptions BindOptions(IConfiguration configuration, RateLimitingOptions limiterOptions) {
        ConcurrencyLimiterOptions options = new();
        configuration.Bind(options);
        return new RateLimitingConcurrencyOptions(options);
    }
}
