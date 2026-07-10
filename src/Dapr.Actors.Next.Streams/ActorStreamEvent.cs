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

namespace Dapr.Actors.Next.Streams;

/// <summary>
/// CloudEvents-shaped event received from a streaming pub/sub subscription.
/// </summary>
public sealed record ActorStreamEvent(
    string Id,
    string PubsubName,
    string Topic,
    ReadOnlyMemory<byte> Data,
    IReadOnlyDictionary<string, string> Attributes)
{
    /// <summary>
    /// Gets the W3C traceparent attribute when present.
    /// </summary>
    public string? TraceParent => TryGetAttribute("traceparent", out var value) ? value : null;

    /// <summary>
    /// Tries to read a CloudEvents attribute by ordinal or case-insensitive key.
    /// </summary>
    public bool TryGetAttribute(string name, out string value)
    {
        if (Attributes.TryGetValue(name, out value!))
        {
            return true;
        }

        foreach (var item in Attributes)
        {
            if (string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = item.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }
}
