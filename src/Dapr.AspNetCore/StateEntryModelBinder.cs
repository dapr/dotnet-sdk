// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
    using Microsoft.Extensions.DependencyInjection;

    internal class StateEntryModelBinder : IModelBinder
    {
        private readonly Func<StateClient, string, string, Task<object>> thunk;
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
                this.thunk = (Func<StateClient, string, string, Task<object>>)Delegate.CreateDelegate(typeof(Func<StateClient, string, string, Task<object>>), null, method);
            }
            else
            {
                var method = this.GetType().GetMethod(nameof(GetStateAsync), BindingFlags.Static | BindingFlags.NonPublic);
                method = method.MakeGenericMethod(type);
                this.thunk = (Func<StateClient, string, string, Task<object>>)Delegate.CreateDelegate(typeof(Func<StateClient, string, string, Task<object>>), null, method);
            }
        }

        public async Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if (bindingContext is null)
            {
                throw new ArgumentNullException(nameof(bindingContext));
            }

            var stateClient = bindingContext.HttpContext.RequestServices.GetRequiredService<StateClient>();

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
                var message = $"Required value {key} not present.";
                bindingContext.Result = ModelBindingResult.Failed();
                bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, message);
                return;
            }

            var obj = await this.thunk(stateClient, this.storeName, key);
            bindingContext.Result = ModelBindingResult.Success(obj);

            bindingContext.ValidationState.Add(bindingContext.Result.Model, new ValidationStateEntry()
            {
                // Don't do validation since the data came from a trusted source.
                SuppressValidation = true,
            });
        }

        private static async Task<object> GetStateEntryAsync<T>(StateClient stateClient, string storeName, string key)
        {
            return await stateClient.GetStateEntryAsync<T>(storeName, key);
        }

        private static async Task<object> GetStateAsync<T>(StateClient stateClient, string storeName, string key)
        {
            return await stateClient.GetStateAsync<T>(storeName, key);
        }
    }
}