// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Client
{
    using System;
    using System.Text.Json;

    /// <summary>
    /// The class containing customizable options for how the Actor Proxy is initialized.
    /// </summary>
    public class ActorProxyOptions
    {
        // TODO: Add actor retry settings

        private JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        private string daprApiToken;

        /// <summary>
        /// The <see cref="JsonSerializerOptions"/> used for actor proxy message serialization in non-remoting invocation.
        /// </summary>>
        public JsonSerializerOptions JsonSerializerOptions
        {
            get => this.jsonSerializerOptions;
            set => this.jsonSerializerOptions = value ??
                    throw new ArgumentNullException(nameof(JsonSerializerOptions), $"{nameof(ActorProxyOptions)}.{nameof(JsonSerializerOptions)} cannot be null");
        }

        /// <summary>
        /// The Dapr Api Token that is added to the header for all requests.
        /// </summary>>
        public string DaprApiToken
        {
            get => this.daprApiToken;
            set => this.daprApiToken = value ??
                    throw new ArgumentNullException(nameof(DaprApiToken), $"{nameof(ActorProxyOptions)}.{nameof(DaprApiToken)} cannot be null");
        }
    }
}
