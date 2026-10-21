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

using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Dapr.Actors.Next.Core.Timers;

/// <summary>
/// Decodes the <c>data</c> payload the sidecar returns for reminders and timers back to its JSON text.
/// </summary>
internal static class ActorSchedulePayloadDecoder
{
    public static string? DecodeJson(Any? data)
    {
        if (data?.Value is not { Length: > 0 } bytes)
        {
            return null;
        }

        if (TryDecodeBytesValue(bytes, out var nestedBytes))
        {
            return DecodePayload(nestedBytes, unwrapJsonBase64String: true);
        }

        return DecodePayload(bytes, unwrapJsonBase64String: false);
    }

    private static bool TryDecodeBytesValue(ByteString bytes, out ByteString nestedBytes)
    {
        try
        {
            nestedBytes = BytesValue.Parser.ParseFrom(bytes).Value;
            return nestedBytes.Length > 0;
        }
        catch (InvalidProtocolBufferException)
        {
            nestedBytes = ByteString.Empty;
            return false;
        }
    }

    private static string? DecodePayload(ByteString bytes, bool unwrapJsonBase64String)
    {
        if (bytes.Length == 0)
        {
            return null;
        }

        var text = System.Text.Encoding.UTF8.GetString(bytes.Span);
        if (!unwrapJsonBase64String)
        {
            return text;
        }

        try
        {
            var encoded = System.Text.Json.JsonSerializer.Deserialize<string>(text);
            if (string.IsNullOrEmpty(encoded))
            {
                return text;
            }

            var decoded = Convert.FromBase64String(encoded);
            return System.Text.Encoding.UTF8.GetString(decoded);
        }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException)
        {
            return text;
        }
    }
}
