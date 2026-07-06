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

namespace Dapr.Actors.Builder;

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