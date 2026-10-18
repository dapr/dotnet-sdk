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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dapr.Workflow.Payloads;

/// <summary>
/// A reference to a workflow payload that has been offloaded to an external store rather than
/// persisted inline in workflow history. The reference is the only thing that crosses the
/// orchestrator boundary; the payload itself is retrieved on demand by an
/// SDK payload codec or by an explicit resolution call.
/// </summary>
/// <remarks>
/// <para>
/// The JSON form of this type is a versioned wire format intended for cross-SDK interoperability.
/// Currently only the .NET SDK supports it. It is a single-property object whose
/// property name is <see cref="SentinelProperty"/>, which allows a converter to distinguish a
/// reference from any user payload without a wrapper type. The format is versioned via
/// <see cref="Version"/>; readers must reject versions they do not understand.
/// </para>
/// <para>
/// Instances are immutable. Workflow histories are long-lived, so this type must remain
/// backward-compatible with every version that has ever been written.
/// </para>
/// </remarks>
public record PayloadReference
{
    /// <summary>
    /// The JSON property name that identifies a serialized reference envelope.
    /// </summary>
    public const string SentinelProperty = "$daprRef";
 
    /// <summary>
    /// The envelope version written by this version of the SDK.
    /// </summary>
    public const int CurrentVersion = 1;
 
    /// <summary>
    /// Initializes a new instance of the <see cref="PayloadReference"/> class.
    /// </summary>
    /// <param name="storeName">The name of the Dapr component holding the payload.</param>
    /// <param name="key">The key or file name under which the payload is stored.</param>
    /// <param name="size">The size of the stored payload in bytes, after any stage transformations.</param>
    /// <param name="contentHash">A lowercase hexadecimal SHA-256 of the serialized payload before any stage transformations.</param>
    /// <param name="stages">The ordered list of transformations applied before storage, or an empty list.</param>
    /// <param name="version">The envelope version. Defaults to <see cref="CurrentVersion"/>.</param>
    public PayloadReference(string storeName, string key, long size, string contentHash,
        IReadOnlyList<PayloadStage>? stages = null, int version = CurrentVersion)
    {
        StoreName = storeName ?? throw new ArgumentNullException(nameof(storeName));
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Size = size;
        ContentHash = contentHash ?? throw new ArgumentNullException(nameof(contentHash));
        Stages = stages ?? Array.Empty<PayloadStage>();
        Version = version;
    }
 
    /// <summary>
    /// Gets the envelope format version.
    /// </summary>
    public int Version { get; init; }
 
    /// <summary>
    /// Gets the name of the Dapr component that holds the payload.
    /// </summary>
    public string StoreName { get; init; }
 
    /// <summary>
    /// Gets the key or file name under which the payload is stored in <see cref="StoreName"/>.
    /// </summary>
    public string Key { get; init; }
 
    /// <summary>
    /// Gets the size of the stored payload in bytes, after any stage transformations were applied.
    /// </summary>
    public long Size { get; init; }
 
    /// <summary>
    /// Gets the lowercase hexadecimal SHA-256 hash of the serialized payload before any stage
    /// transformations were applied. Used for integrity verification and idempotent writes.
    /// </summary>
    public string ContentHash { get; init; }
 
    /// <summary>
    /// Gets the ordered list of transformations (for example compression or encryption) applied to
    /// the payload before it was stored. Readers reverse these in reverse order after retrieval.
    /// </summary>
    public IReadOnlyList<PayloadStage> Stages { get; init; }
 
    /// <summary>
    /// Returns a typed view of this reference.
    /// </summary>
    /// <typeparam name="T">The type the payload is expected to deserialize to.</typeparam>
    /// <returns>A <see cref="PayloadReference{T}"/> sharing this reference's values.</returns>
    public PayloadReference<T> AsTyped<T>() => new(StoreName, Key, Size, ContentHash, Stages, Version);
 
