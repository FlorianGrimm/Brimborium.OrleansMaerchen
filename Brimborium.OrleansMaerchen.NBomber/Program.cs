using System;
using System.Threading.Tasks;
using System.Net.Http;

using NBomber.CSharp;
using NBomber.Http.CSharp;
using System.Text;

namespace NBomberTest {
    class Program {
        static void Main(string[] args) {
            using var httpClient = new HttpClient();
            var url = "http://localhost:5221";
            List<string> listDifferentId = [];
            var scenario_http_different_ids = Scenario.Create("http_different_ids", async context => {
                var invocationNumber = (int)context.InvocationNumber;
                string id;
                lock (listDifferentId) {
                    if (invocationNumber < listDifferentId.Count) {
                        id = listDifferentId[invocationNumber];
                    } else {
                        id = Guid.NewGuid().ToString("d");
                        listDifferentId.Add(id);
                    }
                }

                var step1 = await Step.Run("step_1", context, async () => {
                    var request =
                       Http.CreateRequest("GET", $"{url}/todo/{id}")
                           .WithHeader("Accept", "application/json")
                    ;
                    var response = await Http.Send(httpClient, request);
                    return response;
                });

                var step2 = await Step.Run("step_2", context, async () => {
                    var request =
                       Http.CreateRequest("POST", $"{url}/todo/{id}")
                            .WithHeader("Content-Type", "application/json")
                            .WithBody(new StringContent($$"""
                                {
                                    "todoId": "{{id}}",
                                    "name": "string",
                                    "done": true
                                }
                                """, Encoding.UTF8, "application/json"))
                            ;

                    var response = await Http.Send(httpClient, request);
                    return response;
                });


                var step3 = await Step.Run("step_3", context, async () => {
                    var request =
                       Http.CreateRequest("GET", $"{url}/todo/{id}")
                           .WithHeader("Accept", "application/json")
                    ;
                    var response = await Http.Send(httpClient, request);
                    return response;
                });

                return Response.Ok();
            })
            .WithoutWarmUp()
            .WithLoadSimulations(
                Simulation.Inject(rate: 100,
                    interval: TimeSpan.FromSeconds(1),
                    during: TimeSpan.FromSeconds(10))
            );

            var sameid = Guid.NewGuid().ToString("d");
            var scenario_http_same_id = Scenario.Create("http_same_id", async context => {
                var step1 = await Step.Run("step_1", context, async () =>
                {
                    var request =
                       Http.CreateRequest("GET", $"{url}/todo/{sameid}")
                           .WithHeader("Accept", "application/json")
                    ;
                    var response = await Http.Send(httpClient, request);
                    return response;
                });

                var step2 = await Step.Run("step_2", context, async () => {
                    var request =
                       Http.CreateRequest("POST", $"{url}/todo/{sameid}")
                            .WithHeader("Content-Type", "application/json")
                            .WithBody(new StringContent($$"""
                                {
                                    "todoId": "{{sameid}}",
                                    "name": "string",
                                    "done": true
                                }
                                """, Encoding.UTF8, "application/json"))
                            ;

                    var response = await Http.Send(httpClient, request);
                    return response;
                });


                var step3 = await Step.Run("step_3", context, async () => {
                    var request =
                       Http.CreateRequest("GET", $"{url}/todo/{sameid}")
                           .WithHeader("Accept", "application/json")
                    ;
                    var response = await Http.Send(httpClient, request);
                    return response;
                });

                return Response.Ok();
            })
            .WithoutWarmUp()
            .WithLoadSimulations(
                Simulation.Inject(rate: 100,
                    interval: TimeSpan.FromSeconds(1),
                    during: TimeSpan.FromSeconds(1))
            );

            NBomberRunner
                //.RegisterScenarios(scenario_http_different_ids, scenario_http_same_id)
                .RegisterScenarios(scenario_http_same_id)
                .Run();
        }
    }
}