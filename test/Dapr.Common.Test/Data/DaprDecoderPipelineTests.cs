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

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Common.Data;
using Dapr.Common.Data.Attributes;
using Dapr.Common.Data.Extensions;
using Dapr.Common.Data.Operations;
using Dapr.Common.Data.Operations.Providers.Compression;
using Dapr.Common.Data.Operations.Providers.Encoding;
using Dapr.Common.Data.Operations.Providers.Integrity;
using Dapr.Common.Data.Operations.Providers.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dapr.Common.Test.Data;

public class DaprDecoderPipelineTests
{
    [Fact]
    public async Task ReverseAsync_ShouldReverseOperationsInMetadataOrder()
    {
        // Arrange
        var operations = new List<IDaprDataOperation>
        {
            new GzipCompressor(),
            new SystemTextJsonSerializer<SampleRecord>(),
            new Utf8Encoder(),
            new Sha256Validator()
        };
        var opNames = new Stack<string>();
        opNames.Push("Dapr.Serialization.SystemTextJson[0]");
        opNames.Push("Dapr.Encoding.Utf8[0]");
        opNames.Push("Dapr.Compression.Gzip[0]");
        opNames.Push("Dapr.Integrity.Sha256[0]");
        
        var pipeline = new DaprDecoderPipeline<SampleRecord>(operations, opNames);
        
        // Act
        var payload = Convert.FromBase64String("H4sIAAAAAAAACqtWykvMTVWyUgpOzC3ISVXSUSpLzCkFChia1gIAotvhPBwAAAA=");
        var metadata = new Dictionary<string, string>
        {
            { "Dapr.Integrity.Sha256-hash", "x9yYvPm6j9Xd7X1Iwz08iQFKidQQXR9giprO3SBZg7Y=" },
            {
                "ops",
                "Dapr.Serialization.SystemTextJson[0],Dapr.Encoding.Utf8[0],Dapr.Compression.Gzip[0],Dapr.Integrity.Sha256[0]"
            }
        };
        var result = await pipeline.ReverseProcessAsync(payload, metadata);
        
        Assert.Equal("Sample", result.Payload.Name);
        Assert.Equal(15, result.Payload.Value);
    }

    [Fact]
    public async Task EndToEndTest()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDaprDataProcessingPipeline()
            .WithCompressor<GzipCompressor>()
            .WithSerializer<SystemTextJsonSerializer<SimpleRegistration>>()
            .WithIntegrity(_ => new Sha256Validator())
            .WithEncoder(c => new Utf8Encoder());

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<DataPipelineFactory>();

        var encoderPipeline = factory.CreateEncodingPipeline<SimpleRegistration>();
        var record = new SimpleRegistration("This is merely a test!");
        
        // Act
        var encodedPayload = await encoderPipeline.ProcessAsync(record, CancellationToken.None);

        // Assert
        Assert.NotNull(encodedPayload);
        Assert.True(encodedPayload.Payload.Length > 0);
        Assert.Equal("H4sIAAAAAAAACqtWykvMTVWyUgrJyCxWAKLc1KLUnEqFRIWS1OISRaVaAF3KYX0hAAAA",
            Convert.ToBase64String(encodedPayload.Payload.Span));
        Assert.NotNull(encodedPayload.Metadata);
        Assert.True(encodedPayload.Metadata.ContainsKey("ops"));
        Assert.Equal("Dapr.Serialization.SystemTextJson[0],Dapr.Encoding.Utf8[0],Dapr.Compression.Gzip[0],Dapr.Integrity.Sha256[0]", encodedPayload.Metadata["ops"]);
        Assert.True(encodedPayload.Metadata.ContainsKey("Dapr.Integrity.Sha256[0]hash"));
        Assert.Equal("Ehr18bGgwtfe/uq8MbfnIQkbsUYOAHt7xWNAecRo2DI=", encodedPayload.Metadata["Dapr.Integrity.Sha256[0]hash"]);

        // Act #2
        var decoderPipeline = factory.CreateDecodingPipeline<SimpleRegistration>(encodedPayload.Metadata);
        var decodedPayload = await decoderPipeline.ReverseProcessAsync(encodedPayload.Payload, encodedPayload.Metadata);
        
