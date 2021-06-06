// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ---

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr.AppCallback.Autogen.Grpc.v1;
using Dapr.Client;
using Dapr.Client.Autogen.Grpc.v1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Dapr.AspNetCore
{
    /// <summary>
    /// Base dapr class for gRPC service in ASP.NET Core
    /// </summary>
    public abstract class GrpcBaseService
    {
        /// <summary>
        /// call context for the current invocation which be setted by <see cref="AppCallbackImplementation"/>
        /// </summary>
        public ServerCallContext Context { get; internal set; }

        /// <summary>
        /// <see cref="DaprClient"/> instance which be setted by <see cref="AppCallbackImplementation"/>
        /// </summary>
        public DaprClient DaprClient { get; internal set; }
    }
}
