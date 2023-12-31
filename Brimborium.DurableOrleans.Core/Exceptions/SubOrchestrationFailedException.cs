﻿//  ----------------------------------------------------------------------------------
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

namespace Orleans.DurableTask.Core.Exceptions;

/// <summary>
/// Represents errors created during sub orchestration execution
/// </summary>
[Serializable]
[GenerateSerializer]
[Alias("SubOrchestrationFailedException")]
public class SubOrchestrationFailedException : OrchestrationException {
    /// <summary>
    /// Initializes an new instance of the SubOrchestrationFailedException class
    /// </summary>
    public SubOrchestrationFailedException() {
    }

    /// <summary>
    /// Initializes an new instance of the SubOrchestrationFailedException class with a specified error message
    /// </summary>
    /// <param name="reason">The message that describes the error.</param>
    public SubOrchestrationFailedException(string reason)
        : base(reason) {
    }

    /// <summary>
    /// Initializes an new instance of the SubOrchestrationFailedException class with a specified error message
    ///    and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="reason">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public SubOrchestrationFailedException(string reason, Exception innerException)
        : base(reason, innerException) {
    }

    /// <summary>
    /// Initializes an new instance of the SubOrchestrationFailedException class with a specified event id, schedule id, name, version and error message
    ///    and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="eventId">EventId of the error.</param>
    /// <param name="scheduleId">ScheduleId of the error.</param>
    /// <param name="name">Name of the Type Instance that experienced the error.</param>
    /// <param name="version">Version of the Type Instance that experienced the error.</param>
    /// <param name="reason">The message that describes the error.</param>
    /// <param name="cause">The exception that is the cause of the current exception, or a null reference if no cause is specified.</param>
    public SubOrchestrationFailedException(
        int eventId,
        int scheduleId,
        string? name,
        string? version,
        string? reason,
        Exception? cause)
        : base(eventId, reason, cause) {
        this.ScheduleId = scheduleId;
        this.Name = name ?? string.Empty;
        this.Version = version ?? string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the SubOrchestrationFailedException class with serialized data.
    /// </summary>
    /// <param name="info">The System.Runtime.Serialization.SerializationInfo that holds the serialized object data about the exception being thrown.</param>
    /// <param name="context">The System.Runtime.Serialization.StreamingContext that contains contextual information about the source or destination.</param>
    [Obsolete("This API supports obsolete formatter-based serialization")]
    protected SubOrchestrationFailedException(SerializationInfo info, StreamingContext context)
        : base(info, context) {
        this.ScheduleId = info.GetInt32(nameof(this.ScheduleId));
        this.Name = info.GetString(nameof(this.Name));
        this.Version = info.GetString(nameof(this.Version));
    }

    /// <inheritdoc />
    [Obsolete("This API supports obsolete formatter-based serialization")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context) {
        base.GetObjectData(info, context);
        info.AddValue(nameof(this.ScheduleId), this.ScheduleId);
        info.AddValue(nameof(this.Name), this.Name);
        info.AddValue(nameof(this.Version), this.Version);
    }

    /// <summary>
    /// Schedule Id of the exception
    /// </summary>
    [Id(0)]
    public int ScheduleId { get; set; }

    /// <summary>
    /// Name of the Type Instance that experienced the error
    /// </summary>
    [Id(1)]
    public string Name { get; set; }

    /// <summary>
    /// Version of the Type Instance that experienced the error
    /// </summary>
    [Id(2)]
    public string Version { get; set; }
}