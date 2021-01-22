// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Test
{
    using System.Text.Json;
    using Dapr.Client.Autogen.Test.Grpc.v1;
    using FluentAssertions;
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

            type.Should().BeEquivalentTo(response);
            any.TypeUrl.Should().Be(string.Empty);
            type.Name.Should().Be("test");
        }

        private class Response
        {
            public string Name { get; set; }
        }
    }
}
