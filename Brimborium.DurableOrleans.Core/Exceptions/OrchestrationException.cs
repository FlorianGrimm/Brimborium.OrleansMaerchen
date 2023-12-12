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

namespace Orleans.DurableTask.Core.Exceptions;

/// <summary>
/// Represents errors created during orchestration execution
/// </summary>
[GenerateSerializer]
[Alias("OrchestrationException")]
[Serializable]
public class OrchestrationException : Exception {
    /// <summary>
    /// Initializes an new instance of the OrchestrationException class
    /// </summary>
    public OrchestrationException() {
    }

    /// <summary>
    /// Initializes an new instance of the OrchestrationException class with a specified error message
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public OrchestrationException(string? message)
        : base(message) {
    }

    /// <summary>
    /// Initializes an new instance of the OrchestrationException class with a specified error message
    ///    and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public OrchestrationException(string? message, Exception? innerException)
        : base(message, innerException) {
    }

    /// <summary>
    /// Initializes an new instance of the OrchestrationException class with a specified event id and error message
    ///    and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="eventId">EventId of the error.</param>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public OrchestrationException(int eventId, string? message, Exception? innerException)
        : base(message, innerException) {
        this.EventId = eventId;
    }

    /// <summary>
    /// Initializes a new instance of the OrchestrationException class with serialized data.
    /// </summary>
    /// <param name="info">The System.Runtime.Serialization.SerializationInfo that holds the serialized object data about the exception being thrown.</param>
    /// <param name="context">The System.Runtime.Serialization.StreamingContext that contains contextual information about the source or destination.</param>
    [Obsolete("This API supports obsolete formatter-based serialization")]
    protected OrchestrationException(SerializationInfo info, StreamingContext context)
        : base(info, context) {
        this.EventId = info.GetInt32(nameof(this.EventId));
        this.FailureDetails = (FailureDetails)info.GetValue(nameof(this.FailureDetails), typeof(FailureDetails))!;
    }

    /// <inheritdoc />
    [Obsolete("This API supports obsolete formatter-based serialization")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context) {
        base.GetObjectData(info, context);
        info.AddValue(nameof(this.EventId), this.EventId);
        info.AddValue(nameof(this.FailureDetails), this.FailureDetails);
    }

    /// <summary>
    /// Gets or sets the EventId of the exception
    /// </summary>
    [Id(0)]
    public int EventId { get; set; }

    /// <summary>
    /// Gets additional details about the failure. May be <c>null</c> if the failure details collection is not enabled.
    /// </summary>
    [Id(1)]
    public FailureDetails? FailureDetails { get; internal set; }
}