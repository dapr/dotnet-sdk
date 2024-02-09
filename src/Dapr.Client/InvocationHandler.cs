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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Dapr.Client
{
    /// <summary>
    /// <para>
    /// A <see cref="DelegatingHandler" /> implementation that rewrites URIs of outgoing requests
    /// to use the Dapr service invocation protocol. This handle allows code using <see cref="HttpClient" />
    /// to use the client as-if it were communciating with the destination application directly.
    /// </para>
    /// <para>
    /// The handler will read the <see cref="HttpRequestMessage.RequestUri" /> property, and 
    /// interpret the hostname as the destination <c>app-id</c>. The <see cref="HttpRequestMessage.RequestUri" /> 
    /// property will be replaced with a new URI with the authority section replaced by <see cref="DaprEndpoint" />
    /// and the path portion of the URI rewitten to follow the format of a Dapr service invocation request.
    /// </para>
    /// </summary>
    /// <remarks>
    /// This message handler does not distinguish between requests destined for Dapr service invocation and general
    /// HTTP traffic, and will attempt to forward all traffic to the Dapr endpoint. Do not attempt to set
    /// <see cref="DaprEndpoint" /> to a publicly routable URI, this will result in leaking of traffic and the Dapr
    /// security token.
    /// </remarks>
    public class InvocationHandler : DelegatingHandler
    {
        private Uri parsedEndpoint;
        private string? apiToken;

        /// <summary>
        /// Initializes a new instance of <see cref="InvocationHandler" />.
        /// </summary>
        public InvocationHandler()
        {
            this.parsedEndpoint = new Uri(DaprDefaults.GetDefaultHttpEndpoint(), UriKind.Absolute);
            this.apiToken = DaprDefaults.GetDefaultDaprApiToken();
        }

        /// <summary>
        /// Gets or the sets the URI of the Dapr HTTP endpoint used for service invocation.
        /// </summary>
        /// <returns>The URI of the Dapr HTTP endpoint used for service invocation.</returns>
        public string DaprEndpoint
        {
            get
            {
                return this.parsedEndpoint.OriginalString;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                // This will throw a reasonable exception if the URI is invalid.
                var uri = new Uri(value, UriKind.Absolute);
                if (uri.Scheme != "http" && uri.Scheme != "https")
                {
                    throw new ArgumentException("The URI scheme of the Dapr endpoint must be http or https.", "value");
                }

                this.parsedEndpoint = uri;
            }
        }

        // Internal for testing
        internal string? DaprApiToken
        {
            get => this.apiToken;
            set => this.apiToken = value;
        }

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var original = request.RequestUri;
            if (!this.TryRewriteUri(request.RequestUri, out var rewritten))
            {
                throw new ArgumentException($"The request URI '{original}' is not a valid Dapr service invocation destination.", nameof(request));
            }

            try
            {
                var apiTokenHeader = DaprClient.GetDaprApiTokenHeader(this.apiToken);
                if (apiTokenHeader is not null)
                {
                    request.Headers.Add(apiTokenHeader.Value.Key, apiTokenHeader.Value.Value);
                }
                request.RequestUri = rewritten;

                return await base.SendAsync(request, cancellationToken);
            }
            finally
            {
                request.RequestUri = original;
                request.Headers.Remove("dapr-api-token");
            }
        }

        // Internal for testing
        internal bool TryRewriteUri(Uri? uri, [NotNullWhen(true)] out Uri? rewritten)
        {
            // For now the only invalid cases are when the request URI is missing or just silly.
            // We may support additional cases for validation in the future (like an allow-list of App-Ids).
            if (uri is null || !uri.IsAbsoluteUri || (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                // do nothing
                rewritten = null;
                return false;
            }

            var builder = new UriBuilder(uri)
            {
                Scheme = this.parsedEndpoint.Scheme,
                Host = this.parsedEndpoint.Host,
                Port = this.parsedEndpoint.Port,
                Path = $"/v1.0/invoke/{this.GetOriginalHostFromUri(uri)}/method" + uri.AbsolutePath,
            };

            rewritten = builder.Uri;
            return true;
        }
        
        /// <summary>
        /// Get the original host (case sensitive) from the URI (thanks to uri.OriginalString)
        /// Mandatory to get the original host if the app id has at least one uppercase
        /// </summary>
        /// <param name="uri">The uri</param>
        /// <returns>The original hostname from the uri</returns>
        /// <exception cref="ArgumentException">The original string from the uri is invalid</exception>
        private string GetOriginalHostFromUri(Uri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);

            // If there is no upper character inside the original string, we can directly return the uri host
            if(!uri.OriginalString.Any(char.IsUpper))
            {
                return uri.Host;
            }

            Regex regex = new Regex("^.+?://(?<host>[^:/]+)", RegexOptions.Singleline | RegexOptions.Compiled);
            Match match = regex.Match(uri.OriginalString);

            if (!match.Success || !match.Groups.TryGetValue("host", out Group? host))
            {
                throw new ArgumentException("The original string for the uri is invalid.", nameof(uri));
            }

            return host.Value;
        }
    }
}
