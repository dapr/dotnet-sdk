// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

using System.Text;
using System.Text.Json;
using Dapr.Actors.Next.Core.Timers;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Dapr.Actors.Next.Core.Test;

/// <summary>
/// The sidecar stores timer and reminder payloads in a BytesValue packed into Any. A gRPC registration
/// (this SDK) stores the request bytes JSON-encoded as a base64 string; an HTTP registration stores the raw
/// JSON of the request's "data" field. Get/list must decode both exactly as the callback transport does.
/// </summary>
public sealed class ActorSchedulePayloadDecoderTests
{
    [Theory]
    [InlineData("""{"x":1}""")]
    [InlineData("\"hello\"")]
    [InlineData("\"YWJj\"")]
    [InlineData("123")]
    [InlineData("raw bytes")]
    public void Grpc_registered_payloads_read_back_as_the_original_bytes(string original)
    {
        var data = GrpcRegistration(Encoding.UTF8.GetBytes(original));

        Assert.Equal(original, ActorSchedulePayloadDecoder.DecodeJson(data));
    }

    [Theory]
    [InlineData("""{"x":1}""")]
    [InlineData("[1,2,3]")]
    [InlineData("123")]
    [InlineData("true")]
    [InlineData("\"hello\"")]
    [InlineData("\"not base64!\"")]
    public void Http_registered_payloads_read_back_as_their_raw_json(string rawJson)
    {
        var data = HttpRegistration(rawJson);

        Assert.Equal(rawJson, ActorSchedulePayloadDecoder.DecodeJson(data));
    }

    [Fact]
    public void Http_registered_base64_looking_string_is_decoded_like_the_callback_payload()
    {
        // The wire does not distinguish an HTTP string "YWJj" from a gRPC registration of the bytes "abc".
        // Reading it back follows the same rule as dispatch, so get/list match what the callback receives.
        var data = HttpRegistration("\"YWJj\"");

        Assert.Equal("abc", ActorSchedulePayloadDecoder.DecodeJson(data));
        Assert.Equal("abc", Encoding.UTF8.GetString(ActorSchedulePayloadDecoder.DecodeCallbackData(data).Span));
    }

    [Fact]
    public void Empty_or_missing_payloads_read_back_as_null()
    {
        Assert.Null(ActorSchedulePayloadDecoder.DecodeJson(null));
        Assert.Null(ActorSchedulePayloadDecoder.DecodeJson(new Any()));
        Assert.Null(ActorSchedulePayloadDecoder.DecodeJson(Any.Pack(new BytesValue())));
        Assert.Null(ActorSchedulePayloadDecoder.DecodeJson(GrpcRegistration([])));
    }

    [Fact]
    public void Payloads_that_are_not_a_bytes_value_are_returned_verbatim()
    {
        var unknown = new Any { TypeUrl = "type.googleapis.com/test.Payload", Value = ByteString.CopyFromUtf8("\"YWJj\"") };
        var untyped = new Any { Value = ByteString.CopyFromUtf8("\"YWJj\"") };

        Assert.Equal("\"YWJj\"", ActorSchedulePayloadDecoder.DecodeJson(unknown));
        Assert.Equal("\"YWJj\"", ActorSchedulePayloadDecoder.DecodeJson(untyped));
    }

    // Mirrors the runtime's RegisterActorTimer/RegisterActorReminder gRPC path: json.Marshal([]byte) then wrapperspb.Bytes.
    private static Any GrpcRegistration(byte[] requestBytes) =>
        Any.Pack(new BytesValue { Value = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(requestBytes)) });

    // Mirrors the runtime's CreateTimerRequest/CreateReminderRequest UnmarshalJSON: the raw "data" JSON in wrapperspb.Bytes.
    private static Any HttpRegistration(string rawJson) =>
        Any.Pack(new BytesValue { Value = ByteString.CopyFromUtf8(rawJson) });
}
