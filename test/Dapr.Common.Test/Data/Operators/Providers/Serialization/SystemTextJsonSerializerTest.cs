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

using System.Threading.Tasks;
using Dapr.Common.Data.Operations;
using Dapr.Common.Data.Operations.Providers.Serialization;
using Xunit;

namespace Dapr.Common.Test.Data.Operators.Providers.Serialization;

public class SystemTextJsonSerializerTest
{
    [Fact]
    public async Task ExecuteAsync_ShouldSerialize()
    {
        //Arrange
        var validator = new SystemTextJsonSerializer<TestObject>();
        var input = new TestObject("Test", 15);
        
        //Act
        var result = await validator.ExecuteAsync(input);
        
        //Assert
        Assert.NotNull(result);
        Assert.Equal("{\"name\":\"Test\",\"count\":15}", result.Payload);
    }

    [Fact]
    public async Task ReverseAsync_ShouldDeserialize()
    {
        //Arrange
        var validator = new SystemTextJsonSerializer<TestObject>();
        const string input = "{\"name\":\"Test\",\"count\":15}";
        var payload = new DaprOperationPayload<string>(input);
        
        //Act
        var result = await validator.ReverseAsync(payload, string.Empty);

        //Assert
        Assert.NotNull(result);
        var expected = new TestObject("Test", 15);
        Assert.Equal(expected, result.Payload);
    }

    private record TestObject(string Name, int Count);
}
