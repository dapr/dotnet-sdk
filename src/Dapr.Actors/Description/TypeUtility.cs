// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Description
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
