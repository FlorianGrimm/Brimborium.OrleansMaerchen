

using System.Diagnostics;

namespace Brimborium.OrleansMaerchen.Performance;

internal class Program {
    static async Task Main(string[] args) {
        var url = "http://localhost:5221";

        List<string> listDifferentId = [];

        {
            System.Console.Out.WriteLine("Test1");
            var id = Guid.NewGuid().ToString("d");
            var context0 = new Context();
            
            //await RunAsync(20, 2, context0, async (Context context) => {
            await RunAsync(20, 1, context0, async (Context context) => {
                var sw = new Stopwatch();
                sw.Start();
                var httpClient = context.HttpClient;
                if (httpClient is null) { 
                    httpClient = new HttpClient();
                    httpClient.BaseAddress = new Uri(url);
                    context.HttpClient = httpClient;
                }

                HttpRequestMessage request = new();
                request.Method = HttpMethod.Get;
                request.RequestUri = new Uri($"{url}/todo/{id}");

                using var response = await httpClient.SendAsync(request);
                _ = await response.Content.ReadAsStringAsync();
                sw.Stop();

            });
        }
        
        Console.WriteLine("- fini -");
    }

    public struct Context {
        public int Rate;
        public int Loop;
        public HttpClient? HttpClient;
    }


    public static async Task RunAsync(
        int rate,
        int loops,
        Context context,
        Func<Context, Task> actionAsync
        ) {
        List<Task> listTask = [];
        var syncPoint = new SyncPoint();
        for (int idxRate = 0; idxRate < rate; idxRate++) {
            Context contextRate = context;
            contextRate.Rate = idxRate;
            var task = RunOneRateAsync(loops, contextRate, syncPoint, actionAsync);
            listTask.Add(task);
        }
        var sw = new Stopwatch();
        System.Console.Out.WriteLine("  Start");
        sw.Start();
        await syncPoint.WaitToContinue();
        await Task.WhenAll(listTask);
        sw.Stop();
        System.Console.Out.WriteLine($"  Stop ElapsedMilliseconds: {sw.ElapsedMilliseconds}");
    }

    private static async Task RunOneRateAsync(int loops, Context context, SyncPoint syncPoint, Func<Context, Task> actionAsync) {
        System.Console.Out.WriteLine("  WaitForSyncPoint");
        await syncPoint.WaitForSyncPoint();
        for (int iLoop = 0; iLoop < loops; iLoop++) {
            context.Loop = iLoop;
            System.Console.Out.WriteLine($"  Start {context.Rate} {iLoop}");
            var sw = new Stopwatch();
            sw.Start();
            await actionAsync(context);
            sw.Stop();
            System.Console.Out.WriteLine($"  Stop  {context.Rate} {iLoop} ElapsedMilliseconds: {sw.ElapsedMilliseconds}");
        }
        syncPoint.Continue();
    }
}
