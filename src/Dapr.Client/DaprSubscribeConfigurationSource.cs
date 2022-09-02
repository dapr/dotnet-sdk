﻿// ------------------------------------------------------------------------
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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Autogenerated = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Client
{
    internal class DaprSubscribeConfigurationSource : ConfigurationSource
    {
        private AsyncServerStreamingCall<Autogenerated.SubscribeConfigurationResponse> call;
        private string id = string.Empty;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="call">The streaming call from the Dapr Subscribe Configuration API.</param>
        internal DaprSubscribeConfigurationSource(AsyncServerStreamingCall<Autogenerated.SubscribeConfigurationResponse> call) : base()
        {
            this.call = call;
        }

        /// <inheritdoc/>
        public override string Id => id;

        public override IAsyncEnumerator<Dictionary<string, ConfigurationItem>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new DaprSubscribeConfigurationEnumerator(call, streamId => setIdIfNullOrEmpty(streamId), cancellationToken);
        }

        private void setIdIfNullOrEmpty(string id)
        {
            if (string.IsNullOrEmpty(this.id))
            {
                this.id = id;
            }
        }
    }

    internal class DaprSubscribeConfigurationEnumerator : IAsyncEnumerator<Dictionary<string, ConfigurationItem>>
    {
        private AsyncServerStreamingCall<Autogenerated.SubscribeConfigurationResponse> call;
        private Action<string> idCallback;
        private CancellationToken cancellationToken;

        internal DaprSubscribeConfigurationEnumerator(
            AsyncServerStreamingCall<Autogenerated.SubscribeConfigurationResponse> call,
            Action<string> idCallback,
            CancellationToken cancellationToken = default)
        {
            this.call = call;
            this.idCallback = idCallback;
            this.cancellationToken = cancellationToken;
        }

        /// <inheritdoc/>
        public Dictionary<string, ConfigurationItem> Current
        {
            get
            {
                var current = call.ResponseStream.Current;
                if (current != null)
                {
                    idCallback(current.Id);
                    return current.Items.ToDictionary(item => item.Key, item => new ConfigurationItem(item.Value.Value, item.Value.Version, item.Value.Metadata));
                }
                return null;
            }
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            call.Dispose();
            return new ValueTask();
        }

        /// <inheritdoc/>
        public async ValueTask<bool> MoveNextAsync()
        {
            return await call.ResponseStream.MoveNext(cancellationToken);
        }
    }
}
