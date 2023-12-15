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

namespace Brimborium.OrleansAmqp;

/// <summary>
/// The ReceiverLink class represents a link that accepts incoming messages.
/// </summary>
public partial class ReceiverLink : Link {
    private const int DefaultCredit = 200;
    // flow control
    private SequenceNumber _DeliveryCount;
    private int _TotalCredit;          // total credit set by app or the default
    private bool _Drain;               // a drain cycle is in progress
    private int _Pending;              // queued or being processed by application
    private int _Credit;               // remaining credit
    private int _Restored;             // processed by the application
    private int _FlowThreshold;        // auto restore threshold for a flow

    // received messages queue
    private readonly LinkedList _ReceivedMessages;
    private MessageDelivery _DeliveryCurrent;

    // pending receivers
    private readonly LinkedList _WaiterList;
    private MessageCallback _OnMessage;

    /// <summary>
    /// Initializes a receiver link.
    /// </summary>
    /// <param name="session">The session within which to create the link.</param>
    /// <param name="name">The link name.</param>
    /// <param name="address">The node address.</param>
    public ReceiverLink(Session session, string name, string address)
        : this(session, name, new Source() { Address = address }, null) {
    }

    /// <summary>
    /// Initializes a receiver link.
    /// </summary>
    /// <param name="session">The session within which to create the link.</param>
    /// <param name="name">The link name.</param>
    /// <param name="source">The source on attach that specifies the message source.</param>
    /// <param name="onAttached">The callback to invoke when an attach is received from peer.</param>
    public ReceiverLink(Session session, string name, Source source, OnAttached onAttached)
        : this(session, name, new Attach() { Source = source, Target = new Target() }, onAttached) {
    }

    /// <summary>
    /// Initializes a receiver link.
    /// </summary>
    /// <param name="session">The session within which to create the link.</param>
    /// <param name="name">The link name.</param>
    /// <param name="attach">The attach frame to send for this link.</param>
    /// <param name="onAttached">The callback to invoke when an attach is received from peer.</param>
    public ReceiverLink(Session session, string name, Attach attach, OnAttached onAttached)
        : base(session, name, onAttached) {
        this._TotalCredit = -1;
        this._ReceivedMessages = new LinkedList();
        this._WaiterList = new LinkedList();
        this.SendAttach(true, 0, attach);
    }

    /// <summary>
    /// Starts the message pump.
    /// </summary>
    /// <param name="credit">The link credit to issue. See <seealso cref="SetCredit(int, bool)"/> for more details.</param>
    /// <param name="onMessage">If specified, the callback to invoke when messages are received.
    /// If not specified, call Receive method to get the messages.</param>
    public void Start(int credit, MessageCallback onMessage = null) {
        this._OnMessage = onMessage;
        this.SetCredit(credit, true);
    }

    /// <summary>
    /// Sets a credit on the link. The credit controls how many messages the peer can send.
    /// </summary>
    /// <param name="credit">The new link credit.</param>
    /// <param name="autoRestore">If true, this method is the same as SetCredit(credit, CreditMode.Auto);
    /// if false, it is the same as SetCredit(credit, CreditMode.Manual).</param>
    public void SetCredit(int credit, bool autoRestore = true) {
        this.SetCredit(credit, autoRestore ? CreditMode.Auto : CreditMode.Manual);
    }

