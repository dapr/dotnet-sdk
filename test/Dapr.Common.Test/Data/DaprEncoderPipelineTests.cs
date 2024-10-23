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
using System.Threading.Tasks;
using Dapr.Common.Data;
using Dapr.Common.Data.Attributes;
using Dapr.Common.Data.Operations;
using Dapr.Common.Data.Operations.Providers.Compression;
using Dapr.Common.Data.Operations.Providers.Encoding;
using Dapr.Common.Data.Operations.Providers.Integrity;
using Dapr.Common.Data.Operations.Providers.Serialization;
using Xunit;

namespace Dapr.Common.Test.Data;

public class DaprEncoderPipelineTests
{
    [Fact]
    public async Task ProcessAsync_ShouldProcessBasicOperations()
    {
        // Arrange
        var operations = new List<IDaprDataOperation>
        {
            new SystemTextJsonSerializer<SampleRecord>(),
            new Utf8Encoder()
        };
        var pipeline = new DaprEncoderPipeline<SampleRecord>(operations);

        // Act
        var result = await pipeline.ProcessAsync(new SampleRecord("Sample", 15));

        // Assert
        Assert.Equal("eyJuYW1lIjoiU2FtcGxlIiwidmFsdWUiOjE1fQ==", Convert.ToBase64String(result.Payload.Span));
        Assert.True(result.Metadata.ContainsKey("ops"));
        Assert.Equal("Dapr.Serialization.SystemTextJson[0],Dapr.Encoding.Utf8[0]", result.Metadata["ops"]);
    }

    [Fact]
    public async Task ProcessAsync_ShouldProcessOptionalOperations()
    {
        // Arrange
        var operations = new List<IDaprDataOperation>
        {
            new GzipCompressor(),
            new SystemTextJsonSerializer<SampleRecord>(),
            new Utf8Encoder(),
            new Sha256Validator()
        };
        var pipeline = new DaprEncoderPipeline<SampleRecord>(operations);

        // Act
        var result = await pipeline.ProcessAsync(new SampleRecord("Sample", 15));

        Assert.Equal("H4sIAAAAAAAACqtWykvMTVWyUgpOzC3ISVXSUSpLzCkFChia1gIAotvhPBwAAAA=", Convert.ToBase64String(result.Payload.Span));
        Assert.Equal(2, result.Metadata.Keys.Count);
        Assert.True(result.Metadata.ContainsKey("Dapr.Integrity.Sha256[0]hash"));
        Assert.Equal("x9yYvPm6j9Xd7X1Iwz08iQFKidQQXR9giprO3SBZg7Y=", result.Metadata["Dapr.Integrity.Sha256[0]hash"]);
        Assert.True(result.Metadata.ContainsKey("ops"));
        Assert.Equal("Dapr.Serialization.SystemTextJson[0],Dapr.Encoding.Utf8[0],Dapr.Compression.Gzip[0],Dapr.Integrity.Sha256[0]", result.Metadata["ops"]);
    }
    
    [Fact]
    public async Task ProcessAsync_ShouldProcessDuplicateOperations()
    {
        // Arrange
        var operations = new List<IDaprDataOperation>
        {
            new GzipCompressor(),
            new SystemTextJsonSerializer<SampleRecord>(),
            new Utf8Encoder(),
            new Sha256Validator(),
            new GzipCompressor()
        };
        var pipeline = new DaprEncoderPipeline<SampleRecord>(operations);
        
        // Act
        var result = await pipeline.ProcessAsync(new SampleRecord("Sample", 15));

        Assert.Equal("H4sIAAAAAAAACpPv5mAAA67VYae8z/iGbgri8juje8Iz9FKglvcZTVYuiVnXmBgW3X5oIwNUBQAguNy9LwAAAA==", Convert.ToBase64String(result.Payload.Span));
        Assert.Equal(2, result.Metadata.Keys.Count);
        Assert.True(result.Metadata.ContainsKey("Dapr.Integrity.Sha256[0]hash"));
        Assert.Equal("x9yYvPm6j9Xd7X1Iwz08iQFKidQQXR9giprO3SBZg7Y=", result.Metadata["Dapr.Integrity.Sha256[0]hash"]);
        Assert.True(result.Metadata.ContainsKey("ops"));
        Assert.Equal("Dapr.Serialization.SystemTextJson[0],Dapr.Encoding.Utf8[0],Dapr.Compression.Gzip[0],Dapr.Integrity.Sha256[0],Dapr.Compression.Gzip[1]", result.Metadata["ops"]);
    }

    private record SampleRecord(string Name, int Value);
}
