namespace Brimborium.DurableOrleans.Hosting.Sample;

public class Program : IHostedService {
    public static void Main(string[] args) {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddHostedService<Program>();

        IHost host = builder.Build();
        host.Run();
    }

    public Task StartAsync(CancellationToken cancellationToken) {
        var orchestrationServiceAndClient = new DurableOrleansOrchestrationService();
        var taskHubClient = new TaskHubClient(orchestrationServiceAndClient);
        var taskHubWorker = new TaskHubWorker(orchestrationServiceAndClient);

        OrchestrationInstance instance;
        string instanceId = System.Guid.NewGuid().ToString();
        instance = taskHubClient.CreateOrchestrationInstanceAsync(typeof(GreetingsOrchestration), instanceId, null).Result;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) {
        return Task.CompletedTask;
    }
}
public class GreetingsOrchestration : TaskOrchestration<string, string> {
    public override async Task<string> RunTask(OrchestrationContext context, string input) {
        string user = await context.ScheduleTask<string>(typeof(GetUserTask));
        string greeting = await context.ScheduleTask<string>(typeof(SendGreetingTask), user);
        return greeting;
    }
}
public sealed class GetUserTask : TaskActivity<string, string> {
    protected override string Execute(TaskContext context, string input) {

        Console.WriteLine("Waiting for user to enter name...");
        while (true) {
            var user = Console.ReadLine();
            if (string.IsNullOrEmpty(user)) {
            } else {
                return user;
            }
        }
    }
}
public sealed class SendGreetingTask : TaskActivity<string, string> {
    protected override string Execute(TaskContext context, string user) {
        string message;
        if (!string.IsNullOrWhiteSpace(user) && user.Equals("TimedOut")) {
            message = "GetUser Timed out!!!";
            Console.WriteLine(message);
        } else {
            Console.WriteLine("Sending greetings to user: " + user + "...");

            Thread.Sleep(5 * 1000);

            message = "Greeting sent to " + user;
            Console.WriteLine(message);
        }

        return message;
    }
}