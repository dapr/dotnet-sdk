// using Microsoft.Extensions.DependencyInjection;
//
// namespace System.Runtime.CompilerServices.PublishSubscribe.Extensions;
//
// /// <summary>
// /// Contains extension methods for using Dapr Publish/Subscribe with dependency injection.
// /// </summary>
// public static class PublishSubscribeServiceCollectionExtensions
// {
//     /// <summary>
//     /// Adds Dapr Publish/Subscribe support to the service collection.
//     /// </summary>
//     /// <param name="services">The <see cref="IServiceCollection"/>.</param>
//     /// <returns></returns>
//     public static IServiceCollection AddDaprPubSubClient(this IServiceCollection services) =>
//         AddDaprPubSubClient(services, (_, _) => { });
//
//     /// <summary>
//     /// Adds Dapr Publish/Subscribe support to the service collection.
//     /// </summary>
//     /// <param name="services">The <see cref="IServiceCollection"/>.</param>
//     /// <param name="configure">Optionally allows greater configuration of the <see cref="DaprPublishSubscribeClientBuilder"/>.</param>
//     /// <returns></returns>
//     public static IServiceCollection AddDaprPubSubClient(this IServiceCollection services,
//         Action<DaprPublishSubscribeClientBuilder>? configure) =>
//         services.AddDaprPubSubClient((_, builder) => configure?.Invoke(builder));
//
//     /// <summary>
//     /// Adds Dapr Publish/Subscribe support to the service collection.
//     /// </summary>
//     /// <param name="services">The <see cref="IServiceCollection"/>.</param>
//     /// <param name="configure">Optionally allows greater configuration of the <see cref="DaprPublishSubscribeClient"/> using injected services.</param>
//     /// <returns></returns>
//     public static IServiceCollection AddDaprPubSubClient(this IServiceCollection services, Action<IServiceProvider, DaprPublishSubscribeClientBuilder>? configure)
//     {
//         ArgumentNullException.ThrowIfNull(services, nameof(services));
//
//         //Register the IHttpClientFactory implementation
//         services.AddHttpClient();
//
//         services.TryAddSingleton(serviceProvider =>
//         {
//             var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
//
//             var builder = new DaprPublishSubscribeClientBuilder();
//             builder.UseHttpClientFactory(httpClientFactory);
//
//             configure?.Invoke(serviceProvider, builder);
//
//             return builder.Build();
//         });
//
//         return services;
//     }
// }
