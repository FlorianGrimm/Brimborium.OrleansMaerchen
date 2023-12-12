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

namespace Orleans.DurableTask.Core.Middleware;

internal class DispatchMiddlewarePipeline {
    private readonly List<Func<DispatchMiddlewareDelegate, DispatchMiddlewareDelegate>> _Components = [];

    public Task RunAsync(DispatchMiddlewareContext context, DispatchMiddlewareDelegate handler) {
        // Build the delegate chain
        //foreach (var component in this._Components.Reverse()) {
        //    handler = component(handler);
        //}

        for(int index = this._Components.Count - 1; index >= 0; index--) {
            handler = this._Components[index](handler);
        }

        return handler(context);
    }

    public void Add(Func<DispatchMiddlewareContext, Func<Task>, Task> middleware) {
        this._Components.Add(next => {
            return context => {
                Task SimpleNext() => next(context);
                return middleware(context, SimpleNext);
            };
        });
    }
}
