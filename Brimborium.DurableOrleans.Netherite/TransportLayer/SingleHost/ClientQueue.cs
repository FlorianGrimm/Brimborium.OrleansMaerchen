// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite.SingleHostTransport;

/// <summary>
/// An in-memory queue for delivering events.
/// </summary>
class ClientQueue : BatchWorker<ClientEvent> {
    public TransportAbstraction.IClient Client { get; }

    public ClientQueue(TransportAbstraction.IClient client, ILogger logger)
        : base($"ClientQueue.{Netherite.Client.GetShortId(client.ClientId)}", false, int.MaxValue, CancellationToken.None, null) {
        this.Client = client;
    }

    protected override Task Process(IList<ClientEvent> batch) {
        foreach (var evt in batch) {
            try {
                this.Client.Process(evt);
                DurabilityListeners.ConfirmDurable(evt);
            } catch (System.Threading.Tasks.TaskCanceledException) {
                // this is normal during shutdown
            } catch (Exception e) {
                this.Client.ReportTransportError(nameof(ClientQueue), e);
            }
        }
        return Task.CompletedTask;
    }
}