    /// <summary>
    /// Determines whether <paramref name="type"/> is <see cref="PayloadReference"/> or a closed
    /// <see cref="PayloadReference{T}"/>.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> if the type represents a reference; otherwise <see langword="false"/>.</returns>
    internal static bool IsReferenceType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type == typeof(PayloadReference)
               || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(PayloadReference<>));
    }
 
    /// <summary>
    /// Performs a cheap check to determine whether a serialized payload is a reference envelope.
    /// This inspects the leading characters only; a positive result should be confirmed by
    /// <see cref="TryParse"/>.
    /// </summary>
    /// <param name="json">The serialized payload as produced by <see cref="Dapr.Common.Serialization.IDaprSerializer"/>.</param>
    /// <returns><see langword="true"/> if the payload appears to be a reference envelope.</returns>
    internal static bool LooksLikeEnvelope(ReadOnlySpan<char> json)
    {
        var trimmed = json.TrimStart();
        if (trimmed.IsEmpty || trimmed[0] != '{')
        {
            return false;
        }
 
        var window = trimmed[..Math.Min(64, trimmed.Length)];
        return window.IndexOf(SentinelProperty.AsSpan(), StringComparison.Ordinal) >= 0;
    }
 
    /// <summary>
    /// Attempts to parse a reference envelope from its serialized form.
    /// </summary>
    /// <param name="json">The serialized payload.</param>
    /// <param name="reference">The parsed reference, if successful.</param>
    /// <returns><see langword="true"/> if the payload is a valid, supported reference envelope.</returns>
    public static bool TryParse(string? json, out PayloadReference? reference)
    {
        reference = null;
        if (json is null || !LooksLikeEnvelope(json))
        {
            return false;
        }
 
        try
        {
            var envelope = JsonSerializer.Deserialize(json, PayloadReferenceJsonContext.Default.PayloadEnvelope);
            if (envelope?.Ref is null || envelope.Ref.V > CurrentVersion)
            {
                return false;
            }
 
            var r = envelope.Ref;
            reference = new PayloadReference(r.Store, r.Key, r.Size, r.Sha256,
                r.Stages?.Select(s => new PayloadStage(s.N, s.M)).ToArray(), r.V);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
 
    /// <summary>
    /// Serializes this reference to its cross-SDK JSON envelope form. The result is what the
    /// workflow runtime persists in place of the payload.
    /// </summary>
    /// <returns>The JSON envelope.</returns>
    public string ToEnvelope()
    {
        var envelope = new PayloadEnvelope
        {
            Ref = new PayloadEnvelopeBody
            {
                V = Version,
                Store = StoreName,
                Key = Key,
                Size = Size,
                Sha256 = ContentHash,
                Stages = Stages.Count == 0 ? null : Stages.Select(s => new PayloadEnvelopeStage { N = s.Name, M = s.Metadata }).ToArray(),
            },
        };
        return JsonSerializer.Serialize(envelope, PayloadReferenceJsonContext.Default.PayloadEnvelope);
    }
 
    // ---- Wire shape. Kept separate from the public record so the public API can evolve
    //      independently of the persisted format. ----
 
    internal sealed class PayloadEnvelope
    {
        [JsonPropertyName(SentinelProperty)]
        public PayloadEnvelopeBody? Ref { get; set; }
    }
 
    internal sealed class PayloadEnvelopeBody
    {
        [JsonPropertyName("v")] public int V { get; set; }
        [JsonPropertyName("store")] public string Store { get; set; } = "";
        [JsonPropertyName("key")] public string Key { get; set; } = "";
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("sha256")] public string Sha256 { get; set; } = "";
        [JsonPropertyName("stages")] public PayloadEnvelopeStage[]? Stages { get; set; }
    }
 
    internal sealed class PayloadEnvelopeStage
    {
        [JsonPropertyName("n")] public string N { get; set; } = "";
        [JsonPropertyName("m")] public IReadOnlyDictionary<string, string>? M { get; set; }
    }
}

/// <summary>
/// A <see cref="PayloadReference"/> that carries the expected deserialized type of the payload.
/// Declaring an input as <see cref="PayloadReference{T}"/> bypasses automatic materialization on read.
/// Outgoing references are subject to the write preference and cross-app inlining policy.
/// A receiver expecting a reference must not be sent inline data.
/// </summary>
/// <typeparam name="T">The type the payload is expected to deserialize to.</typeparam>
public sealed record PayloadReference<T> : PayloadReference
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayloadReference{T}"/> class.
    /// </summary>
    /// <inheritdoc cref="PayloadReference(string, string, long, string, IReadOnlyList{PayloadStage}?, int)"/>
    public PayloadReference(string storeName, string key, long size, string contentHash,
        IReadOnlyList<PayloadStage>? stages = null, int version = CurrentVersion)
        : base(storeName, key, size, contentHash, stages, version)
    {
    }
}
