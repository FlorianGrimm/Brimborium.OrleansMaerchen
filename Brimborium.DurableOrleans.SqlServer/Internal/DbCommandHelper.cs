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

namespace Orleans.DurableTask.SqlServer.Internal;

/// <summary>
/// Intended for internal use only; not all edge cases are handled, but these extension methods will work correctly for the queries defined in this assembly and results in more readable code.
/// </summary>
internal static class DbCommandHelper {
    internal readonly static Dictionary<string, object?> EmptyParameters = new();

    
    internal static void AddStatement(this DbCommand source, 
        string sql, 
        IDictionary<string, object?>? parameters = null) {
        ArgumentNullException.ThrowIfNull(source);

        //replace each parameter in the sql statement with auto-generated names
        //add parameters using new auto-generated names
        foreach (var parameter in parameters ?? EmptyParameters) {
            var newName = Guid.NewGuid().ToString("N");
            sql = sql.Replace("@" + parameter.Key, "@" + newName);
            source.AddParameter(newName, parameter.Value ?? DBNull.Value);
        }

        //add newline to ensure commands have some white-space between them; added two new lines for readability
        if (!string.IsNullOrWhiteSpace(source.CommandText)) {
            source.CommandText += Environment.NewLine + Environment.NewLine;
        }

        source.CommandText += sql;
        return;
    }

#warning don't like the reflection here
    internal static void AddStatement(this DbCommand source, string sql, object parameters) {
        if (source == null) {
            throw new ArgumentNullException(nameof(source));
        }

        var dictionary = new Dictionary<string, object?>();

        //convert object to dictionary
        foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(parameters)) {
            var parameterValue = descriptor.GetValue(parameters);
            dictionary.Add(descriptor.Name, parameterValue);
        }

        source.AddStatement(sql, dictionary);
    }

    internal static DbCommand AddParameter(this DbCommand source, string name, object value) {
        if (source == null) {
            throw new ArgumentNullException(nameof(source));
        }

        var parameter = source.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;

        if (value is DateTime) {
            parameter.DbType = DbType.DateTime2;
        }

        source.Parameters.Add(parameter);

        //allow method-chaining
        return source;
    }
}
