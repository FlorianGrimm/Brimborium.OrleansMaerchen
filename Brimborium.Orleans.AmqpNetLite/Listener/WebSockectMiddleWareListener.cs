using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Brimborium.OrleansAmqp.Listener;

public class WebSockectMiddleWareListener
    : IMiddleware {

    public static WebSockectMiddleWareListener Use(
        WebApplication app,
        IContainer broker
    ) {
        var result = new WebSockectMiddleWareListener();
        app.Map("/api/queue/{queue}", async (
            string queue,
            HttpRequest httpRequest) => {
                var httpContext = httpRequest.HttpContext;
                
                //broker.GetFactory
                var webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
                var wsTransport = new ListenerWebSocket2Transport(webSocket, httpContext.User);
                //await broker.HandleTransportAsync(wsTransport, queue, webSocket);
                throw new Exception();
            });
        //app.Map("/queue", app => {
        //    app.UseWebSockets(new WebSocketOptions() {
        //    });
        //    app.Use(result.InvokeAsync);
        //});
        app.Use(result.InvokeAsync);
        return result;
    }

    public WebSockectMiddleWareListener(
        //ConnectionListener listener, 
        //string scheme, string host, int port, 
        //string path
        ) {
    }

    public async Task InvokeAsync(
        HttpContext context,
        RequestDelegate next) {
        if (context.Request.Path.StartsWithSegments("/queue")) {
            if (context.WebSockets.IsWebSocketRequest) {
                //new PathString("")
                //context.Request.Path.Value
                //context.WebSockets.WebSocketRequestedProtocols
                //context.RequestServices
                var webSocket = await context.WebSockets.AcceptWebSocketAsync();

                //var wsTransport = new ListenerWebSocketTransport(wsContext.WebSocket, principal);
                //await this.Listener.HandleTransportAsync(wsTransport, handler, wsContext.WebSocket).ConfigureAwait(false);


                //await Echo(webSocket);
            } else {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        } else {
            await next(context);
        }
    }
}

public class WebSocket2TransportListener : TransportListener {
    private readonly HttpListener _HttpListener;

    public WebSocket2TransportListener(ConnectionListener listener, string scheme, string host, int port, string path) {
        this.Listener = listener;

        // if certificate is set, it must be bound to host:port by netsh http command
        string address = string.Format("{0}://{1}:{2}{3}", scheme, host, port, path);
        this._HttpListener = new HttpListener();
        this._HttpListener.Prefixes.Add(address);
    }

    public override void Open() {
        this._HttpListener.Start();
        var task = this.AcceptListenerContextLoop();
    }

    public override void Close() {
        this.closed = true;
        this._HttpListener.Stop();
        this._HttpListener.Close();
    }

    private async Task HandleListenerContextAsync(HttpListenerContext context, IHandler handler) {
        try {
            int status = await this.CreateTransportAsync(context, handler).ConfigureAwait(false);
            if (status != 0) {
                Trace.WriteLine(TraceLevel.Error, "Failed to create ws transport ", status);
                context.Response.StatusCode = status / 100;
                context.Response.OutputStream.Dispose();
            }
        } catch (Exception exception) {
            Trace.WriteLine(TraceLevel.Error, exception.ToString());

            context.Response.StatusCode = 500;
            context.Response.OutputStream.Dispose();
        }
    }

    private async Task<int> CreateTransportAsync(HttpListenerContext context, IHandler handler) {
        X509Certificate2 clientCertificate = null;

        if (this.Listener._SslSettings != null && this.Listener._SslSettings.ClientCertificateRequired) {
            clientCertificate = await context.Request.GetClientCertificateAsync().ConfigureAwait(false);
            if (clientCertificate == null) {
                return 40300;
            }
            if (this.Listener._SslSettings.RemoteCertificateValidationCallback != null) {
                SslPolicyErrors sslError = SslPolicyErrors.None;
                X509Chain chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = this.Listener._SslSettings.CheckCertificateRevocation ?
                    X509RevocationMode.Online : X509RevocationMode.NoCheck;
                chain.Build(clientCertificate);
                if (chain.ChainStatus.Length > 0) {
                    sslError = SslPolicyErrors.RemoteCertificateChainErrors;
                }

                bool success = this.Listener._SslSettings.RemoteCertificateValidationCallback(
                    this, clientCertificate, chain, sslError);
                if (!success) {
                    return 40301;
                }
            } else if (context.Request.ClientCertificateError != 0) {
                return 40302;
            }
        }

        IPrincipal principal = context.User;
        if (principal == null && clientCertificate != null) {
            principal = new GenericPrincipal(new X509Identity(clientCertificate), Array.Empty<string>());
        }

        string subProtocol = null;
        string[] subProtocols = context.Request.Headers.GetValues("Sec-WebSocket-Protocol");
        if (subProtocols is null) {
        } else {
            for (int i = 0; i < subProtocols.Length; i++) {
                if (subProtocols[i].Equals(WebSocketTransport.WebSocketSubProtocol) ||
                    subProtocols[i].Equals("AMQPWSB10")     // defined by the previous draft
                   ) {
                    subProtocol = subProtocols[i];
                    break;
                }
            }
        }

        if (subProtocol == null) {
            return 40003;
        }

        var wsContext = await context.AcceptWebSocketAsync(subProtocol).ConfigureAwait(false);
        if (handler != null && handler.CanHandle(EventId.WebSocketAccept)) {
            handler.Handle(Event.Create(EventId.WebSocketAccept, null, context: wsContext));
        }

        var wsTransport = new ListenerWebSocketTransport(wsContext.WebSocket, principal);
        await this.Listener.HandleTransportAsync(wsTransport, handler, wsContext.WebSocket).ConfigureAwait(false);

        return 0;
    }

    private async Task AcceptListenerContextLoop() {
        while (!this.closed) {
            try {
                HttpListenerContext context = await this._HttpListener.GetContextAsync().ConfigureAwait(false);

                IHandler handler = this.Listener.HandlerFactory?.Invoke(this.Listener);
                if (handler != null && handler.CanHandle(EventId.HttpAccept)) {
                    handler.Handle(Event.Create(EventId.HttpAccept, null, context: context));
                }

                var task = this.HandleListenerContextAsync(context, handler);
            } catch (Exception exception) {
                Trace.WriteLine(TraceLevel.Error, exception.ToString());
            }
        }
    }
}

public class ListenerWebSocket2Transport : WebSocketTransport, IAuthenticated {
    public ListenerWebSocket2Transport(WebSocket webSocket, IPrincipal principal)
        : base(webSocket) {
        this.Principal = principal;
    }

    public IPrincipal Principal {
        get;
        private set;
    }
}
