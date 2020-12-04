// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.AspNetCore
{
    using System;
    using Dapr.AspNetCore.Resources;
    using Microsoft.AspNetCore.Mvc.ApplicationModels;

    internal class StateEntryApplicationModelProvider : IApplicationModelProvider
    {
        public int Order => 0;

        public void OnProvidersExecuting(ApplicationModelProviderContext context)
        {
        }

        public void OnProvidersExecuted(ApplicationModelProviderContext context)
        {
            // Run after default providers, and customize the binding source for StateEntry<>.
            foreach (var controller in context.Result.Controllers)
            {
                foreach (var property in controller.ControllerProperties)
                {
                    if (property.BindingInfo == null)
                    {
                        // Not bindable.
                    }
                    else if (property.BindingInfo.BindingSource.Id == "state")
                    {
                        // Already configured, don't overwrite in case the user customized it.
                    }
                    else if (IsStateEntryType(property.ParameterType))
                    {
                        throw new InvalidOperationException(SR.ErrorStateStoreNameNotProvidedForStateEntry);
                    }
                }

                foreach (var action in controller.Actions)
                {
                    foreach (var parameter in action.Parameters)
                    {
                        if (parameter.BindingInfo == null)
                        {
                            // Not bindable.
                        }
                        else if (parameter.BindingInfo.BindingSource.Id == "state")
                        {
                            // Already configured, don't overwrite in case the user customized it.
                        }
                        else if (IsStateEntryType(parameter.ParameterType))
                        {
                            throw new InvalidOperationException(SR.ErrorStateStoreNameNotProvidedForStateEntry);
                        }
                    }
                }
            }
        }

        private static bool IsStateEntryType(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(StateEntry<>);
        }
    }
}
