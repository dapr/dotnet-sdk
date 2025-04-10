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