    /// <summary>
    /// Sets a credit on the link and the credit management mode.
    /// </summary>
    /// <param name="credit">The new link credit.</param>
    /// <param name="creditMode">The credit management mode.</param>
    /// <param name="flowThreshold">If credit mode is Auto, it is the threshold of restored
    /// credits that trigers a flow; ignored otherwise.</param>
    /// <remarks>
    /// The receiver link has a default link credit (200). If the default value is not optimal,
    /// application should call this method once after the receiver link is created.
    /// In Auto credit mode, the <paramref name="credit"/> parameter defines the total credits
    /// of the link which is also the total number messages the remote peer can send. 
    /// The link keeps track of acknowledged messages and triggers a flow
    /// when a threshold is reached. The default threshold is half of <see cref="_Credit"/>. Application
    /// acknowledges a message by calling <see cref="Accept(Message)"/>, <see cref="Reject(Message, Error)"/>,
    /// <see cref="Release(Message)"/> or <see cref="Modify(Message, bool, bool, Fields)"/> method.
    /// In Manual credit mode, the <paramref name="credit"/> parameter defines the extra credits
    /// of the link which is the additional messages the remote peer can send.
    /// Please note the following.
    /// 1. In Auto mode, calling this method multiple times with different credits is allowed but not recommended.
    ///    Application may do this if, for example, it needs to control local queue depth based on resource usage.
    ///    If credit is reduced, the link maintains a buffer so incoming messages are still allowed.
    /// 2. The creditMode should not be changed after it is initially set.
    /// 3. To stop a receiver link, set <paramref name="credit"/> to 0. However application should expect
    ///    in-flight messages to come as a result of the previous credit. It is recommended to use the
    ///    Drain mode if the application wishes to stop the messages after a given credit is used.
    /// 4. In drain credit mode, if a drain cycle is still in progress, the call simply returns without
    ///    sending a flow. Application is expected to keep calling <see cref="Receive()"/> in a loop
    ///    until all messages are received or a null message is returned.
    /// 5. In manual credit mode, application is responsible for keeping track of processed messages
    ///    and issue more credits when certain conditions are met. 
    /// </remarks>
    public void SetCredit(int credit, CreditMode creditMode, int flowThreshold = -1) {
        lock (this.ThisLock) {
            if (this.IsDetaching) {
                return;
            }

            if (this._TotalCredit < 0) {
                this._TotalCredit = 0;
            }

            var sendFlow = false;
            if (creditMode == CreditMode.Drain) {
                if (!this._Drain) {
                    // start a drain cycle.
                    this._Pending = 0;
                    this._Restored = 0;
                    this._Drain = true;
                    this._Credit = credit;
                    this._FlowThreshold = -1;
                    sendFlow = true;
                }
            } else if (creditMode == CreditMode.Manual) {
                this._Drain = false;
                this._Pending = 0;
                this._Restored = 0;
                this._FlowThreshold = -1;
                this._Credit += credit;
                sendFlow = true;
            } else {
                this._Drain = false;
                this._FlowThreshold = flowThreshold >= 0 ? flowThreshold : credit / 2;
                // Only change remaining credit if total credit was increased, to allow
                // accepting incoming messages. If total credit is reduced, only update 
                // total so credit will be later auto-restored to the new limit.
                int delta = credit - this._TotalCredit + this._Restored;
                if (delta > 0) {
                    this._Credit += delta;
                    this._Restored = 0;
                    sendFlow = true;
                }
            }

            this._TotalCredit = credit;
            if (sendFlow) {
                this.SendFlow(this._DeliveryCount, (uint)this._Credit, this._Drain);
            }
        }
    }

    /// <summary>
    /// Receives a message. The call is blocked until a message is available or after a default wait time.
    /// </summary>
    /// <returns>A Message object if available; otherwise a null value.</returns>
    public Message Receive() {
        return this.ReceiveInternal(null, AmqpObject.DefaultTimeout);
    }

    /// <summary>
    /// Receives a message. The call is blocked until a message is available or the timeout duration expires.
    /// </summary>
    /// <param name="timeout">The time to wait for a message.</param>
    /// <returns>A Message object if available; otherwise a null value.</returns>
    /// <remarks>
    /// Use TimeSpan.MaxValue or Timeout.InfiniteTimeSpan to wait infinitely. If TimeSpan.Zero is supplied,
    /// the call returns immediately.
    /// </remarks>
    public Message Receive(TimeSpan timeout) {
        int waitTime = timeout == TimeSpan.MaxValue ? -1 : (int)(timeout.Ticks / 10000);
        if (timeout == Timeout.InfiniteTimeSpan) {
            waitTime = -1;
        }
        return this.ReceiveInternal(null, waitTime);
    }

    /// <summary>
    /// Accepts a message. It sends an accepted outcome to the peer.
    /// </summary>
    /// <param name="message">The message to accept.</param>
    public void Accept(Message message) {
        this.Accept(message.GetDelivery());
    }

    /// <summary>
    /// Accepts a message. It sends an accepted outcome to the peer.
    /// </summary>
    /// <param name="messageDelivery">Delivery information of a message to accept.</param>
    public void Accept(MessageDelivery messageDelivery) {
        this.ThrowIfDetaching("Accept");
        this.UpdateDelivery(messageDelivery, new Accepted(), null);
    }

