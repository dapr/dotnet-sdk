// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Builder
{
    using System;

    /// <summary>
    /// Represents an interface for generating the code to support communication.
    /// </summary>
    internal interface ICodeBuilder
    {
        /// <summary>
        /// Gets the interface for getting the names of the generated code (types, interfaces, methods etc.)
        /// </summary>
        ICodeBuilderNames Names { get; }

        /// <summary>
        /// Gets or builds a type that can send the communication messages to the object implementing the specified interface.
        /// </summary>
        /// <param name="interfaceType">Interface for which to generate the method dispatcher.</param>
        /// <returns>A <see cref="MethodDispatcherBuildResult"/> containing the dispatcher to dispatch the messages destined the specified interfaces.</returns>
        MethodDispatcherBuildResult GetOrBuilderMethodDispatcher(Type interfaceType);

        /// <summary>
        /// Gets or builds a communication message body types that can store the method arguments of the specified interface.
        /// </summary>
        /// <param name="interfaceType">Interface for which to generate the method body types.</param>
        /// <returns>A <see cref="MethodBodyTypesBuildResult"/> containing the method body types for each of the methods of the specified interface.</returns>
        MethodBodyTypesBuildResult GetOrBuildMethodBodyTypes(Type interfaceType);

        /// <summary>
        /// Gets or builds a factory object that can generate communication proxy for the specified interface.
        /// </summary>
        /// <param name="interfaceType">Interface for which to generate the proxy factory object.</param>
        /// <returns>A <see cref="ActorProxyGeneratorBuildResult"/> containing the generator for communication proxy for the speficifed interface.</returns>
        ActorProxyGeneratorBuildResult GetOrBuildProxyGenerator(Type interfaceType);
    }
}
