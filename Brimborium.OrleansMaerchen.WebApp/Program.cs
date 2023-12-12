#pragma warning disable IDE0058 // Expression value is never used

#if false
using Microsoft.AspNetCore.Authentication.Negotiate;
#endif

using OpenTelemetry.Resources;

namespace Brimborium.OrleansMaerchen.WebApp;

public class Program {
    private static string GetUserEndPoint(HttpContext context) =>
       $"User {context.User.Identity?.Name ?? "Anonymous"} endpoint:{context.Request.Path} {context.Connection.RemoteIpAddress}";

    public static void Main(string[] args) {
        var builder = WebApplication.CreateBuilder(args);

        builder.AddServiceDefaults();

#if false
        // Add services to the container.
        builder.Services.AddAuthorization();
#endif
        builder.Services.AddOpenTelemetry()
            .ConfigureResource((resourceBuilder) => {
                //resourceBuilder.AddService("Microsoft.Orleans.Silo")
                //.AddService("Microsoft.Orleans.Application");
            });
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

#if false
        builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
            .AddNegotiate();

        builder.Services.AddAuthorization(options => {
            // By default, all incoming requests will be authorized according to the default policy.
            options.FallbackPolicy = options.DefaultPolicy;
        });
#endif

        builder.Host.UseOrleans((hostBuilderContext, siloBuilder) => {
            siloBuilder.UseLocalhostClustering();
            siloBuilder.Configure<ClusterOptions>(options => {
                //options.ServiceId;
                //options.ClusterId
                builder.Configuration.Bind("Orleans", options);
            });

            //siloBuilder.ConfigureEndpoints(siloPort: 17_256, gatewayPort: 34_512);

            siloBuilder.Configure<EndpointOptions>(options => {
                // Port to use for silo-to-silo
                // options.SiloPort = 11_111;
                // Port to use for the gateway
                // options.GatewayPort = 30_000;
                // IP Address to advertise in the cluster
                // options.AdvertisedIPAddress
                // The socket used by the gateway will bind to this endpoint
                // options.SiloListeningEndpoint
                // The socket used for client-to-silo will bind to this endpoint
                // options.GatewayListeningEndpoint 
                builder.Configuration.Bind("Orleans", options);
            });

            siloBuilder.AddActivityPropagation();
            siloBuilder.AddLocalFileGrainStorage("Todo", (options) => {
                builder.Configuration.Bind("Orleans:FileGrainStorage", options);
                // options.RootDirectory = "";

            });
        });

#if true
        const string PolicyLimiterUserName = "LimiterUserName";

        builder.Services.AddRateLimiter((limiterOptions) => {


            // https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-8.0
            // https://github.com/dotnet/AspNetCore.Docs.Samples/blob/main/fundamentals/middleware/rate-limit/WebRateLimitAuth/Program.cs#L145,L281
            limiterOptions.OnRejected = (context, cancellationToken) => {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)) {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
                }

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.RequestServices.GetService<ILoggerFactory>()?
                    .CreateLogger("Microsoft.AspNetCore.RateLimitingMiddleware")
                    .LogWarning("OnRejected: {GetUserEndPoint}", GetUserEndPoint(context.HttpContext));

                return new ValueTask();
            };
            var sectionRateLimit_IpLimiter = builder.Configuration.GetRequiredSection("RateLimit:IpLimiter");
            if (sectionRateLimit_IpLimiter.Exists()) {
                var rateLimit_GlobalLimiter_IsEnabled
                        = sectionRateLimit_IpLimiter.GetValue<bool>("IsEnabled");
                if (rateLimit_GlobalLimiter_IsEnabled) {
                    var ipLimiterOptions = new TokenBucketRateLimiterOptions() {
                        AutoReplenishment = true,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 100,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        TokenLimit = 50,
                        TokensPerPeriod = 25
                    };
                    sectionRateLimit_IpLimiter.Bind(ipLimiterOptions);

                    limiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, IPAddress>(
                    partitioner: (httpContext) => {
                        var remoteIpAddress = httpContext.Connection.RemoteIpAddress;
                        if (remoteIpAddress is null) {
                            return RateLimitPartition.GetNoLimiter<IPAddress>(IPAddress.Any);
                        } else {
                            return RateLimitPartition.GetTokenBucketLimiter<IPAddress>(
                                remoteIpAddress,
                                (ip) => ipLimiterOptions
                            );
                        }
                    },
                    equalityComparer: null);
                }
            }

            bool limiterOptionsAnonymousIsEnabled;
            SlidingWindowRateLimiterOptions limiterOptionsAnonymous = new SlidingWindowRateLimiterOptions() {
                PermitLimit = 100,
                QueueLimit = 100,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                SegmentsPerWindow = 4,
                Window = TimeSpan.FromMinutes(2),
                AutoReplenishment = true
            };
            {
                var configurationSection = builder.Configuration.GetRequiredSection("RateLimit:LimiterUserName");
                limiterOptionsAnonymousIsEnabled = configurationSection.GetValue<bool?>("IsEnabled").GetValueOrDefault(true);
                configurationSection.Bind(limiterOptionsAnonymous);
                limiterOptionsAnonymous.AutoReplenishment = true;
            }

            bool limiterOptionsUserIsEnabled;
            SlidingWindowRateLimiterOptions limiterOptionsUser = new SlidingWindowRateLimiterOptions() {
                PermitLimit = 100,
                QueueLimit = 100,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                SegmentsPerWindow = 4,
                Window = TimeSpan.FromMinutes(2),
                AutoReplenishment = true
            };
            {
                var configurationSection = builder.Configuration.GetRequiredSection("RateLimit:LimiterAnonymous");
                configurationSection.Bind(limiterOptionsUser);
                limiterOptionsUserIsEnabled = configurationSection.GetValue<bool?>("IsEnabled").GetValueOrDefault(true);
                limiterOptionsUser.AutoReplenishment = true;
            }
            limiterOptions.AddPolicy(PolicyLimiterUserName, context => {

                if (context.User.Identity?.IsAuthenticated is true) {
                    var username = context.User.ToString()!;
                    if (limiterOptionsUserIsEnabled) {
                        return RateLimitPartition.GetSlidingWindowLimiter(username,
                        _ => limiterOptionsUser);
                    } else {
                        return RateLimitPartition.GetNoLimiter(username);
                    }
                } else {
                    var username = "anonymous user";
                    if (limiterOptionsAnonymousIsEnabled) {
                        return RateLimitPartition.GetSlidingWindowLimiter(username,
                        _ => limiterOptionsAnonymous);
                    } else {
                        return RateLimitPartition.GetNoLimiter(username);
                    }
                }
            });
        });

