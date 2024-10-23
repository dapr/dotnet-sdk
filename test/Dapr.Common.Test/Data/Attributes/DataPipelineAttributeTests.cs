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

using System.Text.Unicode;
using Dapr.Common.Data.Attributes;
using Dapr.Common.Data.Operations.Providers.Compression;
using Dapr.Common.Data.Operations.Providers.Encoding;
using Dapr.Common.Data.Operations.Providers.Serialization;
using Xunit;

namespace Dapr.Common.Test.Data.Attributes;

public class DataPipelineAttributeTests
{
    [Fact]
    public void DataOperationAttribute_ShouldThrowExceptionForInvalidTypes()
    {
        // Arrange & Act & Assert
        Assert.Throws<DaprException>(() => new DataPipelineAttribute(typeof(InvalidOperation)));
    }

    [Fact]
    public void DataOperationAttribute_ShouldRegisterValidTypes()
    {
        // Arrange & Act
        var attribute = new DataPipelineAttribute(typeof(GzipCompressor), typeof(Utf8Encoder), typeof(SystemTextJsonSerializer<MyRecord>));

        // Assert
        Assert.Equal(3, attribute.DataOperationTypes.Count);
        Assert.Contains(typeof(GzipCompressor), attribute.DataOperationTypes);
        Assert.Contains(typeof(Utf8Encoder), attribute.DataOperationTypes);
        Assert.Contains(typeof(SystemTextJsonSerializer<MyRecord>), attribute.DataOperationTypes);
    }

    private sealed class InvalidOperation
    {
    }

    private sealed record MyRecord(string Name);
}
