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

namespace Dapr.Vector;

/// <summary>
/// Generates an implementation and an Add{Name} DI registration extension for a
/// top-level, non-generic partial interface extending <see cref="IDaprVectorCollection{T}"/>.
/// The implementation forwards operations to DaprVectorClient with the component,
/// collection and payload type bound at compile time.
/// </summary>
[AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
public sealed class VectorCollectionAttribute : Attribute
{
    /// <summary>Identifies the component and collection used by the generated implementation.</summary>
    public VectorCollectionAttribute(string storeName, string collectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);
        StoreName = storeName;
        CollectionName = collectionName;
    }

    /// <summary>The configured Dapr Vector component name.</summary>
    public string StoreName { get; }

    /// <summary>The collection name within the Vector component.</summary>
    public string CollectionName { get; }
}
