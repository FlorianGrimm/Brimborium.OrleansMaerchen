﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;

static class EventTraceContext {
    [ThreadStatic]
    static (long commitLogPosition, string eventId) context;

    static readonly TraceContextClear traceContextClear = new TraceContextClear();

    public static IDisposable MakeContext(long commitLogPosition, string eventId) {
        EventTraceContext.context = (commitLogPosition, eventId);
        return traceContextClear;
    }

    public static (long commitLogPosition, string eventId) Current => EventTraceContext.context;

    class TraceContextClear : IDisposable {
        public void Dispose() {
            EventTraceContext.context = (0L, null);
        }
    }

    public static void Clear() {
        EventTraceContext.context = (0L, null);
    }
}
