//  ------------------------------------------------------------------------------------
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

namespace Brimborium.OrleansAmqp.Listener;

/// <summary>
/// The connection listener accepts AMQP connections from an address.
/// </summary>
public class ConnectionListener : ConnectionFactoryBase {
    private readonly IContainer _Container;
    private readonly HashSet<Connection> _Connections;
    public readonly Address _Address;
    private TransportListener _Listener;
    public SslSettings _SslSettings;
    private SaslSettings _SaslSettings;
    private bool _Closed;

    private ConnectionListener(IContainer container) {
        this._Connections = new();
        this._Container = container;
    }

    /// <summary>
    /// Initializes the connection listener object.
    /// </summary>
    /// <param name="address">The address to listen on.</param>
    /// <param name="container">The IContainer implementation to handle client requests.</param>
    public ConnectionListener(string address, IContainer container)
        : this(new Address(address), container) {
    }

    /// <summary>
    /// Initializes the connection listener object.
    /// </summary>
    /// <param name="address">The address to listen on.</param>
    /// <param name="container">The IContainer implementation to handle client requests.</param>
    public ConnectionListener(Address address, IContainer container)
        : this(container) {
        this._Address = address;
    }

    /// <summary>
    /// Initializes the connection listener object.
    /// </summary>
    /// <param name="addressUri">The address Uri to listen on.</param>
    /// <param name="userInfo">The credentials for client authentication using SASL PLAIN mechanism.</param>
    /// <param name="container">The IContainer implementation to handle client requests.</param>
    /// <remarks>
    /// This constructor is deprecated. To set user info, use ConnectionListener.SASL.EnablePlainMechanism method
    /// after the connection listener is created.
    /// </remarks>
    [Obsolete("Use ConnectionListener(string, IContainer) instead.")]
    public ConnectionListener(Uri addressUri, string userInfo, IContainer container)
        : this(container) {
        this.SetUserInfo(userInfo);
        this._Address = new Address(addressUri.Host, addressUri.Port, null, null, addressUri.AbsolutePath, addressUri.Scheme);
    }

    /// <summary>
    /// Gets the AMQP container.
    /// </summary>
    public IContainer Container {
        get { return this._Container; }
    }

    /// <summary>
    /// Gets the address the listener is listening on.
    /// </summary>
    public Address Address {
        get { return this._Address; }
    }

    /// <summary>
    /// Gets the TLS/SSL settings on the listener.
    /// </summary>
    public SslSettings SSL {
        get {
            return this._SslSettings ??= new SslSettings();
        }
    }

    /// <summary>
    /// Gets the SASL settings on the listener.
    /// </summary>
    public SaslSettings SASL {
        get {
            return this._SaslSettings ??= new SaslSettings();
        }
    }

    /// <summary>
    /// Gets or sets a factory that creates a <see cref="IHandler"/> for an accepted connection.
    /// </summary>
    /// <remarks>The delegate is called once for each accepted transport. It allows for creating
    /// a handler per connection if needed (<see cref="Amqp.Connection.Handler"/>).</remarks>
    public Func<ConnectionListener, IHandler> HandlerFactory {
        get;
        set;
    }

    /// <summary>
    /// Opens the listener.
    /// </summary>
    public void Open() {
        ObjectDisposedException.ThrowIf(this._Closed, this);

        if (this._Container.CustomTransports.TryGetValue(this._Address.Scheme, out var provider)) {
            this._Listener = new CustomTransportListener(this, provider);
        } else if (this._Address.Scheme.Equals(Address.Amqp, StringComparison.OrdinalIgnoreCase)) {
            this._Listener = new TcpTransportListener(this, this._Address.Host, this._Address.Port);
        } else if (this._Address.Scheme.Equals(Address.Amqps, StringComparison.OrdinalIgnoreCase)) {
            this._Listener = new TlsTransportListener(this, this._Address.Host, this._Address.Port, this.GetServiceCertificate());
        } else if (this._Address.Scheme.Equals(WebSocketTransport.WebSockets, StringComparison.OrdinalIgnoreCase)) {
            this._Listener = new WebSocketTransportListener(this, "HTTP", this._Address.Host, _Address.Port, _Address.Path);
        } else if (this._Address.Scheme.Equals(WebSocketTransport.SecureWebSockets, StringComparison.OrdinalIgnoreCase)) {
            this._Listener = new WebSocketTransportListener(this, "HTTPS", this._Address.Host, _Address.Port, _Address.Path);
        } else {
            throw new NotSupportedException(this._Address.Scheme);
        }

        if (this._Address.User != null) {
            this.SASL.EnablePlainMechanism(this._Address.User, this._Address.Password);
        }

        this._Listener.Open();
    }

