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

using System.Threading.Tasks;
using Dapr.Cryptography.Extensions;

namespace Dapr.Cryptography.Test.Extensions;

public class AsyncEnumerableExtensionsTests
{
    [Fact]
    public async Task Empty_ShouldReturnEmptyAsyncEnumerable()
    {
        // Arrange
        var asyncEnumerable = AsyncEnumerableExtensions.Empty<int>();

        // Act & Assert
        await foreach (var item in asyncEnumerable)
        {
            Assert.Fail("Expected no items in the async enumerable.");
        }
    }

    [Fact]
    public async Task Empty_ShouldReturnEmptyAsyncEnumerable_ForStringType()
    {
        // Arrange
        var asyncEnumerable = AsyncEnumerableExtensions.Empty<string>();

        // Act & Assert
        await foreach (var item in asyncEnumerable)
        {
            Assert.Fail("Expected no items in the async enumerable.");
        }
    }

    [Fact]
    public async Task Empty_ShouldReturnEmptyAsyncEnumerable_ForCustomType()
    {
        // Arrange
        var asyncEnumerable = AsyncEnumerableExtensions.Empty<MyCustomType>();

        // Act & Assert
        await foreach (var item in asyncEnumerable)
        {
            Assert.Fail("Expected no items in the async enumerable.");
        }
    }

    private class MyCustomType
    {
        // Custom type definition
    }
}
