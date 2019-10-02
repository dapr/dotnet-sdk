// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors.Builder
{
    using System;

    /// <summary>
    ///  Provides the names for the generated types, methods, arguments, members etc.
    /// </summary>
    internal interface ICodeBuilderNames
    {
        /// <summary>
        /// Gets the name for the interface Id field.
        /// </summary>
        string InterfaceId { get; }

        /// <summary>
        /// Gets the name for the method Id field.
        /// </summary>
        string MethodId { get; }

        /// <summary>
        /// Gets the name for the retval field.
        /// </summary>
        string RetVal { get; }

        /// <summary>
        /// Gets the name for the request body field.
        /// </summary>
        string RequestBody { get; }

        /// <summary>
        /// Gets the name of the assembly in which to generate the method body types.
        /// </summary>
        /// <param name="interfaceType">The name of the remoted interface.</param>
        /// <returns>The assembly name for the method body types.</returns>
        string GetMethodBodyTypesAssemblyName(Type interfaceType);

        /// <summary>
        /// Gets the namespace of the assembly in which to generate the method body types.
        /// </summary>
        /// <param name="interfaceType">The name of the remoted interface.</param>
        /// <returns>The assembly namespace for the method body types.</returns>
        string GetMethodBodyTypesAssemblyNamespace(Type interfaceType);

        /// <summary>
        /// Gets the name of the request body type for the specified method.
        /// </summary>
        /// <param name="methodName">Name of the method whose parameters needs to be wraped in the body type.</param>
        /// <returns>The name of the request body type.</returns>
        string GetRequestBodyTypeName(string methodName);

        /// <summary>
        /// Gets the name of the response body type for the specified method.
        /// </summary>
        /// <param name="methodName">Name of the method whose return value needs to be wraped in the body type.</param>
        /// <returns>The name of the response body type.</returns>
        string GetResponseBodyTypeName(string methodName);

        /// <summary>
        /// Gets the data contract namespace for the generated types.
        /// </summary>
        /// <returns>The data contract namespace.</returns>
        string GetDataContractNamespace();

        /// <summary>
        /// Gets the name of the assembly in which to generate the method dispatcher type.
        /// </summary>
        /// <param name="interfaceType">The remoted interface type.</param>
        /// <returns>The name of the assembly for method disptacher.</returns>
        string GetMethodDispatcherAssemblyName(Type interfaceType);

        /// <summary>
        /// Gets the namespace of the assembly in which to generate the method dispatcher type.
        /// </summary>
        /// <param name="interfaceType">The remoted interface type.</param>
        /// <returns>The namespace of the assembly for method disptacher.</returns>
        string GetMethodDispatcherAssemblyNamespace(Type interfaceType);

        /// <summary>
        /// Gets the name of the method dispatcher class for dispatching methods to the implementation of the specified interface.
        /// </summary>
        /// <param name="interfaceType">The remoted interface type.</param>
        /// <returns>The name of the method dispatcher class.</returns>
        string GetMethodDispatcherClassName(Type interfaceType);

        /// <summary>
        /// Gets the name of the assembly in which to generate the proxy of the specified remoted interface.
        /// </summary>
        /// <param name="interfaceType">The remoted interface type.</param>
        /// <returns>The name of the assembly for proxy.</returns>
        string GetProxyAssemblyName(Type interfaceType);

        /// <summary>
        /// Gets the namespace of the assembly in which to generate the proxy of the specified remoted interface.
        /// </summary>
        /// <param name="interfaceType">The remoted interface type.</param>
        /// <returns>The namespace of the assembly for proxy.</returns>
        string GetProxyAssemblyNamespace(Type interfaceType);

        /// <summary>
        /// Gets the name of the proxy class for the specified interface.
        /// </summary>
        /// <param name="interfaceType">The remoted interface type.</param>
        /// <returns>The name of proxy class.</returns>
        string GetProxyClassName(Type interfaceType);

        /// <summary>
        /// Gets the name of the proxy factory (or activator) class for the specified interface.
        /// </summary>
        /// <param name="interfaceType">The remoted interface type.</param>
        /// <returns>The name of proxy activator class.</returns>
        string GetProxyActivatorClassName(Type interfaceType);
    }
}
