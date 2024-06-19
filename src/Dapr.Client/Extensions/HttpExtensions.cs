// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

#nullable enable
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Dapr.Client
{
    /// <summary>
    /// Provides extensions specific to HTTP types.
    /// </summary>
    public static class HttpExtensions
    {
        /// <summary>
        /// Appends key/value pairs to the query string on an HttpRequestMessage.
        /// </summary>
        /// <param name="message">The message to append the query string parameters to.</param>
        /// <param name="queryStringParameters">The key/value pairs to populate the query string with.</param>
        public static void AddQueryParameters(this HttpRequestMessage? message,
            IReadOnlyCollection<KeyValuePair<string, string>>? queryStringParameters)
        {
            ArgumentNullException.ThrowIfNull(message, nameof(message));
            if (queryStringParameters is null || message.RequestUri is null)
                return;

            var uriBuilder = new UriBuilder(message.RequestUri);
            var qsBuilder = new StringBuilder(uriBuilder.Query);
            foreach (var kvParam in queryStringParameters)
            {
                if (qsBuilder.Length > 0)
                    qsBuilder.Append('&');
                qsBuilder.Append($"{Uri.EscapeDataString(kvParam.Key)}={Uri.EscapeDataString(kvParam.Value)}");
            }

            uriBuilder.Query = qsBuilder.ToString();
            message.RequestUri = uriBuilder.Uri;
        }
    }
}
