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

using System;

namespace Dapr
{
#nullable disable

    internal static class DaprDefaults
    {
        public const string DaprDefaultHost = "http://localhost";
        public const string DaprDefaultHttpPort = "3500";
        public const string DaprDefaultGrpcPort = "50001";

        private static string httpEndpoint;
        private static string grpcEndpoint;
        private static string daprApiToken;
        private static string appApiToken;
        private static string host;

        /// <summary>
        /// Get the value of environment variable DAPR_API_TOKEN
        /// </summary>
        /// <returns>The value of environment variable DAPR_API_TOKEN</returns>
        public static string GetDefaultDaprApiToken()
        {
            // Lazy-init is safe because this is just populating the default
            // We don't plan to support the case where the user changes environment variables
            // for a running process.
            if (daprApiToken == null)
            {
                // Treat empty the same as null since it's an environment variable
                var value = Environment.GetEnvironmentVariable("DAPR_API_TOKEN");
                daprApiToken = (value == string.Empty) ? null : value;
            }

            return daprApiToken;
        }

        /// <summary>
        /// Get the value of environment variable APP_API_TOKEN
        /// </summary>
        /// <returns>The value of environment variable APP_API_TOKEN</returns>
        public static string GetDefaultAppApiToken()
        {
            if (appApiToken == null)
            {
                var value = Environment.GetEnvironmentVariable("APP_API_TOKEN");
                appApiToken = (value == string.Empty) ? null : value;
            }

            return appApiToken;
        }

        /// <summary>
        /// Get the default value of URI endpoint to use for HTTP calls to the Dapr runtime. 
        /// The default value will be <c>DAPR_HOST:DAPR_HTTP_PORT</c> where <c>DAPR_HOST</c> 
        /// represents the value of the <c>DAPR_HOST</c> environment variable and <c>DAPR_HTTP_PORT</c> 
        /// represents the value of the <c>DAPR_HTTP_PORT</c> environment variable. If <c>DAPR_HOST</c> environment 
        /// variable is undefined or empty <c>http://localhost</c> is used as default value. 
        /// If <c>DAPR_HTTP_PORT</c> environment variable is undefined or empty <c>3500</c> is used as default value. 
        /// </summary>
        /// <returns>The default value of URI endpoint to use for HTTP calls to the Dapr runtime.</returns>
        public static string GetDefaultHttpEndpoint()
        {
            if (httpEndpoint == null)
            {
                var port = Environment.GetEnvironmentVariable("DAPR_HTTP_PORT");
                port = string.IsNullOrEmpty(port) ? DaprDefaultHttpPort : port;
                httpEndpoint = $"{GetHost()}:{port}";
            }

            return httpEndpoint;
        }

        /// <summary>
        /// Get the default value of URI endpoint to use for gRPC calls to the Dapr runtime. 
        /// The default value will be <c>DAPR_HOST:DAPR_GRPC_PORT</c> where <c>DAPR_HOST</c> 
        /// represents the value of the <c>DAPR_HOST</c> environment variable and <c>DAPR_GRPC_PORT</c> 
        /// represents the value of the <c>DAPR_GRPC_PORT</c> environment variable. If <c>DAPR_HOST</c> environment 
        /// variable is undefined or empty <c>http://localhost</c> is used as default value. 
        /// If <c>DAPR_GRPC_PORT</c> environment variable is undefined or empty <c>50001</c> is used as default value. 
        /// </summary>
        /// <returns>The default value of URI endpoint to use for gRPC calls to the Dapr runtime.</returns>
        public static string GetDefaultGrpcEndpoint()
        {
            if (grpcEndpoint == null)
            {
                var port = Environment.GetEnvironmentVariable("DAPR_GRPC_PORT");
                port = string.IsNullOrEmpty(port) ? DaprDefaultGrpcPort : port;

                grpcEndpoint = $"{GetHost()}:{port}";
            }

            return grpcEndpoint;
        }

        /// <summary>
        /// Get the default value of DAPR host. Value is retrieved from DAPR_HOST 
        /// environment variable, http://localhost is used when the environment variable 
        /// is undefined or empty.
        /// </summary>
        /// <returns>The default value of DAPR host.</returns>
        private static string GetHost() 
        {
            if (host == null)
            {
                var envHost = Environment.GetEnvironmentVariable("DAPR_HOST");
                host = string.IsNullOrEmpty(envHost) ? DaprDefaultHost : envHost;
            }

            return host;
        }
    }

#nullable enable
}
