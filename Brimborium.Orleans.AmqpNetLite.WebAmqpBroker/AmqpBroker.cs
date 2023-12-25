using global::Brimborium.OrleansAmqp;
using global::Brimborium.OrleansAmqp.Framing;
using global::Brimborium.OrleansAmqp.Listener;
using global::Brimborium.OrleansAmqp.Transactions;
using global::Brimborium.OrleansAmqp.Types;

using System.Security.Cryptography.X509Certificates;

namespace Brimborium.Orleans.AmqpNetLite.WebAmqpBroker;
public sealed class AmqpBroker : IContainer {
    public const uint BatchFormat = 0x80013700;
    private readonly X509Certificate2? _Certificate;
    private readonly Dictionary<string, TransportProvider> _CustomTransports = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TestQueue> _Queues = new();
    private readonly ConnectionListener[] _Listeners;
    private readonly TxnManager _TxnManager;
    private readonly bool _ImplicitQueue;
    private int _DynamidId;

    public AmqpBroker(IList<string> endpoints, string? userInfo, string? certValue, string[]? queues) {
        this._TxnManager = new TxnManager();
        if (queues != null) {
            foreach (string q in queues) {
                this._Queues.Add(q, new TestQueue(this, q));
            }
        } else {
            this._ImplicitQueue = true;
        }

        this._Certificate = certValue != null ? GetCertificate(certValue) : null;

        string containerId = "AMQPNetLite-TestBroker-" + Guid.NewGuid().ToString().Substring(0, 8);
        this._Listeners = new ConnectionListener[endpoints.Count];
        for (int i = 0; i < endpoints.Count; i++) {
            this._Listeners[i] = new ConnectionListener(endpoints[i], this);
            this._Listeners[i].AMQP.MaxSessionsPerConnection = 1000;
            this._Listeners[i].AMQP.ContainerId = containerId;
            this._Listeners[i].AMQP.IdleTimeout = 4 * 60 * 1000;
            this._Listeners[i].AMQP.MaxFrameSize = 64 * 1024;
            if (userInfo != null) {
                string[] a = userInfo.Split(':');
                this._Listeners[i].SASL.EnablePlainMechanism(
                    Uri.UnescapeDataString(a[0]),
                    a.Length == 1 ? string.Empty : Uri.UnescapeDataString(a[1]));
            } else {
                this._Listeners[i].SASL.EnableAnonymousMechanism = true;
            }
        }
    }

    public IDictionary<string, TransportProvider> CustomTransports {
        get { return this._CustomTransports; }
    }

    public void Start() {
        foreach (var listener in this._Listeners) {
            listener.Open();
        }
    }

    public void Stop() {
        foreach (var listener in this._Listeners) {
            try {
                listener.Close();
            } catch (Exception exception) {
                Trace.WriteLine(TraceLevel.Error, exception.ToString());
            }
        }
    }

    public void AddQueue(string queue) {
        lock (this._Queues) {
            this._Queues.Add(queue, new TestQueue(this, queue));
        }
    }

    public void RemoveQueue(string queue) {
        lock (this._Queues) {
            this._Queues.Remove(queue);
        }
    }

    private static X509Certificate2 GetCertificate(string certFindValue) {
        StoreLocation[] locations = [StoreLocation.LocalMachine, StoreLocation.CurrentUser];
        foreach (StoreLocation location in locations) {
            using (X509Store store = new X509Store(StoreName.My, location)) {

                store.Open(OpenFlags.OpenExistingOnly);

                X509Certificate2Collection collection = store.Certificates.Find(
                    X509FindType.FindBySubjectName,
                    certFindValue,
                    false);

                if (collection.Count == 0) {
                    collection = store.Certificates.Find(
                        X509FindType.FindByThumbprint,
                        certFindValue,
                        false);
                }

                if (collection.Count > 0) {
                    return collection[0];
                }
            }
        }

        throw new ArgumentException("No certificate can be found using the find value " + certFindValue);
    }

