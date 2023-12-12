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
/// W3CTraceContext keep the correlation value with W3C TraceContext protocol
/// </summary>
public class W3CTraceContext : TraceContextBase {
    /// <summary>
    /// Default constructor
    /// </summary>
    public W3CTraceContext() : base() { }

    /// <summary>
    /// W3C TraceContext: Traceparent
    /// </summary>
    public string TraceParent { get; set; }

    /// <summary>
    /// W3C TraceContext: Tracestate
    /// </summary>
    public string TraceState { get; set; }

    /// <summary>
    /// W3C TraceContext: ParentSpanId
    /// </summary>
    public string ParentSpanId { get; set; }

    /// <inheritdoc />
    public override TimeSpan Duration => this.CurrentActivity?.Duration ?? DateTimeOffset.UtcNow - this.StartTime;

    /// <inheritdoc />
    public override string TelemetryId {
        get {
            if (this.CurrentActivity == null) {
                var traceParent = TraceParentObject.Create(this.TraceParent);
                return traceParent.SpanId;
            } else {
                return this.CurrentActivity.SpanId.ToHexString();
            }
        }
    }

    /// <inheritdoc />
    public override string TelemetryContextOperationId => this.CurrentActivity?.RootId ??
                TraceParentObject.Create(this.TraceParent).TraceId;

    /// <inheritdoc />
    public override string TelemetryContextOperationParentId {
        get {
            if (this.CurrentActivity == null) {
                return this.ParentSpanId;
            } else {
                return this.CurrentActivity.ParentSpanId.ToHexString();
            }
        }
    }

    /// <inheritdoc />
    public override void SetParentAndStart(TraceContextBase parentTraceContext) {
        if (this.CurrentActivity == null) {
            this.CurrentActivity = new Activity(this.OperationName);
            _ = this.CurrentActivity.SetIdFormat(ActivityIdFormat.W3C);
        }

        if (parentTraceContext is W3CTraceContext) {
            var context = (W3CTraceContext)parentTraceContext;
            _ = this.CurrentActivity.SetParentId(context.TraceParent);
            this.CurrentActivity.TraceStateString = context.TraceState;
            this.OrchestrationTraceContexts = context.OrchestrationTraceContexts.Clone();
        }

        _ = this.CurrentActivity.Start();

        this.StartTime = this.CurrentActivity.StartTimeUtc;
        this.TraceParent = this.CurrentActivity.Id;
        this.TraceState = this.CurrentActivity.TraceStateString;
        this.ParentSpanId = this.CurrentActivity.ParentSpanId.ToHexString();

        CorrelationTraceContext.Current = this;
    }

    /// <inheritdoc />
    public override void StartAsNew() {
        this.CurrentActivity = new Activity(this.OperationName);
        _ = this.CurrentActivity.SetIdFormat(ActivityIdFormat.W3C);
        _ = this.CurrentActivity.Start();

        this.StartTime = this.CurrentActivity.StartTimeUtc;

        this.TraceParent = this.CurrentActivity.Id;

        this.CurrentActivity.TraceStateString = this.TraceState;
        this.TraceState = this.CurrentActivity.TraceStateString;
        this.ParentSpanId = this.CurrentActivity.ParentSpanId.ToHexString();

        CorrelationTraceContext.Current = this;
    }
}

internal class TraceParentObject {
    public string Version { get; set; }

    public string TraceId { get; set; }

    public string SpanId { get; set; }

    public string TraceFlags { get; set; }

    public static TraceParentObject Create(string traceParent) {
        if (!string.IsNullOrEmpty(traceParent)) {
            var substrings = traceParent.Split('-');
            if (substrings.Length != 4) {
                throw new ArgumentException($"Traceparent doesn't respect the spec. {traceParent}");
            }

            return new TraceParentObject {
                Version = substrings[0],
                TraceId = substrings[1],
                SpanId = substrings[2],
                TraceFlags = substrings[3]
            };
        }

        return new TraceParentObject();
    }
}