    /// <summary>
    /// Releases a message. It sends a released outcome to the peer.
    /// </summary>
    /// <param name="message">The message to release.</param>
    public void Release(Message message) {
        this.Release(message.GetDelivery());
    }

    /// <summary>
    /// Releases a message. It sends a released outcome to the peer.
    /// </summary>
    /// <param name="messageDelivery">Delivery information of a message to release.</param>
    public void Release(MessageDelivery messageDelivery) {
        this.ThrowIfDetaching("Accept");
        this.UpdateDelivery(messageDelivery, new Released(), null);
    }

    /// <summary>
    /// Rejects a message. It sends a rejected outcome to the peer.
    /// </summary>
    /// <param name="message">The message to reject.</param>
    /// <param name="error">The error, if any, for the rejection.</param>
    public void Reject(Message message, Error error = null) {
        this.Reject(message.GetDelivery(), error);
    }

    /// <summary>
    /// Rejects a message. It sends a rejected outcome to the peer.
    /// </summary>
    /// <param name="messageDelivery">Delivery information of a message to reject.</param>
    /// <param name="error">The error, if any, for the rejection.</param>
    public void Reject(MessageDelivery messageDelivery, Error error = null) {
        this.ThrowIfDetaching("Reject");
        this.UpdateDelivery(messageDelivery, new Rejected() { Error = error }, null);
    }

    /// <summary>
    /// Modifies a message. It sends a modified outcome to the peer.
    /// </summary>
    /// <param name="message">The message to modify.</param>
    /// <param name="deliveryFailed">If set, the message's delivery-count is incremented.</param>
    /// <param name="undeliverableHere">Indicates if the message should not be redelivered to this endpoint.</param>
    /// <param name="messageAnnotations">Annotations to be combined with the current message annotations.</param>
    public void Modify(Message message, bool deliveryFailed, bool undeliverableHere = false, Fields messageAnnotations = null) {
        this.Modify(message.GetDelivery(), deliveryFailed, undeliverableHere, messageAnnotations);
    }

    /// <summary>
    /// Modifies a message. It sends a modified outcome to the peer.
    /// </summary>
    /// <param name="messageDelivery">Delivery information of a message to reject.</param>
    /// <param name="deliveryFailed">If set, the message's delivery-count is incremented.</param>
    /// <param name="undeliverableHere">Indicates if the message should not be redelivered to this endpoint.</param>
    /// <param name="messageAnnotations">Annotations to be combined with the current message annotations.</param>
    public void Modify(MessageDelivery messageDelivery, bool deliveryFailed, bool undeliverableHere = false, Fields messageAnnotations = null) {
        this.ThrowIfDetaching("Modify");
        this.UpdateDelivery(messageDelivery, new Modified() {
            DeliveryFailed = deliveryFailed,
            UndeliverableHere = undeliverableHere,
            MessageAnnotations = messageAnnotations
        },
        null);
    }

    /// <summary>
    /// Completes a received message. It settles the delivery and sends
    /// a disposition with the delivery state to the remote peer.
    /// </summary>
    /// <param name="message">The message to complete.</param>
    /// <param name="deliveryState">An <see cref="Outcome"/> or a TransactionalState.</param>
    /// <remarks>This method is not transaction aware. It should be used to bypass
    /// transaction context look up when transactions are not used at all, or
    /// to manage AMQP transactions directly by providing a TransactionalState to
    /// <paramref name="deliveryState"/>.</remarks>
    public void Complete(Message message, DeliveryState deliveryState) {
        this.Complete(message.GetDelivery(), deliveryState);
    }

    /// <summary>
    /// Completes a received message. It settles the delivery and sends
    /// a disposition with the delivery state to the remote peer.
    /// </summary>
    /// <param name="messageDelivery">Delivery information of a message to reject.</param>
    /// <param name="deliveryState">An <see cref="Outcome"/> or a TransactionalState.</param>
    /// <remarks>This method is not transaction aware. It should be used to bypass
    /// transaction context look up when transactions are not used at all, or
    /// to manage AMQP transactions directly by providing a TransactionalState to
    /// <paramref name="deliveryState"/>.</remarks>
    public void Complete(MessageDelivery messageDelivery, DeliveryState deliveryState) {
        this.UpdateDelivery(messageDelivery, null, deliveryState);
    }

