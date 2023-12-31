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
/// Object instance creator for a type using default name and version based on the type
/// </summary>
/// <typeparam name="T">Type of Object</typeparam>
public class DefaultObjectCreator<T> : ObjectCreator<T> {
    private readonly T instance;
    private readonly Type prototype;

    /// <summary>
    /// Creates a new DefaultObjectCreator of supplied type
    /// </summary>
    /// <param name="type">Type to use for the creator</param>
    public DefaultObjectCreator(Type type) {
        this.prototype = type;
        this.Initialize(type);
    }

    /// <summary>
    /// Creates a new DefaultObjectCreator using type of supplied object instance
    /// </summary>
    /// <param name="instance">Object instances to infer the type from</param>
    public DefaultObjectCreator(T instance) {
        this.instance = instance;
        this.Initialize(instance);
    }

    /// <summary>
    /// Creates a new instance of the object creator's type
    /// </summary>
    /// <returns>An instance of the type T</returns>
    public override T Create() {
        if (this.prototype != null) {
            return (T)Activator.CreateInstance(this.prototype);
        }

        return this.instance;
    }

    private void Initialize(object obj) {
        this.Name = NameVersionHelper.GetDefaultName(obj);
        this.Version = NameVersionHelper.GetDefaultVersion(obj);
    }
}