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

namespace Orleans.DurableTask.Core;

/// <summary>
/// HttpCorrelationProtocolTraceContext keep the correlation value with HTTP Correlation Protocol
/// </summary>
public class HttpCorrelationProtocolTraceContext : TraceContextBase {
    /// <summary>
    /// Default Constructor
    /// </summary>
    public HttpCorrelationProtocolTraceContext() : base() { }

    /// <summary>
    /// ParentId for backward compatibility
    /// </summary>
    public string ParentId { get; set; }

    /// <summary>
    /// ParentId for parent
    /// </summary>
    public string ParentParentId { get; set; }

    /// <inheritdoc />
    public override void SetParentAndStart(TraceContextBase parentTraceContext) {
        this.CurrentActivity = new Activity(this.OperationName);
        _ = this.CurrentActivity.SetIdFormat(ActivityIdFormat.Hierarchical);

        if (parentTraceContext is HttpCorrelationProtocolTraceContext) {
            var context = (HttpCorrelationProtocolTraceContext)parentTraceContext;
            _ = this.CurrentActivity.SetParentId(context.ParentId); // TODO check if it is context.ParentId or context.CurrentActivity.Id 
            this.OrchestrationTraceContexts = context.OrchestrationTraceContexts.Clone();
        }

        _ = this.CurrentActivity.Start();

        this.ParentId = this.CurrentActivity.Id;
        this.StartTime = this.CurrentActivity.StartTimeUtc;
        this.ParentParentId = this.CurrentActivity.ParentId;

        CorrelationTraceContext.Current = this;
    }

    /// <inheritdoc />
    public override void StartAsNew() {
        this.CurrentActivity = new Activity(this.OperationName);
        _ = this.CurrentActivity.SetIdFormat(ActivityIdFormat.Hierarchical);
        _ = this.CurrentActivity.Start();

        this.ParentId = this.CurrentActivity.Id;
        this.StartTime = this.CurrentActivity.StartTimeUtc;
        this.ParentParentId = this.CurrentActivity.ParentId;

        CorrelationTraceContext.Current = this;
    }

    /// <inheritdoc />
    public override TimeSpan Duration => this.CurrentActivity?.Duration ?? DateTimeOffset.UtcNow - this.StartTime;

    /// <inheritdoc />
    public override string TelemetryId => this.CurrentActivity?.Id ?? this.ParentId;

    /// <inheritdoc />
    public override string TelemetryContextOperationId => this.CurrentActivity?.RootId ?? this.GetRootId(this.ParentId);

    /// <inheritdoc />
    public override string TelemetryContextOperationParentId => this.CurrentActivity?.ParentId ?? this.ParentParentId;

    // internal use. Make it internal for testability.
    public string? GetRootId(string id) => id?.Split('.').FirstOrDefault()?.Replace("|", "");
}
