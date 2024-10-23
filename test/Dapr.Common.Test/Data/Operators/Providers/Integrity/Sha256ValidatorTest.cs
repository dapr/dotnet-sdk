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
using System.Threading;
using System.Threading.Tasks;
using Dapr.Common.Data.Operations;
using Dapr.Common.Data.Operations.Providers.Integrity;
using Xunit;

namespace Dapr.Common.Test.Data.Operators.Providers.Integrity;

public class Sha256ValidatorTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldCalculateChecksum()
    {
        // Arrange
        var validator = new Sha256Validator();
        var input = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3, 4, 5 });

        // Act
        var result = await validator.ExecuteAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Metadata.ContainsKey("hash"));
    }

    [Fact]
    public async Task ReverseAsync_ShouldValidateChecksumWithoutMetadataHeader()
    {
        // Arrange
        var validator = new Sha256Validator();
        var input = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3, 4, 5 });
        var result = new DaprOperationPayload<ReadOnlyMemory<byte>>(input);

        await validator.ReverseAsync(result, $"{validator.Name}[0]", CancellationToken.None);
    }

    [Fact]
    public async Task ReverseAsync_ShouldValidateChecksum()
    {
        // Arrange
        var validator = new Sha256Validator();
        var input = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3, 4, 5 });
        var result = await validator.ExecuteAsync(input);

        // Act & Assert
        await validator.ReverseAsync(result, $"{validator.Name}[0]", CancellationToken.None);
    }

    [Fact]
    public async Task ReverseAsync_ShouldThrowExceptionForInvalidChecksum()
    {
        // Arrange
        var validator = new Sha256Validator();
        var input = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3, 4, 5 });
        var result = await validator.ExecuteAsync(input);
        result = result with
        {
            Payload = new ReadOnlyMemory<byte>(new byte[] { 6, 7, 8, 9, 0 })
        };
        
        // Act & Assert
        await Assert.ThrowsAsync<DaprException>(() => validator.ReverseAsync(result,string.Empty, CancellationToken.None));
    }
}
