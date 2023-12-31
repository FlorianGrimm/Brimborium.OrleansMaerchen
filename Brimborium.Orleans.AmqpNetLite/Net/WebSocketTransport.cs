﻿//  ------------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation
//  All rights reserved. 
//  
//  Licensed under the Apache License, Version 2.0 (the ""License""); you may not use this 
//  file except in compliance with the License. You may obtain a copy of the License at 
//  http://www.apache.org/licenses/LICENSE-2.0  
//  
//  THIS CODE IS PROVIDED *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, 
//  EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY IMPLIED WARRANTIES OR 
//  CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABLITY OR 
//  NON-INFRINGEMENT. 
// 
//  See the Apache Version 2.0 License for specific language governing permissions and 
//  limitations under the License.
//  ------------------------------------------------------------------------------------
#nullable enable

namespace Brimborium.OrleansAmqp;

/// <summary>
/// The WebSocketTransport class allows applications to send and receive AMQP traffic
/// using the AMQP-WebSockets binding protocol.
/// </summary>
public class WebSocketTransport : IAsyncTransport {
    internal const string WebSocketSubProtocol = "amqp";
    internal const string WebSockets = "WS";
    internal const string SecureWebSockets = "WSS";
    internal const int WebSocketsPort = 80;
    internal const int SecureWebSocketsPort = 443;
    private WebSocket? _WebSocket;
    private Connection? _Connection;

    internal WebSocketTransport() {
    }

    internal WebSocketTransport(WebSocket webSocket) {
        this._WebSocket = webSocket;
    }

    internal static bool MatchScheme(string scheme) {
        return string.Equals(scheme, WebSockets, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(scheme, SecureWebSockets, StringComparison.OrdinalIgnoreCase);
    }

    internal Task<IAsyncTransport> ConnectAsync(Address address, Action<ClientWebSocketOptions>? options) {
        return this.ConnectAsync(address, WebSocketSubProtocol, options);
    }

    internal async Task<IAsyncTransport> ConnectAsync(Address address, string subprotocol, Action<ClientWebSocketOptions>? options) {
        Uri uri = new UriBuilder() {
            Scheme = address.Scheme,
            Port = GetDefaultPort(address.Scheme, address.Port),
            Host = address.Host,
            Path = address.Path
        }.Uri;

        var cws = new ClientWebSocket();
        cws.Options.AddSubProtocol(subprotocol);
        if (options != null) {
            options(cws.Options);
        }

        await cws.ConnectAsync(uri, CancellationToken.None).ConfigureAwait(false);
        this._WebSocket = cws;

        return this;
    }

    void IAsyncTransport.SetConnection(Connection connection) {
        this._Connection = connection;
    }

    async Task<int> IAsyncTransport.ReceiveAsync(byte[] buffer, int offset, int count) {
        var webSocket = this._WebSocket;
        if (webSocket is null) { return 0; }

        var result = await webSocket
            .ReceiveAsync(new ArraySegment<byte>(buffer, offset, count), CancellationToken.None)
            .ConfigureAwait(false);
        if (result.MessageType == WebSocketMessageType.Close) {
            return 0;
        }

        return result.Count;
    }

    async Task IAsyncTransport.SendAsync(IList<ByteBuffer> bufferList, int listSize) {
        var webSocket = this._WebSocket;
        if (webSocket is null) { return ; }
        foreach (var buffer in bufferList) {
            await webSocket
                .SendAsync(new ArraySegment<byte>(buffer.Buffer, buffer.Offset, buffer.Length), WebSocketMessageType.Binary, true, CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    void ITransport.Close() {
        var webSocket = this._WebSocket;
        if (webSocket is null) { return ; }

        webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "close", CancellationToken.None)
            .ContinueWith((t, o) => {
                if (t.IsFaulted) {
                    ((WebSocket?)o)?.Dispose();
                }
            }, webSocket);
    }

    void ITransport.Send(ByteBuffer buffer) {
        var webSocket = this._WebSocket;
        if (webSocket is null) { return; }

        webSocket
            .SendAsync(new ArraySegment<byte>(buffer.Buffer, buffer.Offset, buffer.Length), WebSocketMessageType.Binary, true, CancellationToken.None)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    int ITransport.Receive(byte[] buffer, int offset, int count) {
        return ((IAsyncTransport)this).ReceiveAsync(buffer, offset, count)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    private static int GetDefaultPort(string scheme, int port) {
        if (port < 0) {
            string temp = scheme.ToUpperInvariant();
            if (temp == WebSockets) {
                port = WebSocketsPort;
            } else if (temp == SecureWebSockets) {
                port = SecureWebSocketsPort;
            }
        }

        return port;
    }
}