    X509Certificate2? IContainer.ServiceCertificate {
        get { return this._Certificate; }
    }

    Message IContainer.CreateMessage(ByteBuffer buffer) {
        return new BrokerMessage(buffer);
    }

    Link IContainer.CreateLink(ListenerConnection connection, ListenerSession session, Attach attach) {
        return new ListenerLink(session, attach);
    }

    bool IContainer.AttachLink(ListenerConnection connection, ListenerSession session, Link link, Attach attach) {
        Source source = (Source)attach.Source;
        Target? target = (Target?)attach.Target;
        bool dynamic = false;
        string? address = null;
        if (attach.Role) {
            address = source.Address;
            dynamic = source.Dynamic;
        } else {
            if (target != null) {
                address = target.Address;
                dynamic = target.Dynamic;
            } else if (attach.Target is Coordinator) {
                this._TxnManager.AddCoordinator((ListenerLink)link);
                return true;
            }
        }

        if (dynamic) {
            address = string.Format("$dynamic.{0}", Interlocked.Increment(ref this._DynamidId));
            if (attach.Role) {
                source.Dynamic = false;
                source.Address = address;
            } else {
                ArgumentNullException.ThrowIfNull(target);
                target.Address = address;
                target.Dynamic = false;
            }
        }

        ArgumentException.ThrowIfNullOrEmpty(address);
        TestQueue? queue;
        lock (this._Queues) {
            if (!this._Queues.TryGetValue(address, out queue)) {
                if (dynamic || (this._ImplicitQueue && !link.Name.StartsWith("$explicit:"))) {
                    queue = new TestQueue(this, address, !dynamic);
                    this._Queues.Add(address, queue);
                } else {
                    throw new AmqpException(ErrorCode.NotFound, string.Format("Node '{0}' not found", address));
                }
            }
        }

        if (attach.Role) {
            queue.CreateConsumer((ListenerLink)link);
        } else {
            queue.CreatePublisher((ListenerLink)link);
        }

        return true;
    }

    private sealed class BrokerMessage : Message {
        private ByteBuffer _Buffer;
        private int _MessageOffset;
        private uint _FailedCount;

        public BrokerMessage(ByteBuffer buffer) {
            this._Buffer = buffer;
            this._MessageOffset = buffer.Capacity - buffer.Length - buffer.Size;
            this._FailedCount = 0;
        }

        public ByteBuffer Buffer {
            get {
                this._Buffer.Seek(this._MessageOffset);
                this.CheckModified(this._Buffer);
                return this._Buffer;
            }
        }

        public object? LockedBy { get; set; }

        public LinkedListNode<BrokerMessage>? Node { get; set; }

        public void Unlock() {
            this.LockedBy = null;
        }

        public void Modify(bool failed, Fields annotations) {
            if (failed) {
                this._FailedCount++;
            }
            if (annotations != null) {
                this.MessageAnnotations ??= new MessageAnnotations();

                Merge(annotations, this.MessageAnnotations.Map);
            }
            this.LockedBy = null;
        }

        private void CheckModified(ByteBuffer oldBuf) {
            if (this._FailedCount == 0 && this.MessageAnnotations == null) {
                return;
            }
            ByteBuffer newBuf = new ByteBuffer(oldBuf.Size, true);
            Header? header = new Header();
            MessageAnnotations? annotations = this.MessageAnnotations;
            int offset = oldBuf.Offset;
            while (oldBuf.Length > 0) {
                offset = oldBuf.Offset;
                var described = (RestrictedDescribed)AmqpEncoder.ReadDescribed(oldBuf, AmqpEncoder.ReadFormatCode(_Buffer));
                if (described.Descriptor.Code == 0x70UL) {
                    header = (Header)described;
                    this.WriteHeader(ref header, newBuf);
                } else if (described.Descriptor.Code == 0x71UL) {
                    this.WriteHeader(ref header, newBuf);
                    AmqpBitConverter.WriteBytes(newBuf, oldBuf.Buffer, offset, oldBuf.Offset - offset);
                } else if (described.Descriptor.Code == 0x72UL) {
                    this.WriteHeader(ref header, newBuf);
                    WriteMessageAnnotations(ref annotations, (MessageAnnotations)described, newBuf);
                } else {
                    this.WriteHeader(ref header, newBuf);
                    WriteMessageAnnotations(ref annotations, null, newBuf);
                    AmqpBitConverter.WriteBytes(newBuf, oldBuf.Buffer, offset, oldBuf.WritePos - offset);
                    break;
                }
            }
            this._Buffer = newBuf;
            this._MessageOffset = 0;
        }

