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

internal class NameVersionObjectManager<T> : INameVersionObjectManager<T> {
    private readonly Dictionary<string, ObjectCreator<T>> _Creators;
    private readonly object _ThisLock = new object();

    public NameVersionObjectManager() {
        this._Creators = new Dictionary<string, ObjectCreator<T>>(StringComparer.Ordinal);
    }

    public void Add(ObjectCreator<T> creator) {
        string key = this.GetKey(creator.Name, creator.Version);
        lock (this._ThisLock) {

            if (this._Creators.ContainsKey(key)) {
                throw new InvalidOperationException(
                    $"Duplicate entry detected: {creator.Name} {creator.Version}");
            }

            this._Creators.Add(key, creator);
        }
    }

    [return: MaybeNull]
    public T GetObject(string name, string version) {
        string key = this.GetKey(name, version);

        lock (this._ThisLock) {
            if (this._Creators.TryGetValue(key, out ObjectCreator<T> creator)) {
                return creator.Create();
            }

            return default(T);
        }
    }

    private string GetKey(string name, string version) {
        return $"{name}_{version}";
    }
}