#endif
            var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment()) {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        if (app.Configuration.GetValue<bool?>("UseHttpsRedirection").GetValueOrDefault(true)) {
            app.UseHttpsRedirection();
        }

#if false
        app.UseAuthorization();
#endif

        //https://devblogs.microsoft.com/dotnet/announcing-rate-limiting-for-dotnet/

#if false
        app.UseRateLimiter();
#endif

        {
            var groupTodo = app.MapGroup("/Todo");
            groupTodo.MapGet(
                "/{id:guid}",
                async Task<Results<Ok<Todo>, NotFound>> (Guid id, HttpContext httpContext, IClusterClient clusterClient) => {
                    var result = await id.TryCatchAsync(
                        async (id) => {
                            var grain = clusterClient.GetGrain<ITodoGrain>(id);
                            var optTodo = await grain.GetAsync();
                            return optTodo;
                        }).ChainAsync(OptionalResultExtensions.ToGetResults);
                    return result;
                })
                .WithName("TodoGet")
                .WithOpenApi()
            //.RequireRateLimiting(PolicyLimiterUserName)
            ;

            groupTodo.MapPost("/{id:guid}", async (Guid id, Todo value, HttpContext httpContext, IClusterClient clusterClient) => {
                var result = await id.TryCatchAsync(
                        async (id) => {
                            if (id == Guid.Empty) { id = Guid.NewGuid(); }
                            var grain = clusterClient.GetGrain<ITodoGrain>(id);
                            return await grain.SetAsync(value);
                        }).ChainAsync(OptionalResultExtensions.ToGetResults);
                return result;
                /*
                if (id == Guid.Empty) { id = Guid.NewGuid(); }
                var grain = clusterClient.GetGrain<ITodoGrain>(id);
                await grain.SetAsync(value);
                */
            })
                .WithName("TodoSet")
                .WithOpenApi()
                ;
            groupTodo.MapPut("/{id:guid}", async (Guid id, TodoPartial value, HttpContext httpContext, IClusterClient clusterClient) => {
                var result = await id.TryCatchAsync(
                        async (id) => {
                            if (id == Guid.Empty) { id = Guid.NewGuid(); }
                            var grain = clusterClient.GetGrain<ITodoGrain>(id);
                            return await grain.SetPartialAsync(value);
                        }).ChainAsync(OptionalResultExtensions.ToGetResults);
                return result;
                /*
                if (id == Guid.Empty) { id = Guid.NewGuid(); }
                var grain = clusterClient.GetGrain<ITodoGrain>(id);
                await grain.SetAsync(value);
                */
            })
                .WithName("TodoSetPartial")
                .WithOpenApi()
                ;

        }


        var summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        app.MapGet("/weatherforecast", (HttpContext httpContext) => {
            var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast {
                    Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    TemperatureC = Random.Shared.Next(-20, 55),
                    Summary = summaries[Random.Shared.Next(summaries.Length)]
                })
                .ToArray();
            return forecast;
        })
        .WithName("GetWeatherForecast")
        .WithOpenApi()
#if false
        .RequireAuthorization()
#endif
        ;

        app.Run();
    }
}