        private void WriteHeader(ref Header? header, ByteBuffer buffer) {
            if (header != null) {
                header.DeliveryCount += header.DeliveryCount + this._FailedCount;
                header.Encode(buffer);
                header = null;
            }
        }

        private static void WriteMessageAnnotations(ref MessageAnnotations? annotations, MessageAnnotations? current, ByteBuffer buffer) {
            if (annotations != null && current != null) {
                Merge(current.Map, annotations.Map);
                annotations.Encode(buffer);
                annotations = null;
            }
        }

        private static void Merge(Map source, Map dest) {
            foreach (var kvp in source) {
                dest[kvp.Key] = kvp.Value;
            }
        }
    }

    private sealed class TestQueue {
        private readonly AmqpBroker _Broker;
        private readonly string _Address;
        private readonly bool _IsImplicit;
        private readonly HashSet<Connection>? _Connections;
        private readonly LinkedList<BrokerMessage> _Messages = new();
        private readonly LinkedList<Consumer> _Waiters = new();
        private readonly Dictionary<int, Publisher> _Publishers = new();
        private readonly Dictionary<int, Consumer> _Consumers = new();
        private readonly object _SyncRoot = new();
        private int _CurrentId;

        public TestQueue(AmqpBroker broker, string address, bool isImplicit = false) {
            this._Broker = broker;
            this._Address = address;
            this._IsImplicit = isImplicit;
            this._Connections = isImplicit ? new() : null;
        }

        public void CreatePublisher(ListenerLink link) {
            int id = Interlocked.Increment(ref this._CurrentId);
            Publisher publisher = new Publisher(this, link, id);
            lock (this._SyncRoot) {
                this._Publishers.Add(id, publisher);
                this.OnClientConnected(link);
            }
        }

        public void CreateConsumer(ListenerLink link) {
            int id = Interlocked.Increment(ref this._CurrentId);
            Consumer consumer = new Consumer(this, link, id);
            lock (this._SyncRoot) {
                this._Consumers.Add(id, consumer);
                this.OnClientConnected(link);
            }
        }

        private void OnClientConnected(Link link) {
            var connections = this._Connections ?? new();
            if (this._IsImplicit && !connections.Contains(link.Session.Connection)) {
                connections.Add(link.Session.Connection);
                link.Session.Connection.Closed += OnConnectionClosed;
            }
        }

        private void OnConnectionClosed(IAmqpObject sender, Error error) {
            if (this._IsImplicit) {
                lock (this._SyncRoot) {
                    var connections = this._Connections ?? new();
                    connections.Remove((Connection)sender);
                    if (connections.Count == 0) {
                        this._Broker.RemoveQueue(this._Address);
                    }
                }
            }
        }

        private Consumer? GetConsumerWithLock(Consumer? exclude) {
            Consumer? consumer = null;
            var node = this._Waiters.First;
            while (node != null) {
                consumer = node.Value;
                if (consumer.Credit == 0) {
                    this._Waiters.RemoveFirst();
                    consumer = null;
                } else if (consumer != exclude) {
                    consumer.Credit--;
                    if (consumer.Credit == 0) {
                        this._Waiters.RemoveFirst();
                    }

                    break;
                }

                node = node.Next;
            }

            return consumer;
        }

