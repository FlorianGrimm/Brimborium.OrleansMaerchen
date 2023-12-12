namespace Brimborium.RateLimiting;

public class RateLimitingOptions { 
    public bool? IsEnabled { get; set; }
    public string? Kind { get; set; }

    public RateLimitingOptions ApplyDefault(RateLimitingOptions defaultOptions)
        => new() {
            IsEnabled = this.IsEnabled ?? defaultOptions.IsEnabled,
            Kind = this.Kind ?? defaultOptions.Kind
        };

    public virtual RateLimitPartition<TKey> CreateRateLimitPartition<TKey>(TKey partitionKey) {
        throw new NotSupportedException("");
    }
}
