// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Builder
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Dapr.Actors.Description;

    internal class InterfaceDetailsStore
    {
        private readonly ConcurrentDictionary<int, InterfaceDetails> knownTypesMap =
            new ConcurrentDictionary<int, InterfaceDetails>();

        private readonly ConcurrentDictionary<string, int> interfaceIdMapping =
            new ConcurrentDictionary<string, int>();

        public bool TryGetKnownTypes(int interfaceId, out InterfaceDetails interfaceDetails)
        {
            return this.knownTypesMap.TryGetValue(interfaceId, out interfaceDetails);
        }

        public bool TryGetKnownTypes(string interfaceName, out InterfaceDetails interfaceDetails)
        {
            if (!this.interfaceIdMapping.TryGetValue(interfaceName, out var interfaceId))
            {
                // TODO : Add EventSource diagnostics
                interfaceDetails = null;
                return false;
            }

            return this.knownTypesMap.TryGetValue(interfaceId, out interfaceDetails);
        }

        public void UpdateKnownTypeDetail(InterfaceDescription interfaceDescription, MethodBodyTypesBuildResult methodBodyTypesBuildResult)
        {
            var responseKnownTypes = new List<Type>();
            var requestKnownType = new List<Type>();
            foreach (var entry in interfaceDescription.Methods)
            {
                if (TypeUtility.IsTaskType(entry.ReturnType) && entry.ReturnType.GetTypeInfo().IsGenericType)
                {
                    var returnType = entry.MethodInfo.ReturnType.GetGenericArguments()[0];
                    if (!responseKnownTypes.Contains(returnType))
                    {
                        responseKnownTypes.Add(returnType);
                    }
                }

                requestKnownType.AddRange(entry.MethodInfo.GetParameters()
                    .ToList()
                    .Select(p => p.ParameterType)
                    .Except(requestKnownType));
            }

            var knownType = new InterfaceDetails
            {
                Id = interfaceDescription.Id,
                ServiceInterfaceType = interfaceDescription.InterfaceType,
                RequestKnownTypes = requestKnownType,
                ResponseKnownTypes = responseKnownTypes,
                MethodNames = interfaceDescription.Methods.ToDictionary(item => item.Name, item => item.Id),
                RequestWrappedKnownTypes = methodBodyTypesBuildResult.GetRequestBodyTypes(),
                ResponseWrappedKnownTypes = methodBodyTypesBuildResult.GetResponseBodyTypes(),
            };
            this.UpdateKnownTypes(interfaceDescription.Id, interfaceDescription.InterfaceType.FullName, knownType);
        }

        private void UpdateKnownTypes(
            int interfaceId,
            string interfaceName,
            InterfaceDetails knownTypes)
        {
            if (this.knownTypesMap.ContainsKey(interfaceId))
            {
                // TODO : Add EventSource diagnostics
                return;
            }

            if (this.knownTypesMap.TryAdd(interfaceId, knownTypes))
            {
                this.interfaceIdMapping.TryAdd(interfaceName, interfaceId);
            }
        }
    }
}
