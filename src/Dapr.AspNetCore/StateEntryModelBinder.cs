// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.AspNetCore;

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.DependencyInjection;
using Dapr.Client;

internal class StateEntryModelBinder : IModelBinder
{
    private readonly Func<DaprClient, string, string, Task<object>> thunk;
    private readonly string key;
    private readonly string storeName;
    private readonly bool isStateEntry;
    private readonly Type type;

    public StateEntryModelBinder(string storeName, string key, bool isStateEntry, Type type)
    {
        this.storeName = storeName;
        this.key = key;
        this.isStateEntry = isStateEntry;
        this.type = type;

        if (isStateEntry)
        {
            var method = this.GetType().GetMethod(nameof(GetStateEntryAsync), BindingFlags.Static | BindingFlags.NonPublic);
            method = method.MakeGenericMethod(type);
            this.thunk = (Func<DaprClient, string, string, Task<object>>)Delegate.CreateDelegate(typeof(Func<DaprClient, string, string, Task<object>>), null, method);
        }
        else
        {
            var method = this.GetType().GetMethod(nameof(GetStateAsync), BindingFlags.Static | BindingFlags.NonPublic);
            method = method.MakeGenericMethod(type);
            this.thunk = (Func<DaprClient, string, string, Task<object>>)Delegate.CreateDelegate(typeof(Func<DaprClient, string, string, Task<object>>), null, method);
        }
    }

    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        if (bindingContext is null)
        {
            throw new ArgumentNullException(nameof(bindingContext));
        }

        var daprClient = bindingContext.HttpContext.RequestServices.GetRequiredService<DaprClient>();

        // Look up route values to use for keys into state.
        bool missingKey = false;
        var keyName = this.key ?? bindingContext.FieldName;
        var key = (string)bindingContext.HttpContext.Request.RouteValues[keyName];
        if (string.IsNullOrEmpty(key))
        {
            missingKey = true;
        }

        if (missingKey)
        {
            // If we get here this is a configuration error. The error is somewhat opaque on
            // purpose to avoid leaking too much information about the app.
            var message = $"Required value {keyName} not present.";
            bindingContext.Result = ModelBindingResult.Failed();
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, message);
            return;
        }
 
        var obj = await this.thunk(daprClient, this.storeName, key);

        // When the state isn't found in the state store:
        // - If the StateEntryModelBinder is associated with a value of type StateEntry<T>, then the above call returns an object of type
        //   StateEntry<T> which is non-null, but StateEntry<T>.Value is null
        // - If the StateEntryModelBinder is associated with a value of type T, then the above call returns a null value.
        if (obj == null)
        {
            bindingContext.Result = ModelBindingResult.Failed();
        }
        else
        {
            bindingContext.Result = ModelBindingResult.Success(obj);
            bindingContext.ValidationState.Add(bindingContext.Result.Model, new ValidationStateEntry()
            {
                // Don't do validation since the data came from a trusted source.
                SuppressValidation = true,
            });
        }
    }

    private static async Task<object> GetStateEntryAsync<T>(DaprClient daprClient, string storeName, string key)
    {
        return await daprClient.GetStateEntryAsync<T>(storeName, key);
    }

    private static async Task<object> GetStateAsync<T>(DaprClient daprClient, string storeName, string key)
    {
        return await daprClient.GetStateAsync<T>(storeName, key);
    }
}