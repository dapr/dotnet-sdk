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

using System.Collections.Generic;
using Dapr.Common.Data;
using Dapr.Common.Data.Attributes;
using Dapr.Common.Data.Extensions;
using Dapr.Common.Data.Operations.Providers.Compression;
using Dapr.Common.Data.Operations.Providers.Encoding;
using Dapr.Common.Data.Operations.Providers.Integrity;
using Dapr.Common.Data.Operations.Providers.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

 namespace Dapr.Common.Test.Data;

public class DataPipelineFactoryTests
{
    [Fact]
    public void CreatePipeline_ShouldCreateProcessingPipelineWithCorrectOperations()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDaprDataProcessingPipeline()
            .WithCompressor<GzipCompressor>()
            .WithSerializer<SystemTextJsonSerializer<SampleRecord>>()
            .WithIntegrity(_ => new Sha256Validator())
            .WithEncoder(c => new Utf8Encoder());

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<DataPipelineFactory>();

        // Act 
        var pipeline = factory.CreateEncodingPipeline<SampleRecord>();

        // Assert
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void CreatePipeline_ShouldThrowIfSerializationTypeNotRegisteredForProcessingPipeline()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDaprDataProcessingPipeline()
            .WithCompressor<GzipCompressor>()
            .WithIntegrity(_ => new Sha256Validator())
            .WithEncoder(c => new Utf8Encoder());

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<DataPipelineFactory>();
        
        // Act & Assert
        Assert.Throws<DaprException>(() => factory.CreateEncodingPipeline<SampleRecord>());
    }
    
    [Fact]
    public void CreatePipeline_ShouldThrowIEncodingTypeNotRegisteredForProcessingPipeline()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDaprDataProcessingPipeline()
            .WithCompressor<GzipCompressor>()
            .WithSerializer<SystemTextJsonSerializer<SampleRecord>>()
            .WithIntegrity(_ => new Sha256Validator());

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<DataPipelineFactory>();
        
        // Act & Assert
        Assert.Throws<DaprException>(() => factory.CreateEncodingPipeline<SampleRecord>());
    }

    [Fact]
    public void CreatePipeline_ShouldThrowIfSerializationTypeNotRegisteredForReverseProcessingPipeline()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDaprDataProcessingPipeline()
            .WithCompressor<GzipCompressor>()
            .WithIntegrity(_ => new Sha256Validator())
            .WithEncoder(c => new Utf8Encoder());

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<DataPipelineFactory>();
        var metadata = new Dictionary<string, string>
        {
            { "Dapr.Integrity.Sha256-hash", "x9yYvPm6j9Xd7X1Iwz08iQFKidQQXR9giprO3SBZg7Y=" },
            {
                "ops",
                "Dapr.Serialization.SystemTextJson,Dapr.Masking.Regexp,Dapr.Encoding.Utf8,Dapr.Compression.Gzip,Dapr.Integrity.Sha256"
            }
        };
        
        // Act & Assert
        Assert.Throws<DaprException>(() => factory.CreateDecodingPipeline<SampleRecord>(metadata));
    }
    
    [Fact]
    public void CreatePipeline_ShouldThrowIfEncodingTypeNotRegisteredForReverseProcessingPipeline()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDaprDataProcessingPipeline()
            .WithCompressor<GzipCompressor>()
            .WithIntegrity(_ => new Sha256Validator())
            .WithSerializer<SystemTextJsonSerializer<SampleRecord>>();

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<DataPipelineFactory>();
        var metadata = new Dictionary<string, string>
        {
            { "Dapr.Integrity.Sha256-hash", "x9yYvPm6j9Xd7X1Iwz08iQFKidQQXR9giprO3SBZg7Y=" },
            {
                "ops",
                "Dapr.Serialization.SystemTextJson,Dapr.Masking.Regexp,Dapr.Encoding.Utf8,Dapr.Compression.Gzip,Dapr.Integrity.Sha256"
            }
        };
        
        // Act & Assert
        Assert.Throws<DaprException>(() => factory.CreateDecodingPipeline<SampleRecord>(metadata));
    }

    [Fact]
    public void CreatePipeline_ShouldCreateReversePipelineWithCorrectOperations()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDaprDataProcessingPipeline()
            .WithCompressor<GzipCompressor>()
            .WithSerializer<SystemTextJsonSerializer<SampleRecord>>()
            .WithIntegrity(_ => new Sha256Validator())
            .WithEncoder(c => new Utf8Encoder());

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<DataPipelineFactory>();
        var metadata = new Dictionary<string, string>
        {
            { "Dapr.Integrity.Sha256-hash", "x9yYvPm6j9Xd7X1Iwz08iQFKidQQXR9giprO3SBZg7Y=" },
            {
                "ops",
                "Dapr.Serialization.SystemTextJson,Dapr.Encoding.Utf8,Dapr.Compression.Gzip,Dapr.Integrity.Sha256"
            }
        };
        
        // Act
        var pipeline = factory.CreateDecodingPipeline<SampleRecord>(metadata);

        // Assert
        Assert.NotNull(pipeline);
    }

    [DataPipeline(typeof(GzipCompressor), typeof(Utf8Encoder), typeof(SystemTextJsonSerializer<SampleRecord>))]
    private sealed record SampleRecord(string Name, int Value, bool Flag);
}