    internal override void OnFlow(Flow flow) {
        lock (this.ThisLock) {
            if (this._Drain) {
                this._Drain = flow.Drain;
                this._DeliveryCount = flow.DeliveryCount;
                this._Credit = Math.Min(0, (int)flow.LinkCredit);
            }
        }
    }

    internal override void OnTransfer(Delivery delivery, Transfer transfer, ByteBuffer buffer) {
        if (delivery == null) {
            delivery = this._DeliveryCurrent.Delivery;
            AmqpBitConverter.WriteBytes(delivery.Buffer, buffer.Buffer, buffer.Offset, buffer.Length);
        } else {
            this._DeliveryCurrent = new MessageDelivery(delivery, transfer.MessageFormat);
            buffer.AddReference();
            delivery.Buffer = buffer;
            lock (this.ThisLock) {
                this.OnDelivery(transfer.DeliveryId);
            }
        }

        if (!transfer.More) {
            delivery.Message = Message.Decode(delivery.Buffer);
            delivery.Message.Format = this._DeliveryCurrent.MessageFormat;
            this._DeliveryCurrent = MessageDelivery.None;

            IHandler handler = this.Session.Connection.Handler;
            if (handler != null && handler.CanHandle(EventId.ReceiveDelivery)) {
                handler.Handle(Event.Create(EventId.ReceiveDelivery, this.Session.Connection, this.Session, this, context: delivery));
            }

            Waiter waiter;
            MessageCallback callback = this._OnMessage;
            lock (this.ThisLock) {
                waiter = (Waiter)this._WaiterList.First;
                if (waiter != null) {
                    this._WaiterList.Remove(waiter);
                } else if (callback == null) {
                    this._ReceivedMessages.Add(new MessageNode() { Message = delivery.Message });
                    return;
                }
            }

            while (waiter != null) {
                if (waiter.Signal(delivery.Message)) {
                    return;
                }

                lock (this.ThisLock) {
                    waiter = (Waiter)this._WaiterList.First;
                    if (waiter != null) {
                        this._WaiterList.Remove(waiter);
                    } else if (callback == null) {
                        this._ReceivedMessages.Add(new MessageNode() { Message = delivery.Message });
                        return;
                    }
                }
            }

            AssertException.Assert(waiter == null, "waiter must be null now");
            AssertException.Assert(callback != null, "callback must not be null now");
            ArgumentNullException.ThrowIfNull(callback);
            callback(this, delivery.Message);
        }
    }

    internal override void OnAttach(uint remoteHandle, Attach attach) {
        base.OnAttach(remoteHandle, attach);
        this._DeliveryCount = attach.InitialDeliveryCount;
    }

    internal override void OnDeliveryStateChanged(Delivery delivery) {
    }

    /// <summary>
    /// Closes the receiver link.
    /// </summary>
    /// <param name="error">The error for the closure.</param>
    /// <returns></returns>
    protected override bool OnClose(Error error) {
        this.OnAbort(error);

        return base.OnClose(error);
    }

    /// <summary>
    /// Aborts the receiver link.
    /// </summary>
    /// <param name="error">The error for the abort.</param>
    protected override void OnAbort(Error error) {
        Waiter waiter;
        lock (this.ThisLock) {
            waiter = (Waiter)this._WaiterList.Clear();
        }

        while (waiter != null) {
            waiter.Signal(null);
            waiter = (Waiter)waiter.Next;
        }
    }

    internal Message ReceiveInternal(MessageCallback callback, int timeout = 60000) {
        Waiter waiter = null;
        lock (this.ThisLock) {
            this.ThrowIfDetaching("Receive");
            MessageNode first = (MessageNode)this._ReceivedMessages.First;
            if (first != null) {
                this._ReceivedMessages.Remove(first);
                return first.Message;
            }

            if (timeout != 0) {
                waiter = callback == null ? (Waiter)new SyncWaiter() : new AsyncWaiter(this, callback);
                //waiter = new SyncWaiter();
                this._WaiterList.Add(waiter);
            }
        }

        // send credit after waiter creation to avoid race condition
        if (this._TotalCredit < 0) {
            this.SetCredit(DefaultCredit, true);
        }

        if (waiter is null) {
            return null;
        }

        Message message = null;
        message = waiter.Wait(timeout);
        if (this.Error != null) {
            throw new AmqpException(this.Error);
        }

        return message;
    }

