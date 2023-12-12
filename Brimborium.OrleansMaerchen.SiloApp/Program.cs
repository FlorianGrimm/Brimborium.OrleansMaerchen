using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Brimborium.OrleansMaerchen.SiloApp;

internal class Program {
    static void Main(string[] args) {
        using (IHost host = new HostBuilder().Build()) {
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

            lifetime.ApplicationStarted.Register(() =>
            {
                Console.WriteLine("Started");
            });
            lifetime.ApplicationStopping.Register(() =>
            {
                Console.WriteLine("Stopping firing");
                Console.WriteLine("Stopping end");
            });
            lifetime.ApplicationStopped.Register(() =>
            {
                Console.WriteLine("Stopped firing");
                Console.WriteLine("Stopped end");
            });

            host.Start();

            // Listens for Ctrl+C.
            host.WaitForShutdown();
        }
    }
}
