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
/// Exception type thrown by implementors of <see cref="TaskOrchestration"/> when exception
/// details need to flow to parent orchestrations.
/// </summary>
[Serializable]
[GenerateSerializer]
[Alias("OrchestrationFailureException")]
public class OrchestrationFailureException : Exception {
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationFailureException"/>.
    /// </summary>
    public OrchestrationFailureException() {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationFailureException"/>.
    /// </summary>
    public OrchestrationFailureException(string reason)
        : base(reason) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationFailureException"/>.
    /// </summary>
    public OrchestrationFailureException(string reason, Exception innerException)
        : base(reason, innerException) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationFailureException"/>.
    /// </summary>
    public OrchestrationFailureException(string reason, string details)
        : base(reason) {
        this.Details = details;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationFailureException"/> class.
    /// </summary>
    [Obsolete("This API supports obsolete formatter-based serialization")]
    protected OrchestrationFailureException(SerializationInfo info, StreamingContext context)
        : base(info, context) {
        this.Details = info.GetString(nameof(this.Details));
    }

    /// <summary>
    /// Details of the exception which will flow to the parent orchestration.
    /// </summary>
    [Id(0)]
    public string Details { get; set; }

    [Id(1)]
    internal FailureDetails FailureDetails { get; set; }

    /// <summary>
    /// Gets object data for use by serialization.
    /// </summary>
    [Obsolete("This API supports obsolete formatter-based serialization")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context) {
        base.GetObjectData(info, context);
        info.AddValue(nameof(this.Details), this.Details);
    }
}