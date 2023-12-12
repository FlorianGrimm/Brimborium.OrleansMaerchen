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
/// Encapsulates events that represent a message sent to or from an entity.
/// </summary>
public readonly struct EntityMessageEvent {
    private readonly string _EventName;
    private readonly EntityMessage _Message;
    private readonly OrchestrationInstance _Target;

    internal EntityMessageEvent(string eventName, EntityMessage message, OrchestrationInstance target) {
        this._EventName = eventName;
        this._Message = message;
        this._Target = target;
    }

    /// <inheritdoc/>
    public override string ToString() {
        return this._Message.ToString();
    }

    /// <summary>
    /// The name of the event.
    /// </summary>
    public string EventName => this._EventName;

    /// <summary>
    /// The target instance for the event.
    /// </summary>
    public OrchestrationInstance TargetInstance => this._Target;

    /// <summary>
    /// Returns the content of this event, as a serialized string.
    /// </summary>
    /// <returns></returns>
    public string AsSerializedString() {
        return JsonConvert.SerializeObject(this._Message, Serializer.InternalSerializerSettings);
    }

    /// <summary>
    /// Returns this event in the form of a TaskMessage.
    /// </summary>
    /// <returns></returns>
    public TaskMessage AsTaskMessage() {
        return new TaskMessage {
            OrchestrationInstance = this._Target,
            Event = new History.EventRaisedEvent(-1, this.AsSerializedString()) {
                Name = this._EventName
            }
        };
    }

#pragma warning disable CS0618 // Type or member is obsolete. Intentional internal usage.
    /// <summary>
    /// Returns the content as an already-serialized string. Can be used to bypass the application-defined serializer.
    /// </summary>
    /// <returns></returns>
    public RawInput AsRawInput() {
        return new RawInput(this.AsSerializedString());
    }
#pragma warning restore CS0618 // Type or member is obsolete

    /// <summary>
    /// Utility function to compute a capped scheduled time, given a scheduled time, a timestamp representing the current time, and the maximum delay.
    /// </summary>
    /// <param name="nowUtc">a timestamp representing the current time</param>
    /// <param name="scheduledUtcTime">the scheduled time, or null if none.</param>
    /// <param name="maxDelay">The maximum delay supported by the backend.</param>
    /// <returns>the capped scheduled time, or null if none.</returns>
    public static (DateTime original, DateTime capped)? GetCappedScheduledTime(DateTime nowUtc, TimeSpan maxDelay, DateTime? scheduledUtcTime) {
        if (!scheduledUtcTime.HasValue) {
            return null;
        }

        if ((scheduledUtcTime - nowUtc) <= maxDelay) {
            return (scheduledUtcTime.Value, scheduledUtcTime.Value);
        } else {
            return (scheduledUtcTime.Value, nowUtc + maxDelay);
        }
    }
}