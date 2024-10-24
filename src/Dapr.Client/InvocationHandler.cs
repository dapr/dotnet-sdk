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
        private readonly string[] supportedUriSchemes = { "http", "https" };

        /// <summary>
        /// Initializes a new instance of <see cref="InvocationHandler" />.
        /// </summary>
        public InvocationHandler()
        {
            this.parsedEndpoint = new Uri(DaprDefaults.GetDefaultHttpEndpoint(), UriKind.Absolute);
            this.apiToken = DaprDefaults.GetDefaultDaprApiToken(null);
        }

        /// <summary>
        /// Gets or the sets the URI of the Dapr HTTP endpoint used for service invocation.
        /// </summary>
        /// <returns>The URI of the Dapr HTTP endpoint used for service invocation.</returns>
        public string DaprEndpoint
        {
            get => this.parsedEndpoint.OriginalString;
            
            set
            {
                ArgumentNullException.ThrowIfNull(value);

                // This will throw a reasonable exception if the URI is invalid.
                var uri = new Uri(value, UriKind.Absolute);
                
                if (!IsUriSchemeSupported(uri.Scheme))
                {
                    throw new ArgumentException("The URI scheme of the Dapr endpoint must be http or https.", nameof(value));
                }

                this.parsedEndpoint = uri;
            }
        }

        /// <summary>
        /// Gets or sets the default AppId used for service invocation
        /// </summary>
        /// <returns>The AppId used for service invocation</returns>
        public string? DefaultAppId { get; set; }

        // Internal for testing
        internal string? DaprApiToken
        {
            get => this.apiToken;
            set => this.apiToken = value;
        }

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var originalUri = request.RequestUri;
            
            if (!this.TryRewriteUri(request.RequestUri, out var rewrittenUri))
            {
                throw new ArgumentException($"The request URI '{originalUri}' is not a valid Dapr service invocation destination.", nameof(request));
            }

            try
            {
                var apiTokenHeader = DaprClient.GetDaprApiTokenHeader(this.apiToken);
                
                if (apiTokenHeader is not null)
                {
                    request.Headers.Add(apiTokenHeader.Value.Key, apiTokenHeader.Value.Value);
                }
                
                request.RequestUri = rewrittenUri;

                return await base.SendAsync(request, cancellationToken);
            }
            finally
            {
                request.RequestUri = originalUri;
                request.Headers.Remove("dapr-api-token");
            }
        }

        // Internal for testing
        internal bool TryRewriteUri(Uri? uri, [NotNullWhen(true)] out Uri? rewrittenUri)
        {
            if (!IsUriValid(uri))
            {
                // do nothing
                rewrittenUri = null;
                return false;
            }

            string host = UriHostContainsValidDefaultAppId(uri!.Host) ? this.DefaultAppId! : uri!.Host;

            var builder = new UriBuilder(uri)
            {
                Scheme = this.parsedEndpoint.Scheme,
                Host = this.parsedEndpoint.Host,
                Port = this.parsedEndpoint.Port,
                Path = $"/v1.0/invoke/{host}/method" + uri.AbsolutePath
            };

            rewrittenUri = builder.Uri;
            return true;
        }
        
        private bool IsUriSchemeSupported(string uriScheme)
            => supportedUriSchemes.Contains(uriScheme, StringComparer.InvariantCultureIgnoreCase);

        // For now the only invalid cases are when the request URI is missing or just silly.
        // We may support additional cases for validation in the future (like an allow-list of App-Ids).
        private bool IsUriValid(Uri? uri)
            => uri is not null && uri.IsAbsoluteUri && IsUriSchemeSupported(uri.Scheme);

        private bool UriHostContainsValidDefaultAppId(string uriHost)
            => this.DefaultAppId is not null &&
               uriHost.Equals(this.DefaultAppId, StringComparison.InvariantCultureIgnoreCase);

    }
}
