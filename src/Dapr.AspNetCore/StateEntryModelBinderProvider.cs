// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.AspNetCore
{
    using System;
    using Microsoft.AspNetCore.Mvc.ModelBinding;

    internal class StateEntryModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!CanBind(context, out var type))
            {
                return null;
            }

            var storename = (context.BindingInfo.BindingSource as FromStateBindingSource)?.StoreName;
            var key = (context.BindingInfo.BindingSource as FromStateBindingSource)?.Key;
            return new StateEntryModelBinder(storename, key, type != context.Metadata.ModelType, type);
        }

        private static bool CanBind(ModelBinderProviderContext context, out Type type)
        {
            if (context.BindingInfo.BindingSource?.Id == "state")
            {
                // [FromState]
                type = Unwrap(context.Metadata.ModelType);
                return true;
            }

            type = null;
            return false;
        }

        private static Type Unwrap(Type type)
        {
            if (type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(StateEntry<>))
            {
                return type.GetGenericArguments()[0];
            }

            return type;
        }
    }
}
