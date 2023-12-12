//  ----------------------------------------------------------------------------------
//  Copyright Microsoft Corporation
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ----------------------------------------------------------------------------------

namespace Orleans.DurableTask.Core.Entities;

/// <summary>
/// The persisted state of an entity scheduler, as handed forward between ContinueAsNew instances.
/// </summary>
[DataContract]
[GenerateSerializer]
[Alias("SchedulerState")]
public class SchedulerState {
    [IgnoreDataMember]
    public bool EntityExists => this.EntityState != null;

    /// <summary>
    /// The last serialized entity state.
    /// </summary>
    [DataMember(Name = "state", EmitDefaultValue = false)]
    [Id(0)]
    public string? EntityState { get; set; }

    /// <summary>
    /// The queue of waiting operations, or null if none.
    /// </summary>
    [DataMember(Name = "queue", EmitDefaultValue = false)]
    [Id(1)]
    public Queue<RequestMessage>? Queue { get; private set; }

    /// <summary>
    /// The instance id of the orchestration that currently holds the lock of this entity.
    /// </summary>
    [DataMember(Name = "lockedBy", EmitDefaultValue = false)]
    [Id(2)]
    public string? LockedBy { get; set; }

    /// <summary>
    /// Whether processing on this entity is currently suspended.
    /// </summary>
    [DataMember(Name = "suspended", EmitDefaultValue = false)]
    [Id(3)]
    public bool Suspended { get; set; }

    /// <summary>
    /// The metadata used for reordering and deduplication of messages sent to entities.
    /// </summary>
    [DataMember(Name = "sorter", EmitDefaultValue = false)]
    [Id(4)]
    public MessageSorter MessageSorter { get; set; } = new MessageSorter();

    [IgnoreDataMember]
    public bool IsEmpty => !this.EntityExists && (this.Queue is null || this.Queue.Count == 0) && this.LockedBy is null;

    internal void Enqueue(RequestMessage operationMessage) {
        if (this.Queue is null) {
            this.Queue = new Queue<RequestMessage>();
        }

        this.Queue.Enqueue(operationMessage);
    }

    internal void PutBack(Queue<RequestMessage> messages) {
        if (this.Queue != null) {
            foreach (var message in this.Queue) {
                messages.Enqueue(message);
            }
        }

        this.Queue = messages;
    }

    internal bool MayDequeue() {
        return this.Queue != null
            && this.Queue.Count > 0
            && (this.LockedBy is null || this.LockedBy == this.Queue.Peek().ParentInstanceId);
    }

    internal RequestMessage Dequeue() {
        if (this.Queue is null) {
            throw new InvalidOperationException("Queue is empty");
        }

        var result = this.Queue.Dequeue();

        if (this.Queue.Count == 0) {
            this.Queue = null;
        }

        return result;
    }

    public override string ToString() {
        return $"exists={this.EntityExists} queue.count={(this.Queue != null ? this.Queue.Count : 0)}";
    }
}