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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Dapr.Client;

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
    private Uri parsedEndpoint = new(DaprDefaults.GetDefaultHttpEndpoint(), UriKind.Absolute);
    private string? apiToken = DaprDefaults.GetDefaultDaprApiToken(null);

    /// <summary>
    /// Gets or the sets the URI of the Dapr HTTP endpoint used for service invocation.
    /// </summary>
    /// <returns>The URI of the Dapr HTTP endpoint used for service invocation.</returns>
    public string DaprEndpoint
    {
        get => this.parsedEndpoint.OriginalString;
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
        var original = request.RequestUri;
        if (!this.TryRewriteUri(request.RequestUri, out var rewritten))
        {
            throw new ArgumentException($"The request URI '{original}' is not a valid Dapr service invocation destination.", nameof(request));
        }

        try
        {
            var token = this.apiToken;
            if (token != null)
            {
                var apiTokenHeader = DaprClient.GetDaprApiTokenHeader(token);
                if (apiTokenHeader is not null)
                {
                    request.Headers.Add(apiTokenHeader.Value.Key, apiTokenHeader.Value.Value);
                }
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

        string host;

        if (this.DefaultAppId is not null && uri.Host.Equals(this.DefaultAppId, StringComparison.InvariantCultureIgnoreCase))
        {
            host = this.DefaultAppId;
        }
        else
        {
            host = uri.Host;
        }

        var builder = new UriBuilder(uri)
        {
            Scheme = this.parsedEndpoint.Scheme,
            Host = this.parsedEndpoint.Host,
            Port = this.parsedEndpoint.Port,
            Path = $"/v1.0/invoke/{host}/method" + uri.AbsolutePath,
        };

        rewritten = builder.Uri;
        return true;
    }
}
