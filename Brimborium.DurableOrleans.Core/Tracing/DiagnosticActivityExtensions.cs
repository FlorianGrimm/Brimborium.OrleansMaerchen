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

namespace Orleans.DurableTask.Core.Tracing;

using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

#if WEICHEI
/// <summary>
/// Replica from System.Diagnostics.DiagnosticSource >= 6.0.0
/// </summary>
internal enum ActivityStatusCode
{
    Unset = 0,
    OK = 1,
    Error = 2,
}
#endif

/// <summary>
/// Extensions for <see cref="Activity"/>.
/// </summary>
internal static class DiagnosticActivityExtensions
{
    private static readonly Action<Activity, string> _SetSpanId;
    private static readonly Action<Activity, string> _SetId;
    //private static readonly Action<Activity, ActivityStatusCode, string> _SetStatus;

    static DiagnosticActivityExtensions()
    {
        BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        _SetSpanId = typeof(Activity).GetField("_spanId", flags).CreateSetter<Activity, string>();
        _SetId = typeof(Activity).GetField("_id", flags).CreateSetter<Activity, string>();
    }


    public static void SetId(this Activity activity, string id)
        => _SetId(activity, id);

    public static void SetSpanId(this Activity activity, string spanId)
        => _SetSpanId(activity, spanId);

#if false
    public static void SetStatus(this Activity activity, ActivityStatusCode status, string description)
        => _SetStatus(activity, status, description);
        

    private static Action<Activity, ActivityStatusCode, string> CreateSetStatus()
    {
        MethodInfo method = typeof(Activity).GetMethod("SetStatus");
        if (method is null)
        {
            return (activity, status, description) => {
                ArgumentNullException.ThrowIfNull(activity);
                string str = status switch
                {
                    ActivityStatusCode.Unset => "UNSET",
                    ActivityStatusCode.Ok => "OK",
                    ActivityStatusCode.Error => "ERROR",
                    _ => null,
                };
                _ = activity.SetTag("otel.status_code", str);
                _ = activity.SetTag("otel.status_description", description);
            };
        }

        /*
            building expression tree to effectively perform:
            (activity, status, description) => activity.SetStatus((ActivityStatusCode)(int)status, description);
        */

        ParameterExpression targetExp = Expression.Parameter(typeof(Activity), "target");
        ParameterExpression status = Expression.Parameter(typeof(ActivityStatusCode), "status");
        ParameterExpression description = Expression.Parameter(typeof(string), "description");
        UnaryExpression convert = Expression.Convert(status, typeof(int));
        convert = Expression.Convert(convert, method.GetParameters().First().ParameterType);
        MethodCallExpression callExp = Expression.Call(targetExp, method, convert, description);
        return Expression.Lambda<Action<Activity, ActivityStatusCode, string>>(callExp, targetExp, status, description)
            .Compile();
    }
#endif
}