        private void Enqueue(BrokerMessage message) {
            LinkedListNode<BrokerMessage>? nodeToDeliver = null;
            if (message.Format == BatchFormat) {
                var batch = Message.Decode(message.Buffer);
                if (batch.BodySection is DataList dataList) {
                    for (int i = 0; i < dataList.Count; i++) {
                        var msg = new BrokerMessage(dataList[i].Buffer);
                        lock (this._SyncRoot) {
                            msg.Node = this._Messages.AddLast(msg);
                            if (nodeToDeliver == null) {
                                nodeToDeliver = msg.Node;
                            }
                        }
                    }
                } else {
                    if (batch.BodySection is Data data) {
                        var msg = new BrokerMessage(data.Buffer);
                        lock (this._SyncRoot) {
                            nodeToDeliver = msg.Node = this._Messages.AddLast(msg);
                        }
                    } else {
                        // Ignore it for now
                        return;
                    }
                }
            } else {
                // clone the message as the incoming one is associated with a delivery already
                BrokerMessage clone = new BrokerMessage(message.Buffer);
                lock (this._SyncRoot) {
                    nodeToDeliver = clone.Node = this._Messages.AddLast(clone);
                }
            }
            this.Deliver(nodeToDeliver);
        }

        private void Deliver(LinkedListNode<BrokerMessage>? node) {
            Consumer? consumer = null;
            BrokerMessage? message = null;
            while (node != null) {
                lock (this._SyncRoot) {
                    if (consumer == null || consumer.Credit == 0) {
                        consumer = this.GetConsumerWithLock(null);
                        if (consumer == null) {
                            return;
                        }
                    }

                    if (node.List == null) {
                        node = this._Messages.First;
                        continue;
                    }

                    var next = node.Next;
                    message = node.Value;
                    if (message.LockedBy == null) {
                        if (consumer.SettleOnSend) {
                            this._Messages.Remove(node);
                        } else {
                            message.LockedBy = consumer;
                        }
                    } else {
                        message = null;
                    }

                    node = next;
                }

                if (consumer != null && message != null) {
                    consumer.Signal(message);
                }
            }
        }

        private void Dequeue(Consumer consumer, int credit, bool drain) {
            List<BrokerMessage> messageList = [];
            lock (this._SyncRoot) {
                consumer.Credit += credit;

                var current = this._Messages.First;
                while (current != null) {
                    if (current.Value.LockedBy == null) {
                        messageList.Add(current.Value);
                        if (consumer.SettleOnSend) {
                            var temp = current;
                            current = current.Next;
                            this._Messages.Remove(temp);
                        } else {
                            current.Value.LockedBy = consumer;
                            current = current.Next;
                        }

                        consumer.Credit--;
                        if (consumer.Credit == 0) {
                            break;
                        }
                    } else {
                        current = current.Next;
                    }
                }

                if (drain) {
                    consumer.Credit = 0;
                } else if (consumer.Credit > 0) {
                    this._Waiters.AddLast(consumer);
                }
            }

            foreach (var message in messageList) {
                consumer.Signal(message);
            }
        }

        private void Dequeue(BrokerMessage message) {
            lock (this._SyncRoot) {
                if (message.Node is not null) {
                    this._Messages.Remove(message.Node);
                }
            }
        }

        private void Unlock(BrokerMessage message, Consumer? exclude) {
            Consumer? consumer = null;
            lock (this._SyncRoot) {
                message.Unlock();
                consumer = this.GetConsumerWithLock(exclude);
                if (consumer != null) {
                    if (consumer.SettleOnSend) {
                        if (message.Node is not null) {
                            this._Messages.Remove(message.Node);
                        }
                    } else {
                        message.LockedBy = consumer;
                    }
                }
            }

            if (consumer != null) {
                consumer.Signal(message);
            }
        }

        private void OnPublisherClosed(int id, Publisher publisher) {
            lock (this._SyncRoot) {
                this._Publishers.Remove(id);
            }
        }

        private void OnConsumerClosed(int id, Consumer consumer) {
            lock (this._SyncRoot) {
                this._Consumers.Remove(id);
                this._Waiters.Remove(consumer);
                var node = this._Messages.First;
                while (node != null) {
                    var temp = node;
                    node = node.Next;
                    if (temp.Value.LockedBy == consumer) {
                        this.Unlock(temp.Value, consumer);
                    }
                }
            }
        }

