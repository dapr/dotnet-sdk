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

using Dapr.Common.Data.Operations;
using Dapr.Common.Data.Operations.Providers.Compression;
using Dapr.Common.Data.Operations.Providers.Encoding;
using Dapr.Common.Data.Operations.Providers.Integrity;
using Dapr.Common.Data.Operations.Providers.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dapr.Common.Data.Extensions;

/// <summary>
/// Contains the dependency injection registration extension for the Dapr data pipeline operations.
/// </summary>
public static class DaprDataPipelineRegistrationBuilderExtensions
{
    /// <summary>
    /// Registers a Dapr data processing pipeline.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IDaprDataPipelineBuilder AddDaprDataProcessingPipeline(this IServiceCollection services)
    {
        services.AddSingleton<DataPipelineFactory>();
        return new DaprDataPipelineBuilder(services);
    }

    /// <summary>
    /// Adds a serializer data operation.
    /// </summary>
    public static IDaprDataPipelineBuilder WithSerializer<TService>(this IDaprDataPipelineBuilder builder,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TService : class, IDaprDataOperation =>
        builder.WithDaprOperation<TService>(lifetime);

    /// <summary>
    /// Adds a serializer data operation.
    /// </summary>
    public static IDaprDataPipelineBuilder WithSerializer<TInput>(this IDaprDataPipelineBuilder builder,
        Func<IServiceProvider, IDaprDataSerializer<TInput>> serializerFactory,
        ServiceLifetime lifetime = ServiceLifetime.Singleton) =>
        builder.WithDaprOperation<IDaprDataSerializer<TInput>, TInput, string>(serializerFactory, lifetime);

    /// <summary>
    /// Adds a compression data operation.
    /// </summary>
    public static IDaprDataPipelineBuilder WithCompressor<TService>(this IDaprDataPipelineBuilder builder,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TService : class, IDaprDataCompressor =>
        builder.WithDaprOperation<TService>(lifetime);

    /// <summary>
    /// Adds a compressor data operation.
    /// </summary>
    public static IDaprDataPipelineBuilder WithCompressor(this IDaprDataPipelineBuilder builder,
        Func<IServiceProvider, IDaprDataCompressor> compressorFactory,
        ServiceLifetime lifetime = ServiceLifetime.Singleton) =>
        builder.WithDaprOperation<IDaprDataCompressor, ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(compressorFactory,
            lifetime);

    /// <summary>
    /// Adds a data integrity operation.
    /// </summary>
    public static IDaprDataPipelineBuilder WithIntegrity<TService>(this IDaprDataPipelineBuilder builder,
        ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        where TService : class, IDaprDataValidator
        => builder.WithDaprOperation<TService>(serviceLifetime);

    /// <summary>
    /// Adds a data integrity operation using a factory that provides an <see cref="IServiceProvider"/>.
    /// </summary>
    public static IDaprDataPipelineBuilder WithIntegrity(this IDaprDataPipelineBuilder builder,
        Func<IServiceProvider, IDaprDataValidator> validatorFactory,
        ServiceLifetime serviceLifetime = ServiceLifetime.Singleton) =>
        builder.WithDaprOperation<IDaprDataValidator, ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(validatorFactory,
            serviceLifetime);
    
    /// <summary>
    /// Adds an encoder operation.
    /// </summary>
    public static IDaprDataPipelineBuilder WithEncoder<TService>(this IDaprDataPipelineBuilder builder,
        ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        where TService : class, IDaprDataEncoder
        => builder.WithDaprOperation<TService>(serviceLifetime);

    /// <summary>
    /// Adds an encoder operation using a factory that provides an <see cref="IServiceProvider"/>.
    /// </summary>
    public static IDaprDataPipelineBuilder WithEncoder(this IDaprDataPipelineBuilder builder,
        Func<IServiceProvider, IDaprDataEncoder> encoderFactory,
        ServiceLifetime serviceLifetime = ServiceLifetime.Singleton) =>
        builder.WithDaprOperation<IDaprDataEncoder, string, ReadOnlyMemory<byte>>(encoderFactory,
            serviceLifetime);
    
    /// <summary>
    /// Registers the specified Dapr operation services.
    /// </summary>
    /// <param name="builder">The builder to register the type on.</param>
    /// <param name="lifetime">The lifetime the service should be registered for.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an invalid service lifetime is provided.</exception>
    private static IDaprDataPipelineBuilder WithDaprOperation<TService>(this IDaprDataPipelineBuilder builder,
        ServiceLifetime lifetime)
        where TService : class, IDaprDataOperation
    {
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDaprDataOperation, TService>());
                break;
            case ServiceLifetime.Scoped:
                builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IDaprDataOperation, TService>());
                break;
            case ServiceLifetime.Transient:
                builder.Services.TryAddEnumerable(ServiceDescriptor.Transient<IDaprDataOperation, TService>());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null);
        }
    
        return builder;
    }

    /// <summary>
    /// Registers the specified <see cref="IDaprDataOperation{TInput,TOutput}"/> 
    /// </summary>
    /// <param name="builder">The builder to register the type on.</param>
    /// <param name="operationFactory">The data operation factory used to register the data operation service.</param>
    /// <param name="lifetime">The lifetime the service should be registered for.</param>
    /// <typeparam name="TService">The type of service being registered.</typeparam>
    /// <typeparam name="TInput">The input type provided to the operation.</typeparam>
    /// <typeparam name="TOutput">The output type provided by the operation.</typeparam>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an invalid service lifetime is provided.</exception>
    private static IDaprDataPipelineBuilder WithDaprOperation<TService, TInput, TOutput>(this IDaprDataPipelineBuilder builder,
        Func<IServiceProvider, TService> operationFactory, ServiceLifetime lifetime)
        where TService : class, IDaprDataOperation<TInput, TOutput>
    {
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDaprDataOperation>(operationFactory));
                break;
            case ServiceLifetime.Scoped:
                builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IDaprDataOperation>(operationFactory));
                break;
            case ServiceLifetime.Transient:
                builder.Services.TryAddEnumerable(ServiceDescriptor.Transient<IDaprDataOperation>(operationFactory));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null);
        }

        return builder;
    }
}
