
using Brimborium.OrleansAmqp.Listener;

using Microsoft.AspNetCore.Builder;

namespace Brimborium.Orleans.AmqpNetLite.WebAmqpBroker;

public class Program {
    public static void Main(string[] args) {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        //builder.Services.AddAuthorization();

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        //builder.Services.AddEndpointsApiExplorer();
        //builder.Services.AddSwaggerGen();
        

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        //if (app.Environment.IsDevelopment()) {
        //    app.UseSwagger();
        //    app.UseSwaggerUI();
        //}

        //app.UseHttpsRedirection();

        //app.UseAuthorization();
        app.UseWebSockets(new WebSocketOptions() { 
        });

        List<string> endpoints = new();
        string? creds = null;
        string? trace = null;
        string? sslValue = null;
        string[]? queues = null;
        bool parseEndpoint = true;


        for (int i = 0; i < args.Length; i++) {
            if (args[i][0] != '/' && parseEndpoint) {
                endpoints.Add(args[i]);
            } else {
                parseEndpoint = false;
                if (args[i].StartsWith("/creds:", StringComparison.OrdinalIgnoreCase)) {
                    creds = args[i].Substring(7);
                } else if (args[i].StartsWith("/trace:", StringComparison.OrdinalIgnoreCase)) {
                    trace = args[i].Substring(7);
                } else if (args[i].StartsWith("/queues:", StringComparison.OrdinalIgnoreCase)) {
                    queues = args[i].Substring(8).Split(';');
                } else if (args[i].StartsWith("/cert:", StringComparison.OrdinalIgnoreCase)) {
                    sslValue = args[i].Substring(6);
                } else {
                    Console.WriteLine("Unknown argument: {0}", args[i]);
                    // Usage();
                    return;
                }
            }
        }

        var broker = new AmqpBroker(endpoints, creds, sslValue, queues);
        //app.Use(new WebSockectMiddleWareListener().InvokeAsync);
        WebSockectMiddleWareListener.Use(app, broker);
        app.MapGet("/", async (context) => {
            await context.Response.WriteAsync("Hello World!");
        });
        broker.Start();
        app.Run();
    }
}