    /// <summary>
    /// Closes the listener.
    /// </summary>
    public void Close() {
        if (this._Listener == null) {
            this._Closed = true;
            return;
        }

        this._Listener.Close();

        var snapshot = new List<Connection>();
        lock (this._Connections) {
            this._Closed = true;
            snapshot.AddRange(this._Connections);
            _Connections.Clear();
        }

        foreach (var connection in snapshot) {
            connection.CloseInternal(0, new Error(ErrorCode.ConnectionForced) { Description = "listener close" });
        }
    }

    internal void SetUserInfo(string userInfo) {
        if (userInfo != null) {
            string[] a = userInfo.Split(':');
            this.SASL.EnablePlainMechanism(
                Uri.UnescapeDataString(a[0]),
                a.Length == 1 ? string.Empty : Uri.UnescapeDataString(a[1]));
        }
    }

    private X509Certificate2 GetServiceCertificate() {
        if (this._SslSettings != null && this._SslSettings.Certificate != null) {
            return this._SslSettings.Certificate;
        } else if (this._Container.ServiceCertificate != null) {
            return this._Container.ServiceCertificate;
        }

        throw new ArgumentNullException("certificate");
    }

    public async Task HandleTransportAsync(IAsyncTransport transport, IHandler handler, object context) {
        IPrincipal principal = null;
        if (this._SaslSettings != null) {
            ListenerSaslProfile profile = new ListenerSaslProfile(this);
            transport = await profile.NegotiateAsync(transport).ConfigureAwait(false);
            principal = profile.GetPrincipal();
        }

        var connection = new ListenerConnection(this, this._Address, handler, transport);
        if (principal == null) {
            // SASL principal preferred. If not present, check transport.
            if (transport is IAuthenticated authenticated) {
                principal = authenticated.Principal;
            }
        }

        connection.Principal = principal;

        bool shouldClose = false;
        lock (this._Connections) {
            if (!this._Closed) {
                connection.Closed += this.OnConnectionClosed;
                this._Connections.Add(connection);
            } else {
                shouldClose = true;
            }
        }

        if (shouldClose) {
            await connection.CloseAsync().ConfigureAwait(false);
        } else {
            if (handler != null && handler.CanHandle(EventId.ConnectionAccept)) {
                handler.Handle(Event.Create(EventId.ConnectionAccept, connection, context: context));
            }

            AsyncPump pump = new AsyncPump(this.BufferManager, transport);
            pump.Start(connection);
        }
    }

    private void OnConnectionClosed(IAmqpObject sender, Error error) {
        lock (this._Connections) {
            this._Connections.Remove((Connection)sender);
        }
    }

    /// <summary>
    /// Contains the TLS/SSL settings for a connection.
    /// </summary>
    public class SslSettings {
        internal SslSettings() {
            this.Protocols = ConnectionFactory.SslSettings.DefaultSslProtocols;
        }

