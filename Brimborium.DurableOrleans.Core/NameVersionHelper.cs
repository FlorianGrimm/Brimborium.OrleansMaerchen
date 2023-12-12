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
/// Helper class for getting name information from types and instances
/// </summary>
public static class NameVersionHelper {
    internal static string GetDefaultMethodName(
        MethodInfo methodInfo,
        bool useFullyQualifiedMethodNames) {
        if (useFullyQualifiedMethodNames
            && methodInfo.DeclaringType != null) {
            return GetFullyQualifiedMethodName(methodInfo.DeclaringType.Name, methodInfo.Name);
        } else {
            return methodInfo.Name;
        }
    }

    /// <summary>
    /// Gets the default name of an Object instance without using fully qualified names
    /// </summary>
    /// <param name="obj">Object to get the name for</param>
    /// <returns>Name of the object instance's type</returns>
    public static string GetDefaultName(object obj) {
        return GetDefaultName(obj, false);
    }

    /// <summary>
    /// Gets the default name of an Object instance using reflection
    /// </summary>
    /// <param name="obj">Object to get the name for</param>
    /// <param name="useFullyQualifiedMethodNames">Boolean indicating whether to use fully qualified names or not</param>
    /// <returns>Name of the object instance's type</returns>
    public static string GetDefaultName(object obj, bool useFullyQualifiedMethodNames) {
        ArgumentNullException.ThrowIfNull(obj, nameof(obj));

        if (obj is Type type) {
            return type.ToString();
        } else if (obj is MethodInfo methodInfo) {
            return GetDefaultMethodName(methodInfo, useFullyQualifiedMethodNames);
        } else if (obj is InvokeMemberBinder binder) {
            return binder.Name;
        } else {
            return obj.GetType().ToString();
        }
    }

    /// <summary>
    /// Gets the default version for an object instance's type
    /// </summary>
    /// <param name="obj">Object to get the version for</param>
    /// <returns>The version as string</returns>
    public static string GetDefaultVersion(object obj) {
        return string.Empty;
    }

    internal static string GetFullyQualifiedMethodName(string declaringType, string methodName) {
        if (string.IsNullOrWhiteSpace(declaringType)) {
            return methodName;
        } else {
            return $"{declaringType}.{methodName}";
        }
    }
}