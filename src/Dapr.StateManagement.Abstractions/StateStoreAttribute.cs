// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

namespace Dapr.StateManagement;

/// <summary>
/// Identifies a partial interface as a typed Dapr state store client bound to a specific
/// Dapr state store component.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to a <c>partial interface</c> that extends
/// <see cref="IDaprStateStoreClient"/>. The Dapr source generator will then emit:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///       A sealed internal implementation class that forwards all
///       <see cref="IDaprStateStoreClient"/> calls to a <see cref="DaprStateManagementClient"/>
///       with the specified <see cref="StoreName"/> pre-filled.
///     </description>
///   </item>
///   <item>
///     <description>
///       A DI registration extension method on
///       <c>Dapr.StateManagement.Extensions.IDaprStateManagementBuilder</c>
///       named <c>Add{InterfaceName}</c> (with a leading <c>I</c> stripped when present).
///     </description>
///   </item>
/// </list>
/// <para>
/// Example usage:
/// </para>
/// <code>
/// [StateStore("mystore")]
/// public partial interface IMyStateStore : IDaprStateStoreClient { }
/// </code>
/// <para>
/// Then register in your DI setup:
/// </para>
/// <code>
/// builder.Services
///     .AddDaprStateManagementClient()
///     .AddMyStateStore();
/// </code>
/// <para>
/// And inject into your services:
/// </para>
/// <code>
/// public class MyService(IMyStateStore store)
/// {
///     public Task&lt;Widget?&gt; GetWidgetAsync(string id) =&gt;
///         store.GetStateAsync&lt;Widget&gt;(id);
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
public sealed class StateStoreAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StateStoreAttribute"/> class.
    /// </summary>
    /// <param name="storeName">
    /// The name of the Dapr state store component. This must match the <c>metadata.name</c>
    /// of a configured Dapr state store component YAML.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="storeName"/> is <see langword="null"/> or empty.
    /// </exception>
    public StateStoreAttribute(string storeName)
    {
        if (string.IsNullOrEmpty(storeName))
            throw new ArgumentException("The store name must not be null or empty.", nameof(storeName));

        StoreName = storeName;
    }

    /// <summary>
    /// Gets the name of the Dapr state store component to which the annotated interface is bound.
    /// </summary>
    public string StoreName { get; }
}
