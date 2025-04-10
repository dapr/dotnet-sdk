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

using System.Reflection;
using System.Text.Json;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;

namespace Dapr.Common;

/// <summary>
/// Builder for building a generic Dapr client.
/// </summary>
public abstract class DaprGenericClientBuilder<TClientBuilder> where TClientBuilder : class, IDaprClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DaprGenericClientBuilder{TClientBuilder}"/> class.
    /// </summary>
    protected DaprGenericClientBuilder(IConfiguration? configuration = null)
    {
        this.GrpcEndpoint = DaprDefaults.GetDefaultGrpcEndpoint();
        this.HttpEndpoint = DaprDefaults.GetDefaultHttpEndpoint();

        this.GrpcChannelOptions = new GrpcChannelOptions()
        {
            // The gRPC client doesn't throw the right exception for cancellation
            // by default, this switches that behavior on.
            ThrowOperationCanceledOnCancellation = true,
        };

        this.JsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        this.DaprApiToken = DaprDefaults.GetDefaultDaprApiToken(configuration);
    }

    /// <summary>
    /// Property exposed for testing purposes.
    /// </summary>
    internal string GrpcEndpoint { get; private set; }

    /// <summary>
    /// Property exposed for testing purposes.
    /// </summary>
    internal string HttpEndpoint { get; private set; }

    /// <summary>
    /// Property exposed for testing purposes.
    /// </summary>
    internal Func<HttpClient>? HttpClientFactory { get; set; }

    /// <summary>
    /// Property exposed for testing purposes.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; private set; }

    /// <summary>
    /// Property exposed for testing purposes.
    /// </summary>
    internal GrpcChannelOptions GrpcChannelOptions { get; private set; }

    /// <summary>
    /// Property exposed for testing purposes.
    /// </summary>
    public string DaprApiToken { get; private set; }
    
    /// <summary>
    /// Property exposed for testing purposes.
    /// </summary>
    internal TimeSpan Timeout { get; private set; }

    /// <summary>
    /// Overrides the HTTP endpoint used by the Dapr client for communicating with the Dapr runtime.
    /// </summary>
    /// <param name="httpEndpoint">
    /// The URI endpoint to use for HTTP calls to the Dapr runtime. The default value will be 
    /// <c>DAPR_HTTP_ENDPOINT</c> first, or <c>http://127.0.0.1:DAPR_HTTP_PORT</c> as fallback
    /// where <c>DAPR_HTTP_ENDPOINT</c> and <c>DAPR_HTTP_PORT</c> represents the value of the
    /// corresponding environment variables. 
    /// </param>
    /// <returns>The <see cref="DaprGenericClientBuilder{TClientBuilder}" /> instance.</returns>
    public DaprGenericClientBuilder<TClientBuilder> UseHttpEndpoint(string httpEndpoint)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(httpEndpoint, nameof(httpEndpoint));
        this.HttpEndpoint = httpEndpoint;
        return this;
    }

    /// <summary>
    /// Exposed internally for testing purposes.
    /// </summary>
    internal DaprGenericClientBuilder<TClientBuilder> UseHttpClientFactory(Func<HttpClient> factory)
    {
        this.HttpClientFactory = factory;
        return this;
    }

    /// <summary>
    /// Overrides the legacy mechanism for building an HttpClient and uses the new <see cref="IHttpClientFactory"/>
    /// introduced in .NET Core 2.1.
    /// </summary>
    /// <param name="httpClientFactory">The factory used to create <see cref="HttpClient"/> instances.</param>
    /// <returns></returns>
    public DaprGenericClientBuilder<TClientBuilder> UseHttpClientFactory(IHttpClientFactory httpClientFactory)
    {
        this.HttpClientFactory = httpClientFactory.CreateClient;
        return this;
    }

    /// <summary>
    /// Overrides the gRPC endpoint used by the Dapr client for communicating with the Dapr runtime.
    /// </summary>
    /// <param name="grpcEndpoint">
    /// The URI endpoint to use for gRPC calls to the Dapr runtime. The default value will be 
    /// <c>http://127.0.0.1:DAPR_GRPC_PORT</c> where <c>DAPR_GRPC_PORT</c> represents the value of the 
    /// <c>DAPR_GRPC_PORT</c> environment variable.
    /// </param>
    /// <returns>The <see cref="DaprGenericClientBuilder{TClientBuilder}" /> instance.</returns>
    public DaprGenericClientBuilder<TClientBuilder> UseGrpcEndpoint(string grpcEndpoint)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(grpcEndpoint, nameof(grpcEndpoint));
        this.GrpcEndpoint = grpcEndpoint;
        return this;
    }

    /// <summary>
    /// <para>
    /// Uses the specified <see cref="JsonSerializerOptions"/> when serializing or deserializing using <see cref="System.Text.Json"/>.
    /// </para>
    /// <para>
    /// The default value is created using <see cref="JsonSerializerDefaults.Web" />.
    /// </para>
    /// </summary>
    /// <param name="options">Json serialization options.</param>
    /// <returns>The <see cref="DaprGenericClientBuilder{TClientBuilder}" /> instance.</returns>
    public DaprGenericClientBuilder<TClientBuilder> UseJsonSerializationOptions(JsonSerializerOptions options)
    {
        this.JsonSerializerOptions = options;
        return this;
    }

    /// <summary>
    /// Uses the provided <paramref name="grpcChannelOptions" /> for creating the <see cref="GrpcChannel" />.
    /// </summary>
    /// <param name="grpcChannelOptions">The <see cref="GrpcChannelOptions" /> to use for creating the <see cref="GrpcChannel" />.</param>
    /// <returns>The <see cref="DaprGenericClientBuilder{TClientBuilder}" /> instance.</returns>
    public DaprGenericClientBuilder<TClientBuilder> UseGrpcChannelOptions(GrpcChannelOptions grpcChannelOptions)
    {
        this.GrpcChannelOptions = grpcChannelOptions;
        return this;
    }

    /// <summary>
    /// Adds the provided <paramref name="apiToken" /> on every request to the Dapr runtime.
    /// </summary>
    /// <param name="apiToken">The token to be added to the request headers/>.</param>
    /// <returns>The <see cref="DaprGenericClientBuilder{TClientBuilder}" /> instance.</returns>
    public DaprGenericClientBuilder<TClientBuilder> UseDaprApiToken(string apiToken)
    {
        this.DaprApiToken = apiToken;
        return this;
    }

    /// <summary>
    ///  Sets the timeout for the HTTP client used by the Dapr client.
    /// </summary>
    /// <param name="timeout"></param>
    /// <returns></returns>
    public DaprGenericClientBuilder<TClientBuilder> UseTimeout(TimeSpan timeout)
    {
        this.Timeout = timeout;
        return this;
    }

    /// <summary>
    /// Builds out the inner DaprClient that provides the core shape of the
    /// runtime gRPC client used by the consuming package.
    /// </summary>
    /// <param name="assembly">The assembly the dependencies are being built for.</param>
    /// <exception cref="InvalidOperationException"></exception>
    protected internal (GrpcChannel channel, HttpClient httpClient, Uri httpEndpoint, string daprApiToken) BuildDaprClientDependencies(Assembly assembly)
    {
        var grpcEndpoint = new Uri(this.GrpcEndpoint);
        if (grpcEndpoint.Scheme != "http" && grpcEndpoint.Scheme != "https")
        {
            throw new InvalidOperationException("The gRPC endpoint must use http or https.");
        }

        if (grpcEndpoint.Scheme.Equals(Uri.UriSchemeHttp))
        {
            // Set correct switch to make secure gRPC service calls. This switch must be set before creating the GrpcChannel.
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        }

      var httpEndpoint = new Uri(this.HttpEndpoint);
        if (httpEndpoint.Scheme != "http" && httpEndpoint.Scheme != "https")
        {
            throw new InvalidOperationException("The HTTP endpoint must use http or https.");
        }

        //Configure the HTTP client
        var httpClient = ConfigureHttpClient(assembly);
        this.GrpcChannelOptions.HttpClient = httpClient;
        
        var channel = GrpcChannel.ForAddress(this.GrpcEndpoint, this.GrpcChannelOptions);        
        return (channel, httpClient, httpEndpoint, this.DaprApiToken);
    }

    /// <summary>
    /// Configures the HTTP client.
    /// </summary>
    /// <param name="assembly">The assembly the user agent is built from.</param>
    /// <returns>The HTTP client to interact with the Dapr runtime with.</returns>
    private HttpClient ConfigureHttpClient(Assembly assembly)
    {
        var httpClient = HttpClientFactory is not null ? HttpClientFactory() : new HttpClient();
        
        //Set the timeout as necessary
        if (this.Timeout > TimeSpan.Zero)
        {
            httpClient.Timeout = this.Timeout;
        }
        
        //Set the user agent
        var userAgent = DaprClientUtilities.GetUserAgent(assembly);
        httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent.ToString());
        
        //Set the API token
        var apiTokenHeader = DaprClientUtilities.GetDaprApiTokenHeader(this.DaprApiToken);
        if (apiTokenHeader is not null)
        {
            httpClient.DefaultRequestHeaders.Add(apiTokenHeader.Value.Key, apiTokenHeader.Value.Value);
        }

        return httpClient;
    }

    /// <summary>
    /// Builds the client instance from the properties of the builder.
    /// </summary>
    /// <returns>The Dapr client instance.</returns>
    /// <summary>
    /// Builds the client instance from the properties of the builder.
    /// </summary>
    public abstract TClientBuilder Build();
}
