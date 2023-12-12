namespace Brimborium.RateLimiting;

public sealed class RateLimitCreator {
    private static RateLimitCreator? _Instance;

    public static RateLimitCreator GetInstance() {
        if (_Instance is null) {
            lock (typeof(RateLimitCreator)) {
                if (_Instance is null) {
                    RateLimitCreator instance = new();
                    instance.Initialize();
                    _Instance = instance;
                }
            }
        }
        return _Instance;
    }

    private readonly Dictionary<string, RateLimitPartitionFactory> _DictFactoryByType = new(StringComparer.OrdinalIgnoreCase);

    public RateLimitCreator() {
    }

    public void Initialize() {
        this.AddFactory("SlidingWindow", new RateLimitPartitionSlidingWindowFactory());
        this.AddFactory("Concurrency", new RateLimitPartitionConcurrencyFactory());
        this.AddFactory("NoLimiter", new RateLimitPartitionNoLimiterFactory());
        this.AddFactory("FixedWindow", new RateLimitPartitionFixedWindowFactory());
        this.AddFactory("TokenBucket", new RateLimitPartitionTokenBucketFactory());
        // TODO: this.AddFactory("Chained", new RateLimitPartitionChainedFactory());
        // TODO: this.AddFactory("Sequence", new RateLimitPartitionChainedFactory());
    }

    public Dictionary<string, RateLimitPartitionFactory> DictFactoryByType => this._DictFactoryByType;

    public void AddFactory(string type, RateLimitPartitionFactory factory) {
        this._DictFactoryByType.Add(type, factory);
    }

    public string? DefaultKind { get; set; } //= "SlidingWindow";

    public RateLimitingOptions BindOption(
        IConfiguration configuration,
        RateLimitingOptions limiterOptions) {
        var kind = limiterOptions.Kind ?? this.DefaultKind;
        if (kind is null) {
            return new RateLimitingOptions() { IsEnabled = false, Kind = null };
        }
        if (!this._DictFactoryByType.TryGetValue(kind, out var rateLimitPartitionFactory)) {
            return new RateLimitingOptions() { IsEnabled = false, Kind = null };
        }
        return rateLimitPartitionFactory.BindOptions(configuration, limiterOptions);
    }
}
