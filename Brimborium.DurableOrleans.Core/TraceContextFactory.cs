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

namespace Orleans.DurableTask.Core;

/// <summary>
/// Factory of TraceContext
/// </summary>
public class TraceContextFactory {
    /// <summary>
    /// Create an instance of TraceContext
    /// </summary>
    /// <param name="operationName">Operation name for the TraceContext</param>
    /// <returns></returns>
    public static TraceContextBase Create(string operationName) {
        return CreateFactory().Create(operationName);
    }

    /// <summary>
    /// Create an instance of TraceContext
    /// </summary>
    /// <param name="activity">Activity already started</param>
    /// <returns></returns>
    public static TraceContextBase Create(Activity activity) {
        return CreateFactory().Create(activity);
    }

    /// <summary>
    /// Create a default context of TraceContext
    /// returns NullObjectTraceContext object
    /// </summary>
    public static TraceContextBase Empty { get; } = new NullObjectTraceContext();

    private static ITraceContextFactory CreateFactory() {
        switch (CorrelationSettings.Current.Protocol) {
            case Protocol.W3CTraceContext:
                return new W3CTraceContextFactory();
            case Protocol.HttpCorrelationProtocol:
                return new HttpCorrelationProtocolTraceContextFactory();
            default:
                throw new NotSupportedException($"{CorrelationSettings.Current.Protocol} is not supported. Check the CorrelationSettings.Current.Protocol");
        }
    }

    private interface ITraceContextFactory {
        TraceContextBase Create(Activity activity);

        TraceContextBase Create(string operationName);
    }

    private class W3CTraceContextFactory : ITraceContextFactory {
        public TraceContextBase Create(Activity activity) {
            return new W3CTraceContext() {
                OperationName = activity.OperationName,
                StartTime = activity.StartTimeUtc,
#warning TODO: !
                TraceParent = activity.Id!,
#warning TODO: !
                TraceState = activity.TraceStateString!,
                ParentSpanId = activity.ParentSpanId.ToHexString(),
                // ParentId = activity.Id // TODO check if it necessary
                CurrentActivity = activity
            };
        }

        public TraceContextBase Create(string operationName) {
            return new W3CTraceContext() {
                OperationName = operationName
            };
        }
    }

    private class HttpCorrelationProtocolTraceContextFactory : ITraceContextFactory {
        public TraceContextBase Create(Activity activity) {
            return new HttpCorrelationProtocolTraceContext() {
                OperationName = activity.OperationName,
                StartTime = activity.StartTimeUtc,
#warning TODO: !
                ParentId = activity.Id!,
                CurrentActivity = activity
            };
        }

        public TraceContextBase Create(string operationName) {
            return new HttpCorrelationProtocolTraceContext() {
                OperationName = operationName
            };
        }
    }
}