        private sealed class Publisher {
            private static readonly Action<ListenerLink, Message, DeliveryState, object> _OnMessage = OnMessage;
            private static readonly Action<Message, bool, object> _OnDischarge = OnDischarge;
            private readonly TestQueue _Queue;
            private readonly ListenerLink _Link;
            private readonly int _Id;

            public Publisher(TestQueue queue, ListenerLink link, int id) {
                this._Queue = queue;
                this._Link = link;
                this._Id = id;

                link.Closed += this.OnLinkClosed;
                link.InitializeReceiver(200, _OnMessage, this);
            }

            private void OnLinkClosed(IAmqpObject sender, Error error) {
                this._Queue.OnPublisherClosed(this._Id, this);
            }

            private static void OnMessage(ListenerLink link, Message message, DeliveryState deliveryState, object state) {
                var thisPtr = (Publisher)state;
                string? errorCondition = null;
                if (message.ApplicationProperties != null &&
                    (errorCondition = (string)message.ApplicationProperties["errorcondition"]) != null) {
                    link.DisposeMessage(
                        message,
                        new Rejected() { Error = new Error(errorCondition) { Description = "message was rejected" } },
                        true);
                } else {
                    if (deliveryState is TransactionalState txnState) {
                        Transaction txn = thisPtr._Queue._Broker._TxnManager.GetTransaction(txnState.TxnId);
                        txn.AddOperation(message, _OnDischarge, thisPtr);
                        txnState.Outcome = new Accepted();
                    } else {
                        thisPtr._Queue.Enqueue((BrokerMessage)message);
                        deliveryState = new Accepted();
                    }

                    thisPtr._Link.DisposeMessage(message, deliveryState, true);
                }
            }

            private static void OnDischarge(Message message, bool fail, object state) {
                if (!fail) {
                    var thisPtr = (Publisher)state;
                    thisPtr._Queue.Enqueue((BrokerMessage)message);
                }
            }
        }

        private sealed class Consumer {
            private static readonly Action<int, Fields, object> _OnCredit = OnCredit;
            private static readonly Action<Message, DeliveryState, bool, object> _OnDispose = OnDispose;
            private static readonly Action<Message, bool, object> _OnDischarge = OnDischarge;
            private readonly TestQueue _Queue;
            private readonly ListenerLink _Link;
            private readonly int _Id;

            public Consumer(TestQueue queue, ListenerLink link, int id) {
                this._Queue = queue;
                this._Link = link;
                this._Id = id;

                link.Closed += this.OnLinkClosed;
                link.InitializeSender(_OnCredit, _OnDispose, this);
            }

            public bool SettleOnSend { get { return this._Link.SettleOnSend; } }

            public int Credit { get; set; }

            public void Signal(BrokerMessage message) {
                this._Link.SendMessage(message, message.Buffer);
            }

            private void OnLinkClosed(IAmqpObject sender, Error error) {
                this.Credit = 0;
                this._Queue.OnConsumerClosed(this._Id, this);
            }

            private static void OnCredit(int credit, Fields properties, object state) {
                var consumer = (Consumer)state;
                consumer._Queue.Dequeue(consumer, credit, consumer._Link.IsDraining);
                if (consumer._Link.IsDraining) {
                    consumer.Credit = 0;
                    consumer._Link.CompleteDrain();
                }
            }

            private static void OnDispose(Message message, DeliveryState deliveryState, bool settled, object state) {
                var thisPtr = (Consumer)state;
                if (deliveryState is TransactionalState transactionalState) {
                    Transaction txn = thisPtr._Queue._Broker._TxnManager.GetTransaction(transactionalState.TxnId);
                    txn.AddOperation(message, _OnDischarge, thisPtr);
                } else {
                    if (deliveryState is Released) {
                        thisPtr._Queue.Unlock((BrokerMessage)message, null);
                    } else if (deliveryState is Modified modified) {
                        ((BrokerMessage)message).Modify(modified.DeliveryFailed, modified.MessageAnnotations);
                        thisPtr._Queue.Unlock((BrokerMessage)message, modified.UndeliverableHere ? thisPtr : null);
                    } else {
                        thisPtr._Queue.Dequeue((BrokerMessage)message);
                    }
                }
            }