        /// <summary>
        /// Gets or sets the listener certificate.
        /// </summary>
        public X509Certificate2 Certificate {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a a Boolean value that specifies whether the client must supply a certificate for authentication.
        /// </summary>
        public bool ClientCertificateRequired {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the supported protocols to use.
        /// </summary>
        public SslProtocols Protocols {
            get;
            set;
        }

        /// <summary>
        /// Specifies whether certificate revocation should be performed during handshake.
        /// </summary>
        public bool CheckCertificateRevocation {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a certificate validation callback to validate remote certificate.
        /// </summary>
        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback {
            get;
            set;
        }
    }

    /// <summary>
    /// Contains the SASL settings for a connection.
    /// </summary>
    public class SaslSettings {
        private readonly Dictionary<Symbol, SaslMechanism> _Mechanisms = new();

        internal SaslSettings() {
        }

        internal Symbol[] Mechanisms {
            get {
                return this._Mechanisms.Keys.ToArray();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating if SASL ANONYMOUS mechanism is enabled.
        /// </summary>
        public bool EnableAnonymousMechanism {
            get {
                return this._Mechanisms.ContainsKey(SaslProfile.AnonymousName);
            }

            set {
                if (value) {
                    this._Mechanisms[SaslProfile.AnonymousName] = SaslMechanism.Anonymous;
                } else {
                    this._Mechanisms.Remove(SaslProfile.AnonymousName);
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating if SASL EXTERNAL mechanism is enabled.
        /// </summary>
        public bool EnableExternalMechanism {
            get {
                return this._Mechanisms.ContainsKey(SaslProfile.ExternalName);
            }

            set {
                if (value) {
                    this._Mechanisms[SaslProfile.ExternalName] = SaslMechanism.External;
                } else {
                    this._Mechanisms.Remove(SaslProfile.ExternalName);
                }
            }
        }

        /// <summary>
        /// Enables SASL PLAIN mechanism.
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        public void EnablePlainMechanism(string userName, string password) {
            this._Mechanisms[SaslProfile.PlainName] = new SaslPlainMechanism(userName, password);
        }

        /// <summary>
        /// Enables a custom mechanism.
        /// </summary>
        /// <typeparam name="T">The type of <paramref name="profile"/>.</typeparam>
        /// <param name="mechanism">The mechanism.</param>
        /// <param name="profile">The <see cref="SaslProfile"/> that handles the negotiation for this mechanism.</param>
        public void EnableMechanism<T>(Symbol mechanism, T profile) where T : SaslProfile {
            this._Mechanisms.Add(mechanism, new CustomSaslMechanism<T>(mechanism, profile));
        }

        internal bool TryGetMechanism(Symbol name, out SaslMechanism mechanism) {
            return this._Mechanisms.TryGetValue(name, out mechanism);
        }
    }

    private class CustomSaslMechanism<T> : SaslMechanism where T : SaslProfile {
        private readonly Symbol _Mechanism;
        private readonly T _Profile;

        public CustomSaslMechanism(Symbol mechanism, T profile) {
            this._Mechanism = mechanism;
            this._Profile = profile;
        }

        public override string Name {
            get { return this._Mechanism; }
        }

        public override SaslProfile CreateProfile() {
            return this._Profile;
        }
    }

    private class ListenerSaslProfile : SaslProfile {
        private readonly ConnectionListener _Listener;
        private SaslProfile _InnerProfile;

        public ListenerSaslProfile(ConnectionListener listener)
            : base(string.Empty) {
            this._Listener = listener;
        }

        public IPrincipal GetPrincipal() {
            if (this._InnerProfile is IAuthenticated authenticated) {
                return authenticated.Principal;
            }

            return null;
        }

        public Task<IAsyncTransport> NegotiateAsync(IAsyncTransport transport) {
            return this.OpenAsync(null, this._Listener.BufferManager, transport, this.GetStartCommand(null));
        }

        protected override ITransport UpgradeTransport(ITransport transport) {
            if (this._InnerProfile != null) {
                return this._InnerProfile.UpgradeTransportInternal(transport);
            }

            return transport;
        }

        protected override DescribedList GetStartCommand(string hostname) {
            Symbol[] symbols = this._Listener._SaslSettings.Mechanisms;
            return new SaslMechanisms() { SaslServerMechanisms = symbols };
        }

        protected override DescribedList OnCommand(DescribedList command) {
            if (this._InnerProfile == null) {
                if (command.Descriptor.Code == Codec.SaslInit.Code) {
                    var init = (SaslInit)command;
                    if (!this._Listener._SaslSettings.TryGetMechanism(init.Mechanism, out var saslMechanism)) {
                        throw new AmqpException(ErrorCode.NotImplemented, init.Mechanism);
                    }

                    this._InnerProfile = saslMechanism.CreateProfile();
                } else {
                    throw new AmqpException(ErrorCode.NotAllowed, command.Descriptor.Name);
                }
            }

            return this._InnerProfile.OnCommandInternal(command);
        }
    }
}
public abstract class TransportListener {
    protected bool closed;

    protected ConnectionListener Listener {
        get;
        set;
    }

    public abstract void Open();

    public abstract void Close();
}

public class TcpTransportListener : TransportListener {
    private readonly Socket[] _ListenSockets;

    public TcpTransportListener(ConnectionListener listener, string host, int port) {
        this.Listener = listener;

        List<IPAddress> addresses = new();
        if (IPAddress.TryParse(host, out var ipAddress)) {
            addresses.Add(ipAddress);
        } else if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
              host.Equals(Environment.GetEnvironmentVariable("COMPUTERNAME"), StringComparison.OrdinalIgnoreCase) ||
              host.Equals(Brimborium.OrleansAmqp.TaskExtensions.GetHostEntryAsync(string.Empty).Result.HostName, StringComparison.OrdinalIgnoreCase)) {
            if (Socket.OSSupportsIPv4) {
                addresses.Add(IPAddress.Any);
            }

            if (Socket.OSSupportsIPv6) {
                addresses.Add(IPAddress.IPv6Any);
            }
        } else {
            addresses.AddRange(Brimborium.OrleansAmqp.TaskExtensions.GetHostAddressesAsync(host).GetAwaiter().GetResult());
        }

        this._ListenSockets = new Socket[addresses.Count];
        for (int i = 0; i < addresses.Count; ++i) {
            this._ListenSockets[i] = new Socket(addresses[i].AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            this._ListenSockets[i].SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
            this._ListenSockets[i].Bind(new IPEndPoint(addresses[i], port));
            this._ListenSockets[i].Listen(20);
        }
    }

    public override void Open() {
        for (int i = 0; i < this._ListenSockets.Length; i++) {
            var task = this.AcceptAsync(this._ListenSockets[i]);
            task.Ignore();
        }
    }

    public override void Close() {
        this.closed = true;
        if (this._ListenSockets != null) {
            for (int i = 0; i < this._ListenSockets.Length; i++) {
                this._ListenSockets[i]?.Dispose();
            }
        }
    }

    protected async Task HandleSocketAsync(Socket socket) {
        try {

            this.Listener.tcpSettings?.Configure(socket);

            IHandler handler = this.Listener.HandlerFactory?.Invoke(this.Listener);
            if (handler != null && handler.CanHandle(EventId.SocketAccept)) {
                handler.Handle(Event.Create(EventId.SocketAccept, null, context: socket));
            }

            IAsyncTransport transport = await this.CreateTransportAsync(socket, handler).ConfigureAwait(false);

            await this.Listener.HandleTransportAsync(transport, handler, socket).ConfigureAwait(false);
        } catch (Exception exception) {
            Trace.WriteLine(TraceLevel.Error, exception.ToString());
            socket.Dispose();
        }
    }

    protected virtual Task<IAsyncTransport> CreateTransportAsync(Socket socket, IHandler handler) {
        var tcs = new TaskCompletionSource<IAsyncTransport>();
        tcs.SetResult(new ListenerTcpTransport(socket, this.Listener.BufferManager));
        return tcs.Task;
    }

    private async Task AcceptAsync(Socket socket) {
        SocketAsyncEventArgs args = new SocketAsyncEventArgs();
        args.Completed += (s, a) => SocketExtensions.Complete(s, a, false, a.SocketError == SocketError.Success ? a.AcceptSocket : null);

        while (!this.closed) {
            try {
                args.AcceptSocket = null;
                Socket acceptSocket = await socket.AcceptAsync(args, SocketFlags.None).ConfigureAwait(false);
                if (acceptSocket != null) {
                    var task = this.HandleSocketAsync(acceptSocket);
                    task.Ignore();
                }
            } catch (ObjectDisposedException) {
                // listener is closed
            } catch (Exception exception) {
                Trace.WriteLine(TraceLevel.Warning, exception.ToString());
            }
        }

        args.Dispose();
        socket.Dispose();
    }
}

public class TlsTransportListener : TcpTransportListener {
    private readonly X509Certificate2 _Certificate;

    public TlsTransportListener(ConnectionListener listener, string host, int port, X509Certificate2 certificate)
        : base(listener, host, port) {
        this._Certificate = certificate;
    }

    protected override async Task<IAsyncTransport> CreateTransportAsync(Socket socket, IHandler handler) {
        SslStream sslStream;
        if (this.Listener._SslSettings == null) {
            sslStream = new SslStream(new NetworkStream(socket));
            await sslStream.AuthenticateAsServerAsync(this._Certificate).ConfigureAwait(false);
        } else {
            sslStream = new SslStream(new NetworkStream(socket), false,
                this.Listener._SslSettings.RemoteCertificateValidationCallback);

            await sslStream.AuthenticateAsServerAsync(this._Certificate, this.Listener._SslSettings.ClientCertificateRequired,
                this.Listener._SslSettings.Protocols, this.Listener._SslSettings.CheckCertificateRevocation).ConfigureAwait(false);
        }

        if (handler != null && handler.CanHandle(EventId.SslStreamAccept)) {
            handler.Handle(Event.Create(EventId.SslStreamAccept, null, context: sslStream));
        }

        return new ListenerTcpTransport(sslStream, this.Listener.BufferManager);
    }
}

public class CustomTransportListener : TransportListener {
    private readonly TransportProvider _Provider;

    public CustomTransportListener(ConnectionListener listener, TransportProvider provider) {
        this.Listener = listener;
        this._Provider = provider;
    }

    public override void Open() {
        this.AcceptAsync().Ignore();
    }

    public override void Close() {
        this._Provider.Dispose();
    }

    private async Task AcceptAsync() {
        while (!this.closed) {
            try {
                var transport = await this._Provider.CreateAsync(this.Listener._Address);
                IHandler handler = this.Listener.HandlerFactory?.Invoke(this.Listener);
                await this.Listener.HandleTransportAsync(transport, handler, null).ConfigureAwait(false);
            } catch (ObjectDisposedException) {
                // listener is closed
            } catch (Exception exception) {
                Trace.WriteLine(TraceLevel.Warning, exception.ToString());
            }
        }
    }
}

public class ListenerTcpTransport : TcpTransport, IAuthenticated {
    public ListenerTcpTransport(Socket socket, IBufferManager bufferManager)
        : base(bufferManager) {
        this.socketTransport = new TcpSocket(this, socket);
    }

    public ListenerTcpTransport(SslStream sslStream, IBufferManager bufferManager)
        : base(bufferManager) {
        this.socketTransport = new SslSocket(this, sslStream);
        if (sslStream.RemoteCertificate != null) {
            this.Principal = new GenericPrincipal(
                new X509Identity(sslStream.RemoteCertificate),
                Array.Empty<string>());
        }
    }

    public IPrincipal Principal {
        get;
        private set;
    }
}

public class WebSocketTransportListener : TransportListener {
    private readonly HttpListener _HttpListener;

    public WebSocketTransportListener(ConnectionListener listener, string scheme, string host, int port, string path) {
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

public class ListenerWebSocketTransport : WebSocketTransport, IAuthenticated {
    public ListenerWebSocketTransport(WebSocket webSocket, IPrincipal principal)
        : base(webSocket) {
        this.Principal = principal;
    }

    public IPrincipal Principal {
        get;
        private set;
    }
}
