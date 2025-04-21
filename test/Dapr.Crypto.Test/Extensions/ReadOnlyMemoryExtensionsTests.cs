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

using Dapr.Crypto.Extensions;

namespace Dapr.Crypto.Test.Extensions;

public class ReadOnlyMemoryExtensionsTests
{
    [Fact]
    public void CreateMemoryStream_EmptyMemory_ReturnsEmptyMemoryStream()
    {
        var emptyMemory = ReadOnlyMemory<byte>.Empty;
        var result = emptyMemory.CreateMemoryStream(isReadOnly: true);
        Assert.NotNull(result);
        Assert.Equal(0, result.Length);
        Assert.True(result.CanRead);
        Assert.False(result.CanWrite);
    }

    [Fact]
    public void CreateMemoryStream_NonEmptyMemory_ReturnsMemoryStream()
    {
        byte[] data = { 1, 2, 3, 4, 5 };
        var memory = new ReadOnlyMemory<byte>(data);

        var result = memory.CreateMemoryStream(isReadOnly: true);

        Assert.NotNull(result);
        Assert.Equal(data.Length, result.Length);
        Assert.True(result.CanRead);
        Assert.False(result.CanWrite);
    }
    
    [Fact]
    public void CreateMemoryStream_NonEmptyMemory_ReturnsWriteableMemoryStream()
    {
        byte[] data = { 1, 2, 3, 4, 5 };
        var memory = new ReadOnlyMemory<byte>(data);

        var result = memory.CreateMemoryStream(isReadOnly: false);

        Assert.NotNull(result);
        Assert.Equal(data.Length, result.Length);
        Assert.True(result.CanRead);
        Assert.True(result.CanWrite);
    }
}