            private static void OnDischarge(Message message, bool fail, object state) {
                if (!fail) {
                    var thisPtr = (Consumer)state;
                    thisPtr._Queue.Dequeue((BrokerMessage)message);
                }
            }
        }
    }

    private sealed class TxnOperation {
        public required Message Message;

        public required Action<Message, bool, object> Callback;

        public required object State;
    }

    private sealed class Transaction {
        private readonly Queue<TxnOperation> _Operations;

        public Transaction() {
            this._Operations = new Queue<TxnOperation>();
        }

        public int Id { get; set; }

        public void AddOperation(Message message, Action<Message, bool, object> callback, object state) {
            var op = new TxnOperation() { Message = message, Callback = callback, State = state };
            lock (this._Operations) {
                this._Operations.Enqueue(op);
            }
        }

        public void Discharge(bool fail) {
            foreach (var op in this._Operations) {
                op.Callback(op.Message, fail, op.State);
            }
        }
    }

    private sealed class TxnManager {
        private readonly HashSet<ListenerLink> _Coordinators = new();
        private readonly Dictionary<int, Transaction> _Transactions = new();
        private int _Id;

        public TxnManager() {
        }

        public void AddCoordinator(ListenerLink link) {
            lock (this._Coordinators) {
                this._Coordinators.Add(link);
            }

            link.Closed += (o, e) => this.RemoveCoordinator((ListenerLink)o);
            link.InitializeReceiver(100, OnMessage, this);
        }

        public Transaction GetTransaction(byte[] txnId) {
            int id = BitConverter.ToInt32(txnId, 0);
            return this._Transactions[id];
        }

        private void RemoveCoordinator(ListenerLink link) {
            lock (this._Coordinators) {
                this._Coordinators.Remove(link);
            }
        }

        private static void OnMessage(ListenerLink link, Message message, DeliveryState deliveryState, object state) {
            var thisPtr = (TxnManager)state;
            object? body;
            try {
                body = Message.Decode(((BrokerMessage)message).Buffer).Body;
            } catch (Exception exception) {
                Trace.WriteLine(TraceLevel.Error, exception.Message);
                link.DisposeMessage(
                    message,
                    new Rejected() { Error = new Error(ErrorCode.DecodeError) { Description = "Cannot decode txn message" } },
                    true);

                return;
            }

            if (body is Declare) {
                int txnId = thisPtr.CreateTransaction();
                var outcome = new Declared() { TxnId = BitConverter.GetBytes(txnId) };
                link.DisposeMessage(message, outcome, true);
            } else if (body is Discharge discharge) {
                int txnId = BitConverter.ToInt32(discharge.TxnId, 0);
                if (thisPtr._Transactions.TryGetValue(txnId, out var txn)) {
                    lock (thisPtr._Transactions) {
                        thisPtr._Transactions.Remove(txnId);
                    }

                    txn.Discharge(discharge.Fail);
                    link.DisposeMessage(message, new Accepted(), true);
                } else {
                    link.DisposeMessage(
                        message,
                        new Rejected() { Error = new Error(ErrorCode.NotFound) { Description = "Transaction not found" } },
                        true);
                }
            } else {
                link.DisposeMessage(
                    message,
                    new Rejected() { Error = new Error(ErrorCode.NotImplemented) { Description = "Unsupported message body" } },
                    true);
            }
        }

        private int CreateTransaction() {
            Transaction txn = new Transaction() { Id = Interlocked.Increment(ref this._Id) };
            lock (this._Transactions) {
                this._Transactions.Add(txn.Id, txn);
                return txn.Id;
            }
        }
    }
}
