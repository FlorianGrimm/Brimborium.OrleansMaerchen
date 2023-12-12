#if false
namespace Brimborium.RateLimiting;

public sealed class RateLimitingChainedOptions : RateLimitingOptions {
    public RateLimitingChainedOptions(
            ChainedRateLimiterOptions options
        ) {
        this.Options = options;
    }

    public ChainedRateLimiterOptions Options { get; }

    public override RateLimitPartition<TKey> CreateRateLimitPartition<TKey>(TKey partitionKey) {
        return RateLimitPartition.GetChainedLimiter(partitionKey, _ => this.Options);
    }
}

public class RateLimitPartitionChainedFactory
    : RateLimitPartitionFactory {
    public RateLimitPartitionChainedFactory() {
    }

    public override RateLimitingOptions BindOptions(IConfiguration configuration, RateLimitingOptions limiterOptions) {
        ChainedRateLimiterOptions options = new();
        configuration.Bind(options);
        return new RateLimitingChainedOptions(options);
    }
}
#endif
