# Brimborium.RateLimiting

Idea:

Based on the configuration (section) generate the options and the rate limiter.

## Configuration

```json
{
	"RateLimiting": {
		"MySection": {
			"IsEnabled": true,
			"Kind": "SlidingWindow",
			"Limit": 100,
			"Period": "00:01:00"
		},
		"IPLimiter": {
			"IsEnabled": true,
			"Kind": "Sequence",
			"Children": [
				{
					"Kind": "Concurrency",
					"Limit": 100,
					"Period": "00:01:00"
				},
				{
					"Kind": "SlidingWindow",
					"Limit": 100,
					"Period": "00:01:00"
				}
			]"
		}
	}
}
```

## Builder

```csharp
builder.Services.AddRateLimiter((limiterOptions) => {
	var ipLimiterOptions = GetLimiterOption(builder.Configuration, "IPLimiter");
	if (ipLimiterOptions.IsEnabled){
		limiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, IPAddress>(
			partitioner: (httpContext) => {
				var remoteIpAddress = httpContext.Connection.RemoteIpAddress;
				if (remoteIpAddress is null) {
					return RateLimitPartition.GetNoLimiter<IPAddress>(IPAddress.Any);
				} else {
					/*
					return RateLimitPartition.GetTokenBucketLimiter<IPAddress>(
						remoteIpAddress,
						(ip) => ipLimiterOptions
					);
					*/
					return ipLimiterOptions.GetRateLimitPartition<IPAddress>(remoteIpAddress);
				}
			},
			equalityComparer: null);
	}
});
```