        // Assert #2
        Assert.Equal(record, decodedPayload.Payload);
    }
    
    [Fact]
    public async Task EndToEndTest_ShouldFailValidationWithBadHashValue()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDaprDataProcessingPipeline()
            .WithCompressor<GzipCompressor>()
            .WithSerializer<SystemTextJsonSerializer<SimpleRegistration>>()
            .WithIntegrity(_ => new Sha256Validator())
            .WithEncoder(c => new Utf8Encoder());

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<DataPipelineFactory>();

        var encoderPipeline = factory.CreateEncodingPipeline<SimpleRegistration>();
        var record = new SimpleRegistration("This is merely a test!");
        
        // Act
        var encodedPayload = await encoderPipeline.ProcessAsync(record, CancellationToken.None);

        // Assert
        Assert.NotNull(encodedPayload);
        Assert.True(encodedPayload.Payload.Length > 0);
        Assert.Equal("H4sIAAAAAAAACqtWykvMTVWyUgrJyCxWAKLc1KLUnEqFRIWS1OISRaVaAF3KYX0hAAAA",
            Convert.ToBase64String(encodedPayload.Payload.Span));
        Assert.NotNull(encodedPayload.Metadata);
        Assert.True(encodedPayload.Metadata.ContainsKey("ops"));
        Assert.Equal("Dapr.Serialization.SystemTextJson[0],Dapr.Encoding.Utf8[0],Dapr.Compression.Gzip[0],Dapr.Integrity.Sha256[0]", encodedPayload.Metadata["ops"]);
        Assert.True(encodedPayload.Metadata.ContainsKey("Dapr.Integrity.Sha256[0]hash"));
        Assert.Equal("Ehr18bGgwtfe/uq8MbfnIQkbsUYOAHt7xWNAecRo2DI=", encodedPayload.Metadata["Dapr.Integrity.Sha256[0]hash"]);

        encodedPayload.Metadata["Dapr.Integrity.Sha256[0]hash"] = "abc123";
        
        // Act & Assert #2
        var decoderPipeline = factory.CreateDecodingPipeline<SimpleRegistration>(encodedPayload.Metadata);
        await Assert.ThrowsAsync<DaprException>(async () =>
            await decoderPipeline.ReverseProcessAsync(encodedPayload.Payload, encodedPayload.Metadata));
    }

    [Fact]
    public async Task EndToEndWithDuplicateOperations()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDaprDataProcessingPipeline()
            .WithCompressor<GzipCompressor>()
            .WithSerializer<SystemTextJsonSerializer<DuplicateRegistration>>()
            .WithIntegrity(_ => new Sha256Validator())
            .WithEncoder(c => new Utf8Encoder());

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<DataPipelineFactory>();

        var encoderPipeline = factory.CreateEncodingPipeline<DuplicateRegistration>();
        var record = new DuplicateRegistration("Don't worry - this is only a test!");
        
        // Act
        var encodedPayload = await encoderPipeline.ProcessAsync(record, CancellationToken.None);

        // Assert
        Assert.NotNull(encodedPayload);
        Assert.True(encodedPayload.Payload.Length > 0);
        Assert.Equal("H4sIAAAAAAAACpPv5mAAA67VYae8z/iGbgoqOnm+W9PUwMBIP1DjvL7WqpALoRonT+iEMSz6s2eOV6tL66Qrj4Rcl0YxiFw4c8oIqBUAdhx5/UQAAAA=",
            Convert.ToBase64String(encodedPayload.Payload.Span));
        Assert.NotNull(encodedPayload.Metadata);
        Assert.True(encodedPayload.Metadata.ContainsKey("ops"));
        Assert.Equal("Dapr.Serialization.SystemTextJson[0],Dapr.Encoding.Utf8[0],Dapr.Compression.Gzip[0],Dapr.Integrity.Sha256[0],Dapr.Compression.Gzip[1],Dapr.Integrity.Sha256[1]", encodedPayload.Metadata["ops"]);
        Assert.True(encodedPayload.Metadata.ContainsKey("Dapr.Integrity.Sha256[0]hash"));
        Assert.Equal("9+H+ngzx1fru8VdywlpoT0E20JqBXm1k81Un/o7z0ZM=", encodedPayload.Metadata["Dapr.Integrity.Sha256[0]hash"]);
        Assert.True(encodedPayload.Metadata.ContainsKey("Dapr.Integrity.Sha256[1]hash"));
        Assert.Equal("r9EkN6xWpuB9saAWGy92aGvU0T8dkLt2Kur5/ItSf2s=", encodedPayload.Metadata["Dapr.Integrity.Sha256[1]hash"]);
        
        // Act #2
        var decoderPipeline = factory.CreateDecodingPipeline<DuplicateRegistration>(encodedPayload.Metadata);
        var decodedPayload = await decoderPipeline.ReverseProcessAsync(encodedPayload.Payload, encodedPayload.Metadata);
        
        // Assert #2
        Assert.Equal(record, decodedPayload.Payload);
    }
    
    private record SampleRecord(string Name, int Value);

    [DataPipeline(typeof(GzipCompressor), typeof(SystemTextJsonSerializer<SimpleRegistration>), typeof(Utf8Encoder), typeof(Sha256Validator))]
    private record SimpleRegistration(string Name);

    [DataPipeline(typeof(SystemTextJsonSerializer<DuplicateRegistration>), typeof(GzipCompressor), typeof(Utf8Encoder), typeof(Sha256Validator), typeof(GzipCompressor), typeof(Sha256Validator))]
    private record DuplicateRegistration(string Name);
}
