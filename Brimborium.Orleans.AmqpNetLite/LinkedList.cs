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
#nullable enable
namespace Brimborium.OrleansAmqp;

internal interface INode {
    INode? Next { get; set; }

    INode? Previous { get; set; }
}

internal class LinkedList {
    private INode? _Head;
    private INode? _Tail;

    public INode? First { get { return this._Head; } }

    public void Add(INode node) {
        ArgumentNullException.ThrowIfNull(node, nameof(node));
        if (node.Previous != null || node.Next != null) {
            throw new ArgumentException("node is already in a list", nameof(node));
        }
        if (this._Head == null) {
            if (this._Tail != null) {
                throw new ArgumentException("tail must be null");
            }
            this._Head = this._Tail = node;
        } else {
            if (this._Tail == null) {
                throw new ArgumentException("tail must not be null");
            }
            this._Tail.Next = node;
            node.Previous = this._Tail;
            this._Tail = node;
        }
    }

    public void Remove(INode node) {
        ArgumentNullException.ThrowIfNull(node, nameof(node));
        if (node == this._Head) {
            this._Head = node.Next;
            if (this._Head == null) {
                this._Tail = null;
            } else {
                this._Head.Previous = null;
            }
        } else if (node == this._Tail) {
            this._Tail = node.Previous;
            if (this._Tail is not null) { 
                this._Tail.Next = null;
            }
        } else if (node.Previous != null && node.Next != null) {
            // remove middle
            node.Previous.Next = node.Next;
            node.Next.Previous = node.Previous;
        }

        node.Previous = node.Next = null;
    }

    public INode? Clear() {
        INode? first = this._Head;
        this._Head = this._Tail = null;
        return first;
    }
}