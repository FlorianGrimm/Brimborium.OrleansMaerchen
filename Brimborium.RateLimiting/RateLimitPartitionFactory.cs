namespace Brimborium.RateLimiting;

public abstract class RateLimitPartitionFactory {
    public abstract RateLimitingOptions BindOptions(
        IConfiguration configuration,
        RateLimitingOptions limiterOptions);

    // public abstract RateLimitPartition<TKey> CreateRateLimitPartition<TKey>();
}

/*
public class RateLimitPartitionFactory<TKey>
    : RateLimitPartitionFactory {
    //public RateLimitPartition<TKey> Create(
        //IConfiguration configuration
        //) {
        //RateLimitPartition.GetTokenBucketLimiter
        //public static RateLimitPartition<TKey> GetConcurrencyLimiter<TKey>(TKey partitionKey, Func<TKey, ConcurrencyLimiterOptions> factory);
        // ConcurrencyLimiterOptions
    //}
}
*/