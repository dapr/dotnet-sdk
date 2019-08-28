// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Description
{
    using System;
    using System.Reflection;
    using System.Threading.Tasks;

    internal static class TypeUtility
    {
        public static bool IsTaskType(Type type)
        {
            return ((type == typeof(Task)) ||
                    (type.GetTypeInfo().IsGenericType && (type.GetGenericTypeDefinition() == typeof(Task<>))));
        }

        public static bool IsVoidType(Type type)
        {
            return ((type == typeof(void)) || (type == null));
        }
    }
}
