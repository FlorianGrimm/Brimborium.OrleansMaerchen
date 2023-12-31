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
/// Delegate sending telemetry to the other side.
/// Mainly send telemetry to the Durable Functions TelemetryClient
/// </summary>
public static class CorrelationTraceClient {
    private const string DiagnosticSourceName = "DurableTask.Core";
    private const string RequestTrackEvent = "RequestEvent";
    private const string DependencyTrackEvent = "DependencyEvent";
    private const string ExceptionEvent = "ExceptionEvent";
    private static readonly DiagnosticSource _Logger = new DiagnosticListener(DiagnosticSourceName);
    private static IDisposable? _ApplicationInsightsSubscription = null;
    private static IDisposable? _ListenerSubscription = null;

    /// <summary>
    /// Setup this class uses callbacks to enable send telemetry to the Application Insights.
    /// You need to call this method if you want to use this class. 
    /// </summary>
    /// <param name="trackRequestTelemetryAction">Action to send request telemetry using <see cref="Activity"></see></param>
    /// <param name="trackDependencyTelemetryAction">Action to send telemetry for <see cref="Activity"/></param>
    /// <param name="trackExceptionAction">Action to send telemetry for exception </param>
    public static void SetUp(
        Action<TraceContextBase> trackRequestTelemetryAction,
        Action<TraceContextBase> trackDependencyTelemetryAction,
        Action<Exception> trackExceptionAction) {

        _ListenerSubscription = DiagnosticListener.AllListeners.Subscribe(

                delegate (DiagnosticListener listener) {
                    if (listener.Name == DiagnosticSourceName) {
                        _ApplicationInsightsSubscription?.Dispose();
                        
                        _ApplicationInsightsSubscription = listener.Subscribe((KeyValuePair<string, object?> evt) => {
                            if (evt.Key == RequestTrackEvent) {
                                var context = (TraceContextBase)evt.Value;
                                trackRequestTelemetryAction(context);
                            }

                            if (evt.Key == DependencyTrackEvent) {
                                // the parameter is DependencyTelemetry which is already stopped. 
                                var context = (TraceContextBase)evt.Value;
                                trackDependencyTelemetryAction(context);
                            }

                            if (evt.Key == ExceptionEvent) {
                                var e = (Exception)evt.Value;
                                trackExceptionAction(e);
                            }
                        });
                    }
                });
    }

    /// <summary>
    /// Track the RequestTelemetry
    /// </summary>
    /// <param name="context"></param>
    public static void TrackRequestTelemetry(TraceContextBase? context) {
        Tracking(() => _Logger.Write(RequestTrackEvent, context));
    }

    /// <summary>
    /// Track the DependencyTelemetry
    /// </summary>
    /// <param name="context"></param>
    public static void TrackDepencencyTelemetry(TraceContextBase context) {
        Tracking(() => _Logger.Write(DependencyTrackEvent, context));
    }

    /// <summary>
    /// Track the Exception
    /// </summary>
    /// <param name="e"></param>
    public static void TrackException(Exception e) {
        Tracking(() => _Logger.Write(ExceptionEvent, e));
    }

    /// <summary>
    /// Execute Action for Propagate correlation information.
    /// It suppresses the execution when <see cref="CorrelationSettings"/>.DisablePropagation is true.
    /// </summary>
    /// <param name="action"></param>
    public static void Propagate(Action action) {
        Execute(action);
    }

    /// <summary>
    /// Execute Aysnc Function for propagete correlation information
    /// It suppresses the execution when <see cref="CorrelationSettings"/>.DisablePropagation is true.
    /// </summary>
    /// <param name="func"></param>
    /// <returns></returns>
    public static Task PropagateAsync(Func<Task> func) {
        if (CorrelationSettings.Current.EnableDistributedTracing) {
            return func();
        } else {
            return Task.CompletedTask;
        }
    }

    private static void Tracking(Action tracking) {
        Execute(tracking);
    }

    private static void Execute(Action action) {
        if (CorrelationSettings.Current.EnableDistributedTracing) {
            action();
        }
    }
}
