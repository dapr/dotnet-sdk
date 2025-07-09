// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dapr.Common.Extensions;

/// <summary>
/// Provides extension method implementations against an <see cref="IServiceCollection"/>.
/// </summary>
public static class DaprServiceCollectionExtensions
{
    /// <summary>
    /// The name of the Dapr HTTP client.
    /// </summary>
    public const string DaprHttpClientName = "DaprClient";
    
    /// <summary>
    /// Adds and configures a Dapr HTTP client to the service collection.
    /// </summary>
    public static IServiceCollection AddDaprHttpClient(
        this IServiceCollection services,
        Action<DaprHttpClientOptions>? configureOptions = null)
    {
        //Register the options with defaults from DaprDefaults
        services.AddOptions<DaprHttpClientOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                //Only set these values from DaprDefaults if they haven't been explicitly configured already
                options.HttpEndpoint ??= DaprDefaults.GetDefaultHttpEndpoint();
                options.GrpcEndpoint ??= DaprDefaults.GetDefaultGrpcEndpoint();
                options.DaprApiToken ??= DaprDefaults.GetDefaultDaprApiToken(configuration);
            });
        
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        // Add the HttpClient with configuration from the options and DaprDefaults
        services.AddHttpClient(name: DaprHttpClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<DaprHttpClientOptions>>().Value;
            var configuration = sp.GetRequiredService<IConfiguration>();

            //Configure the timeout
            if (options.Timeout > TimeSpan.Zero)
            {
                client.Timeout = options.Timeout;
            }

            //Add user agent
            var userAgent = DaprClientUtilities.GetUserAgent(Assembly.GetExecutingAssembly());
            client.DefaultRequestHeaders.Add("User-Agent", userAgent.ToString());

            //Add API token if needed - use options first, then fall back to DaprDefaults
            var apiToken = options.DaprApiToken ?? DaprDefaults.GetDefaultDaprApiToken(configuration);
            var apiTokenHeader = DaprClientUtilities.GetDaprApiTokenHeader(apiToken);
            if (apiTokenHeader is not null)
            {
                client.DefaultRequestHeaders.Add(apiTokenHeader.Value.Key, apiTokenHeader.Value.Value);
            }
        });

        return services;
    }

    /// <summary>
    /// Extension method to use a configured <see cref="HttpClient"/> from an <see cref="IHttpClientFactory"/>
    /// in a <see cref="DaprGenericClientBuilder{TClientBuilder}"/>.
    /// </summary>
    public static TClientBuilder UseDaprHttpClientFactory<TClientBuilder>(
        this TClientBuilder builder,
        IHttpClientFactory httpClientFactory,
        IOptions<DaprHttpClientOptions> options)
        where TClientBuilder : DaprGenericClientBuilder<IDaprClient>
    {
        builder.UseHttpClientFactory(() => httpClientFactory.CreateClient(DaprHttpClientName));
        return builder;
    }
}
