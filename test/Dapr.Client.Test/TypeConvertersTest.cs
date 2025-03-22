// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

namespace Dapr.Client.Test
{
    using System.Text.Json;
    using Shouldly;
    using Xunit;

    public class TypeConvertersTest
    {
        [Fact]
        public void AnyConversion_JSON_Serialization_Deserialization()
        {
            var response = new Response()
            {
                Name = "test"
            };

            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

            var any = TypeConverters.ToJsonAny(response, options);
            var type = TypeConverters.FromJsonAny<Response>(any, options);

            type.ShouldBeEquivalentTo(response);
            any.TypeUrl.ShouldBe(string.Empty);
            type.Name.ShouldBe("test");
        }

        private class Response
        {
            public string Name { get; set; }
        }
    }
}
