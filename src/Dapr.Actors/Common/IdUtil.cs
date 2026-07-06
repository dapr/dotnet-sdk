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

namespace Dapr.Actors.Common;

using System;
using System.Reflection;
using System.Text;

internal static class IdUtil
{
    internal static int ComputeId(MethodInfo methodInfo)
    {
        var hash = methodInfo.Name.GetHashCode();

        if (methodInfo.DeclaringType != null)
        {
            if (methodInfo.DeclaringType.Namespace != null)
            {
                hash = HashCombine(methodInfo.DeclaringType.Namespace.GetHashCode(), hash);
            }

            hash = HashCombine(methodInfo.DeclaringType.Name.GetHashCode(), hash);
        }

        return hash;
    }

    internal static int ComputeId(Type type)
    {
        var hash = type.Name.GetHashCode();
        if (type.Namespace != null)
        {
            hash = HashCombine(type.Namespace.GetHashCode(), hash);
        }

        return hash;
    }

    internal static int ComputeIdWithCRC(Type type)
    {
        var name = type.Name;

        if (type.Namespace != null)
        {
            name = string.Concat(type.Namespace, name);
        }

        return ComputeIdWithCRC(name);
    }

    internal static int ComputeIdWithCRC(MethodInfo methodInfo)
    {
        var name = methodInfo.Name;

        if (methodInfo.DeclaringType != null)
        {
            if (methodInfo.DeclaringType.Namespace != null)
            {
                name = string.Concat(methodInfo.DeclaringType.Namespace, name);
            }

            name = string.Concat(methodInfo.DeclaringType.Name, name);
        }

        return ComputeIdWithCRC(name);
    }

    internal static int ComputeIdWithCRC(string typeName)
    {
        return (int)CRC64.ToCRC64(Encoding.UTF8.GetBytes(typeName));
    }

    internal static int ComputeId(string typeName, string typeNamespace)
    {
        var hash = typeName.GetHashCode();
        if (typeNamespace != null)
        {
            hash = HashCombine(typeNamespace.GetHashCode(), hash);
        }

        return hash;
    }

    /// <summary>
    /// This is how VB Anonymous Types combine hash values for fields.
    /// </summary>
    internal static int HashCombine(int newKey, int currentKey)
    {
#pragma warning disable SA1139 // Use literal suffix notation instead of casting
        return unchecked((currentKey * (int)0xA5555529) + newKey);
#pragma warning restore SA1139 // Use literal suffix notation instead of casting
    }
}