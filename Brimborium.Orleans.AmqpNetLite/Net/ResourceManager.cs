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

namespace Brimborium.OrleansAmqp.Transactions;

internal sealed class ResourceManager {
    private static readonly ResourceManager _Instance = new ResourceManager();
    private readonly Dictionary<Connection, Controller> _Controllers;
    private readonly Dictionary<string, Enlistment> _Enlistments;

    private ResourceManager() {
        this._Controllers = new Dictionary<Connection, Controller>();
        this._Enlistments = new Dictionary<string, Enlistment>(StringComparer.OrdinalIgnoreCase);
    }

    private object SyncRoot { get { return this._Enlistments; } }

    public static async Task<TransactionalState> GetTransactionalStateAsync(Link link) {
        Transaction txn = Transaction.Current;
        if (txn != null) {
            byte[] txnId = await _Instance.EnlistAsync(link, txn).ConfigureAwait(false);
            return new TransactionalState() { TxnId = txnId };
        }

        return null;
    }

    private async Task<byte[]> EnlistAsync(Link link, Transaction txn) {
        string id = txn.TransactionInformation.LocalIdentifier;
        Enlistment enlistment;
        lock (this.SyncRoot) {
            if (!this._Enlistments.TryGetValue(id, out enlistment)) {
                enlistment = new Enlistment(this, txn);
                this._Enlistments.Add(id, enlistment);
                txn.TransactionCompleted += this.OnTransactionCompleted;

                if (!txn.EnlistPromotableSinglePhase(enlistment)) {
                    this._Enlistments.Remove(id);
                    txn.TransactionCompleted -= this.OnTransactionCompleted;
                    throw new InvalidOperationException("DTC not supported");
                }
            }
        }

        return await enlistment.EnlistAsync(link).ConfigureAwait(false);
    }

    private void OnTransactionCompleted(object sender, TransactionEventArgs e) {
        lock (this.SyncRoot) {
            var localIdentifier = e.Transaction?.TransactionInformation.LocalIdentifier;
            if (localIdentifier is not null) { 
                this._Enlistments.Remove(localIdentifier);
            }
        }
    }

    private Controller GetOrCreateController(Link link) {
        Controller controller;
        lock (this.SyncRoot) {
            if (!this._Controllers.TryGetValue(link.Session.Connection, out controller)) {
                Session session = new Session(link.Session.Connection);
                controller = new Controller(session);
                controller.Closed += this.OnControllerClosed;
                this._Controllers.Add(link.Session.Connection, controller);
            }
        }

        return controller;
    }

    private void OnControllerClosed(IAmqpObject obj, Error error) {
        var controller = (Controller)obj;
        bool removed;
        lock (this.SyncRoot) {
            removed = this._Controllers.Remove(controller.Session.Connection);
        }

        if (removed) {
            controller.Session.CloseInternal(0);
        }
    }

    private class Enlistment : IPromotableSinglePhaseNotification {
        //private static readonly TimeSpan _RollbackTimeout = TimeSpan.FromMinutes(1);
        private readonly ResourceManager _Owner;
        private readonly Transaction _Transaction;
        private readonly string _TransactionId;
        private readonly object _SyncRoot;
        private Controller _Controller;
        private Task<byte[]> _DeclareTask;
        private byte[] _Txnid;

        public Enlistment(ResourceManager owner, Transaction transaction) {
            this._Owner = owner;
            this._Transaction = transaction;
            this._TransactionId = this._Transaction.TransactionInformation.LocalIdentifier;
            this._SyncRoot = new object();
        }

        public async Task<byte[]> EnlistAsync(Link link) {
            if (this._Txnid != null) {
                return this._Txnid;
            }

            lock (this._SyncRoot) {
                if (this._DeclareTask == null) {
                    this._Controller = this._Owner.GetOrCreateController(link);
                    this._DeclareTask = this._Controller.DeclareAsync();
                }
            }

            return this._Txnid = await this._DeclareTask.ConfigureAwait(false);
        }

        void IPromotableSinglePhaseNotification.Initialize() {
        }

        void IPromotableSinglePhaseNotification.Rollback(SinglePhaseEnlistment singlePhaseEnlistment) {
            lock (this._SyncRoot) {
                if (this._Txnid != null) {
                    this._Controller.DischargeAsync(this._Txnid, true).ContinueWith(
                        (t, o) => {
                            var spe = (SinglePhaseEnlistment)o;
                            if (t.IsFaulted) {
                                spe?.Aborted(t.Exception.InnerException);
                            } else {
                                spe?.Done();
                            }
                        },
                        singlePhaseEnlistment);
                }
            }
        }

        void IPromotableSinglePhaseNotification.SinglePhaseCommit(SinglePhaseEnlistment singlePhaseEnlistment) {
            lock (this._SyncRoot) {
                if (this._Txnid != null) {
                    this._Controller.DischargeAsync(this._Txnid, false).ContinueWith(
                        (t, o) => {
                            var spe = (SinglePhaseEnlistment)o;
                            if (t.IsFaulted) {
                                spe?.Aborted(t.Exception.InnerException);
                            } else {
                                spe?.Done();
                            }
                        },
                        singlePhaseEnlistment);
                }
            }
        }

        byte[] ITransactionPromoter.Promote() {
            throw new TransactionPromotionException("DTC not supported");
        }
    }
}
