using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Orleans.Configuration;
using Orleans.Extension.Clustering;

using System.Diagnostics;
using System.Net;
namespace Brimborium.OrleansMaerchen.Channel.Sample;

public class Program {
    public static async Task<int> Main(string[] args) {
        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings() {
                Args = args
            });
        builder.Configuration.AddUserSecrets<Program>();
#if DEBUG
        var clusterForkFeedback = await builder.HandleDevelopmentFork(typeof(Program).Assembly);
        if (clusterForkFeedback.ShouldExit(out var exitCode)) {
            System.Console.Out.WriteLine("main fork terminates.");
            return exitCode; 
        } else if (clusterForkFeedback.TryForked(out var forkIndex, out var forkCount)) {
            System.Console.Out.WriteLine($"Forked starts {forkIndex} / {forkCount}.");
            // System.Diagnostics.Debugger.Launch();
        } else {
            System.Console.Out.WriteLine("Non Forked starts.");
        }
#endif
        builder.Services.AddOptions<ProgramOptions>().BindConfiguration("");
        builder.UseOrleans((ISiloBuilder siloBuilder) => {
            siloBuilder.UseOrleansByConfiguration(builder.Configuration);
            /*
#if DEBUG
            SiloBuilderClusteringExtensions.AddClusteringByOptions(siloBuilder, clusterForkFeedback);
#else
            SiloBuilderClusteringExtensions.AddClusteringByOptions(siloBuilder);
#endif
            */
            //siloBuilder.UseLocalhostClustering();
            //siloBuilder.Configure<ClusterOptions>(options => {
            //    options.ClusterId = "my-first-cluster";
            //    options.ServiceId = "SampleApp";
            //});
            /*
            var primarySiloEndpoint = new IPEndpoint(PRIMARY_SILO_IP_ADDRESS, 11_111);
            siloBuilder.Configure<EndpointOptions>(options =>
            {
                // Port to use for silo-to-silo
                options.SiloPort = 11_111;
                // Port to use for the gateway
                options.GatewayPort = 30_000;
                // IP Address to advertise in the cluster
                //options.AdvertisedIPAddress = IPAddress.Parse("172.16.0.42");
                // The socket used for client-to-silo will bind to this endpoint
                options.GatewayListeningEndpoint = new IPEndPoint(IPAddress.Any, options.GatewayPort);
                // The socket used by the gateway will bind to this endpoint
                options.SiloListeningEndpoint = new IPEndPoint(IPAddress.Any, options.SiloPort);
            })
            Brimborium.OrleansMaerchen.Clustering
             Orleans.Extension.Clustering
            */
        });
        var app = builder.Build();
        await app.StartAsync();
        await Task.Delay(20000);
        await app.StopAsync();
        System.Console.Out.WriteLine("-fini-");
        return 0;
    }

}

public class ProgramOptions {
}