    // deliveryState overwrites outcome
    private void UpdateDelivery(MessageDelivery messageDelivery, Outcome outcome, DeliveryState deliveryState) {
        Delivery delivery = messageDelivery.Delivery;
        if (delivery == null) {
            throw new InvalidOperationException("Message was not delivered yet.");
        }

        if (delivery == null || delivery.Link != this) {
            throw new InvalidOperationException("Message was not received by this link.");
        }

        if (!delivery.Settled) {
            DeliveryState state = outcome != null ? outcome : deliveryState;
            bool settled = true;
            if (outcome != null) {
                var txnState = Brimborium.OrleansAmqp.Transactions.ResourceManager.GetTransactionalStateAsync(this).Result;
                if (txnState != null) {
                    txnState.Outcome = outcome;
                    state = txnState;
                    settled = false;
                }
            }

            this.Session.DisposeDelivery(true, delivery, state, settled);
        }

        lock (this.ThisLock) {
            this._Restored++;
            this._Pending--;
            if (this._FlowThreshold >= 0 && this._Restored >= this._FlowThreshold) {
                // total credit may be reduced. restore to what is allowed
                int delta = Math.Min(this._Restored, this._TotalCredit - this._Credit - this._Pending);
                if (delta > 0) {
                    this._Credit += delta;
                    this.SendFlow(this._DeliveryCount, (uint)this._Credit, false);
                }

                this._Restored = 0;
            }
        }
    }

    private void OnDelivery(SequenceNumber deliveryId) {
        // called with lock held
        if (this._Credit <= 0) {
            throw new AmqpException(ErrorCode.TransferLimitExceeded,
                Fx.Format(SRAmqp.DeliveryLimitExceeded, deliveryId));
        }

        this._DeliveryCount++;
        this._Pending++;
        this._Credit--;
        if (this._Drain && this._Credit == 0) {
            this._Drain = false;
        }
    }

    private sealed class MessageNode : INode {
        public Message Message { get; set; }

        public INode Previous { get; set; }

        public INode Next { get; set; }
    }

    private abstract class Waiter : INode {
        public INode Previous { get; set; }

        public INode Next { get; set; }

        public abstract Message Wait(int timeout);

        public abstract bool Signal(Message message);
    }

    private sealed class SyncWaiter : Waiter {
        private readonly ManualResetEvent signal;
        private Message message;
        private bool expired;

        public SyncWaiter() {
            this.signal = new ManualResetEvent(false);
        }

        public override Message Wait(int timeout) {
            this.signal.WaitOne(timeout);
            lock (this) {
                this.expired = this.message == null;
                return this.message;
            }
        }

        public override bool Signal(Message message) {
            bool signaled = false;
            lock (this) {
                if (!this.expired) {
                    this.message = message;
                    signaled = true;
                }
            }

            this.signal.Set();
            return signaled;
        }
    }

    private sealed class AsyncWaiter : Waiter {
        private static readonly TimerCallback onTimer = OnTimer;
        private readonly ReceiverLink link;
        private readonly MessageCallback callback;
        private Timer timer;
        private int state;  // 0: created, 1: waiting, 2: signaled

        public AsyncWaiter(ReceiverLink link, MessageCallback callback) {
            this.link = link;
            this.callback = callback;
        }

        public override Message Wait(int timeout) {
            this.timer = new Timer(onTimer, this, timeout, -1);
            if (Interlocked.CompareExchange(ref this.state, 1, 0) != 0) {
                // already signaled
                this.timer.Dispose();
            }

            return null;
        }

        public override bool Signal(Message message) {
            int old = Interlocked.Exchange(ref this.state, 2);
            if (old != 2) {
                Timer temp = this.timer;
                temp?.Dispose();

                this.callback(this.link, message);
                return true;
            } else {
                return false;
            }
        }

        private static void OnTimer(object state) {
            var thisPtr = (AsyncWaiter)state;
            lock (thisPtr.link.ThisLock) {
                thisPtr.link._WaiterList.Remove(thisPtr);
            }

            thisPtr.Signal(null);
        }
    }

}