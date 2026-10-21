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

using Google.Protobuf.WellKnownTypes;

namespace Dapr.Actors.Next.Core.Timers;

/// <summary>
/// Decodes the <c>data</c> payload the sidecar stores for reminders and timers.
/// </summary>
/// <remarks>
/// The sidecar keeps the payload in a <see cref="BytesValue"/> packed into <see cref="Any"/>, but the bytes
/// inside depend on how the registration was made:
/// <list type="bullet">
/// <item>gRPC registration (what this SDK does): the request bytes JSON-encoded, that is a base64 JSON string.</item>
/// <item>HTTP registration: the raw JSON value of the request's <c>data</c> field.</item>
/// </list>
/// The wrapper does not record which form was used, so a JSON string that is valid base64 is ambiguous on the
/// wire. The contract applied here is the one the callback itself sees: the payload is decoded exactly as the
/// actor event stream decodes it before dispatching the timer or reminder turn, so get/list return the same
/// bytes the callback receives. Any payload that is not a base64 JSON string is returned as it was stored.
/// </remarks>
internal static class ActorSchedulePayloadDecoder
{
    /// <summary>
    /// Returns the payload bytes the actor callback receives for the stored <paramref name="data"/>.
    /// </summary>
    public static ReadOnlyMemory<byte> DecodeCallbackData(Any? data)
    {
        if (data is null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        if (!data.Is(BytesValue.Descriptor))
        {
            return data.Value.Memory;
        }

        var payload = data.Unpack<BytesValue>().Value;
        if (payload.IsEmpty)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(payload.Memory);
            if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.String &&
                document.RootElement.TryGetBytesFromBase64(out var bytes))
            {
                return bytes;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Non-JSON callback data is also supported by the byte[] scheduling API.
        }

        return payload.Memory;
    }

    /// <summary>
    /// Returns the callback payload as JSON text, or <see langword="null"/> when no payload is stored.
    /// </summary>
    public static string? DecodeJson(Any? data)
    {
        var bytes = DecodeCallbackData(data);
        return bytes.IsEmpty ? null : System.Text.Encoding.UTF8.GetString(bytes.Span);
    }
}
