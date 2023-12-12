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
/// Exception type thrown by implementors of <see cref="TaskActivity"/> when exception
/// details need to flow to parent orchestrations.
/// </summary>
[Serializable]
[GenerateSerializer]
[Alias("TaskFailureException")]
public class TaskFailureException : Exception {
    /// <summary>
    /// Initializes a new instance of the <see cref="TaskFailureException"/> class.
    /// </summary>
    public TaskFailureException() {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskFailureException"/> class.
    /// </summary>
    public TaskFailureException(string reason)
        : base(reason) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskFailureException"/> class.
    /// </summary>
    public TaskFailureException(string reason, Exception innerException)
        : base(reason, innerException) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskFailureException"/> class.
    /// </summary>
    public TaskFailureException(string reason, Exception innerException, string details)
        : base(reason, innerException) {
        this.Details = details;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskFailureException"/> class.
    /// </summary>
    public TaskFailureException(string reason, string details)
        : base(reason) {
        this.Details = details;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskFailureException"/> class.
    /// </summary>
    [Obsolete("This API supports obsolete formatter-based serialization")]
    protected TaskFailureException(SerializationInfo info, StreamingContext context)
        : base(info, context) {
        this.Details = info.GetString(nameof(this.Details));

        if (this.ExistPropertyInfo(info, nameof(this.FailureSource))) {
            // FailureSource is an internal property, it may not be populated by the serialization engine
            this.FailureSource = info.GetString(nameof(this.FailureSource));
        }
    }

    /// <summary>
    /// Gets object data for use by serialization.
    /// </summary>
    [Obsolete("This API supports obsolete formatter-based serialization")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context) {
        base.GetObjectData(info, context);
        info.AddValue(nameof(this.Details), this.Details);
        info.AddValue(nameof(this.FailureSource), this.FailureSource);
    }

    /// <summary>
    /// Returns a debug string representing the current exception object.
    /// </summary>
    public override string ToString() {
        return string.Format("FailureSource: {1}{0}Details: {2}{0}Message: {3}{0}Exception: {4}",
            Environment.NewLine,
            this.FailureSource,
            this.Details,
            this.Message,
            base.ToString());
    }

    /// <summary>
    /// Details of the exception which will flow to the parent orchestration.
    /// </summary>
    [Id(0)]
    public string Details { get; set; }

    [Id(1)]
    internal string FailureSource { get; set; }

    [Id(2)]
    internal FailureDetails FailureDetails { get; set; }

    internal TaskFailureException WithFailureSource(string failureSource) {
        this.FailureSource = failureSource;
        return this;
    }

    internal TaskFailureException WithFailureDetails(FailureDetails failureDetails) {
        this.FailureDetails = failureDetails;
        return this;
    }

    private bool ExistPropertyInfo(SerializationInfo info, string propertyName) {
        SerializationInfoEnumerator enumerator = info.GetEnumerator();
        while (enumerator.MoveNext()) {
            if (enumerator.Current.Name == propertyName) {
                return true;
            }
        }

        return false;
    }
}