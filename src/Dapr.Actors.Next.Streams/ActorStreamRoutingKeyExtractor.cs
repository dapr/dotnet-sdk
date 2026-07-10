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

using System.Text.Json;

namespace Dapr.Actors.Next.Streams;

/// <summary>
/// Extracts actor ids from CloudEvents attributes or JSON content paths.
/// </summary>
public sealed class ActorStreamRoutingKeyExtractor
{
    /// <summary>
    /// Extracts the actor id selected by the subscription route expression.
    /// </summary>
    public string ExtractActorId(ActorStreamSubscription subscription, ActorStreamEvent evt)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentNullException.ThrowIfNull(evt);

        subscription.Validate();
        var routeBy = subscription.RouteBy;
        if (evt.TryGetAttribute(routeBy, out var attributeValue))
        {
            return EnsureValue(attributeValue, routeBy);
        }

        if (string.Equals(routeBy, "subject", StringComparison.OrdinalIgnoreCase)
            && evt.TryGetAttribute("subject", out var subject))
        {
            return EnsureValue(subject, routeBy);
        }

        var path = routeBy.StartsWith("data.", StringComparison.OrdinalIgnoreCase)
            ? routeBy[5..]
            : routeBy;
        return ExtractFromJson(evt.Data, path, routeBy);
    }

    private static string ExtractFromJson(ReadOnlyMemory<byte> data, string path, string routeBy)
    {
        if (data.IsEmpty)
        {
            throw new ArgumentException($"RouteBy '{routeBy}' could not be resolved because the CloudEvent data is empty.", nameof(routeBy));
        }

        using var document = JsonDocument.Parse(data);
        var current = document.RootElement;
        if (path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(segment => current.ValueKind != JsonValueKind.Object || !TryGetProperty(current, segment, out current)))
        {
            throw new ArgumentException($"RouteBy '{routeBy}' could not be resolved from CloudEvent data.", nameof(routeBy));
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => EnsureValue(current.GetString(), routeBy),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => current.ToString(),
            _ => throw new ArgumentException($"RouteBy '{routeBy}' resolved to a non-scalar CloudEvent data value.", nameof(routeBy)),
        };
    }

    private static string EnsureValue(string? value, string routeBy) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"RouteBy '{routeBy}' resolved to an empty actor id.", nameof(routeBy))
            : value;

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
