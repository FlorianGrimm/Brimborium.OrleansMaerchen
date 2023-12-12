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

public struct MayBeValue<T> {
    [AllowNull]
    private T _Value;
    private bool _IsNotNull;
    public MayBeValue() { }
    public MayBeValue(T value) {
        _Value = value;
        _IsNotNull = value is not null;
    }
    public MayBeValue(T value, bool isNotNull) {
        _Value = value;
        _IsNotNull = isNotNull;
    }
    public bool TryGetValue([MaybeNullWhen(false)] out T value) {
        if (_IsNotNull) {
            value = _Value!;
            return true;
        } else {
            value = default;
            return false;
        }
    }

    public T? GetValueOrNull() {
        return _Value;
    }
}